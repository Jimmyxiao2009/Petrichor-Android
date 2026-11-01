using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Petrichor.Core;

internal static class ResponseCacheTests
{
    private sealed class Store : IResponseCacheStore
    {
        public readonly ConcurrentDictionary<string,CachedResponse> Entries=new();
        public bool BrokenRead,BrokenWrite;
        public Task<CachedResponse> ReadAsync(string key){if(BrokenRead)throw new InvalidOperationException("corrupt");Entries.TryGetValue(key,out var value);return Task.FromResult(value);}
        public Task WriteAsync(string key,CachedResponse value){if(BrokenWrite)throw new InvalidOperationException("disk full");Entries[key]=value;return Task.CompletedTask;}
        public Task ClearAsync(){Entries.Clear();return Task.CompletedTask;}
    }
    private static JObject Value(int n)=>new(){["value"]=n};
    private static TaskCompletionSource<JObject> Pending()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task Until(Func<bool> predicate){for(int i=0;i<200&&!predicate();i++)await Task.Delay(5);if(!predicate())throw new Exception("cache background work timed out");}
    public static async Task RunAsync(Action<bool,string> check)
    {
        var store=new Store();var cache=new ResponseCache(store);var pending=Pending();int calls=0;
        Task<JObject> Fetch(){Interlocked.Increment(ref calls);return pending.Task;}
        using var cancel=new CancellationTokenSource();
        var cancelled=cache.GetAsync("guest/a",TimeSpan.FromMinutes(5),Fetch,cancel.Token);
        var second=cache.GetAsync("guest/a",TimeSpan.FromMinutes(5),Fetch,CancellationToken.None);
        await Until(()=>calls==1);cancel.Cancel();bool didCancel=false;
        try{await cancelled;}catch(OperationCanceledException){didCancel=true;}
        check(didCancel&&!second.IsCompleted,"Leaving one page cancels only its wait, preserving shared prefetch");
        pending.SetResult(Value(1));var one=await second;
        check(calls==1&&(int)one["value"]==1,"Concurrent first loads share a single network request");
        one["value"]=99;
        var again=await cache.GetAsync("guest/a",TimeSpan.FromMinutes(5),Fetch,CancellationToken.None);
        check(calls==1&&(int)again["value"]==1,"Fresh cached data is reused and returned as an isolated copy");
        var restarted=new ResponseCache(store);
        var persisted=await restarted.GetAsync("guest/a",TimeSpan.FromMinutes(5),()=>throw new Exception("unexpected network"),CancellationToken.None);
        check((int)persisted["value"]==1,"A new process cache can hydrate persisted data without networking");

        var staleStore=new Store();staleStore.Entries["guest/stale"]=new(){SavedAt=DateTimeOffset.UtcNow.AddHours(-1),Data=Value(2)};
        var stale=new ResponseCache(staleStore);var refresh=Pending();var changed=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        stale.Changed+=_=>changed.TrySetResult(true);
        var immediate=stale.GetAsync("guest/stale",TimeSpan.FromMinutes(5),()=>refresh.Task,CancellationToken.None);
        var staleResult=await immediate.WaitAsync(TimeSpan.FromSeconds(1));
        check((int)staleResult["value"]==2&&!refresh.Task.IsCompleted,"Stale content displays before a blocked revalidation finishes");
        var revision=stale.Version;refresh.SetResult(Value(3));await changed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var refreshed=await stale.GetAsync("guest/stale",TimeSpan.FromMinutes(5),()=>throw new Exception("unexpected"),CancellationToken.None);
        check((int)refreshed["value"]==3&&stale.HasChanged(new[]{"guest/stale"},revision),"Background revalidation updates storage and marks dependent pages dirty");
        check(!stale.HasChanged(new[]{"other"},revision),"Unrelated cache updates do not reset other pages");

        var failureStore=new Store();failureStore.Entries["stale"]=new(){SavedAt=DateTimeOffset.UtcNow.AddHours(-1),Data=Value(4)};
        var failure=new ResponseCache(failureStore);int failures=0;
        Task<JObject> Fail(){Interlocked.Increment(ref failures);throw new InvalidOperationException("offline");}
        var offline=await failure.GetAsync("stale",TimeSpan.Zero,Fail,CancellationToken.None);await Until(()=>failures==1);
        check((int)offline["value"]==4&&(int)failureStore.Entries["stale"].Data["value"]==4,"Offline refresh failure preserves usable stale data");
        var corrupt=new ResponseCache(new Store{BrokenRead=true,BrokenWrite=true});
        var recovered=await corrupt.GetAsync("x",TimeSpan.Zero,()=>Task.FromResult(Value(5)),CancellationToken.None);
        check((int)recovered["value"]==5,"Corrupt cache and disk-full writes fall back to successful network content");

        var clearStore=new Store();var clearing=new ResponseCache(clearStore);var old=Pending();var entered=false;
        var before=clearing.GetAsync("private/1",TimeSpan.Zero,()=>{entered=true;return old.Task;},CancellationToken.None);
        await Until(()=>entered);await clearing.ClearAsync();old.SetResult(Value(6));bool invalidated=false;
        try{await before;}catch(OperationCanceledException){invalidated=true;}
        check(invalidated&&clearStore.Entries.IsEmpty,"Logout/invalidation prevents an old in-flight response from restoring deleted data");
        var account1=await clearing.GetAsync("user/1",TimeSpan.FromMinutes(5),()=>Task.FromResult(Value(7)),CancellationToken.None);
        var account2=await clearing.GetAsync("user/2",TimeSpan.FromMinutes(5),()=>Task.FromResult(Value(8)),CancellationToken.None);
        check((int)account1["value"]==7&&(int)account2["value"]==8,"Account partitions never reuse another user's cached response");

        var fake=new FakeHandler();var session=new MemorySessionStore();await session.SaveAsync("MUSIC_U=test-session");
        using var client=new NeteaseClient(new DesktopCrypto(),session,fake){Cache=new ResponseCache(new Store())};
        await client.RestoreLocalSessionAsync(new UserProfile{Id=123,Nickname="cached"});
        check(client.IsLoggedIn&&fake.Payloads.Count==0,"Local session/profile restoration does not wait for a network request");
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":[{\"id\":1,\"name\":\"before\"}]}");
        await client.GetMyPlaylistsAsync();await client.GetMyPlaylistsAsync();
        check(fake.Payloads.Count==1,"API read allowlist serves repeated library visits from cache");
        fake.Bodies.Enqueue("{\"code\":200}");await client.SetLikedAsync(1,true);
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":[{\"id\":2,\"name\":\"after\"}]}");var updated=await client.GetMyPlaylistsAsync();
        check(fake.Payloads.Count==3&&updated.Single().Id==2,"Successful account mutations invalidate dependent cached reads");
        for(int i=0;i<2;i++){fake.Bodies.Enqueue("{\"code\":200,\"data\":[{\"id\":1,\"url\":\"https://example.org/a.mp3\"}]}");await client.GetStreamAsync(1);}
        check(fake.Payloads.Count==5,"Expiring playback URLs bypass the page cache");
        await client.LogoutAsync();bool loginRequired=false;try{await client.GetMyPlaylistsAsync();}catch(MusicApiException e){loginRequired=e.RequiresLogin;}
        check(loginRequired,"Cached library data never bypasses the login requirement after logout");
        fake.Bodies.Enqueue("{\"code\":200,\"unikey\":\"again\"}");var qr=await client.BeginQrLoginAsync();
        fake.Bodies.Enqueue("{\"code\":803}");fake.LoginCookie=true;
        fake.Bodies.Enqueue("{\"code\":200,\"profile\":{\"userId\":123,\"nickname\":\"again\"}}");await client.CheckQrLoginAsync(qr);
        check((await session.LoadAsync()).Contains("MUSIC_U=test-session"),"Logging back in after logout persists a repeated cookie value");
        var expiredStore=new Store();expiredStore.Entries["old"]=new(){SavedAt=DateTimeOffset.UtcNow.AddDays(-8),Data=Value(1)};
        var expired=new ResponseCache(expiredStore);var fresh=await expired.GetAsync("old",TimeSpan.FromMinutes(5),()=>Task.FromResult(Value(2)),CancellationToken.None);
        check((int)fresh["value"]==2,"Responses beyond the maximum stale age are refetched before display");
    }
}
