using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Petrichor.Core;

internal static class MigrationTests
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var fake = new FakeHandler();
        var store = new MemorySessionStore();
        using var client = new NeteaseClient(new DesktopCrypto(), store, fake);
        try { await client.SendLoginCodeAsync("123"); throw new Exception("Invalid number accepted"); }
        catch (ArgumentException) { check(fake.LastUri == null, "SMS invalid phone rejected before network"); }
        fake.Bodies.Enqueue("{\"code\":200,\"data\":true}");
        await client.SendLoginCodeAsync("138 0013 8000");
        check(fake.LastUri.AbsolutePath == "/weapi/sms/captcha/sent" && (string)fake.LastPayload["cellphone"] == "13800138000" && (string)fake.LastPayload["ctcode"] == "86", "SMS request uses the verified phone and country-code fields");
        fake.Bodies.Enqueue("{\"code\":200,\"data\":false}");
        try { await client.SendLoginCodeAsync("13800138000"); throw new Exception("False success"); }
        catch (MusicApiException e) { check(e.Code == 502, "SMS false result never displayed as sent"); }
        fake.Bodies.Enqueue("{\"code\":200}"); fake.LoginCookie = true;
        fake.Bodies.Enqueue("{\"code\":200,\"profile\":{\"userId\":12,\"nickname\":\"短信用户\"}}");
        await client.LoginWithCodeAsync("13800138000", "123456");
        check(client.CurrentUser.Id == 12 && (await store.LoadAsync()).Contains("MUSIC_U="), "SMS login persists and verifies real session before success");
        var loginPayload = fake.Payloads.Single(p => p["phone"] != null);
        check((string)loginPayload["captcha"] == "123456" && (string)loginPayload["countrycode"] == "86" && loginPayload["password"] == null, "Code login sends captcha and country code without a password");
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{\"id\":5000000001,\"name\":\"雨夜\",\"creator\":{\"userId\":12}}}");
        var created = await client.CreatePlaylistAsync(" 雨夜 ", true);
        check(created.Id == 5000000001 && (string)fake.LastPayload["name"] == "雨夜" && (int)fake.LastPayload["privacy"] == 10 && (string)fake.LastPayload["type"] == "NORMAL", "Private playlist creation preserves trimmed name and 64-bit ID");
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{\"id\":8}}");
        await client.CreatePlaylistAsync("公开");
        check((int)fake.LastPayload["privacy"] == 0, "Public playlists explicitly send privacy zero");
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{}}");
        try { await client.CreatePlaylistAsync("test"); throw new Exception("Missing playlist ID accepted"); }
        catch (MusicApiException e) { check(e.Code == 502, "Missing created playlist identity is surfaced"); }
        try { await client.CreatePlaylistAsync("   "); throw new Exception("Blank playlist accepted"); }
        catch (ArgumentException) { check(fake.Bodies.Count == 0, "Blank playlist name never reaches network"); }
        fake.Bodies.Enqueue("{\"code\":200,\"data\":{\"resources\":[{\"baseInfo\":{\"id\":9000000001,\"name\":\"晴天翻唱\",\"radio\":{\"id\":7000000001,\"name\":\"翻唱电台\"},\"mainSong\":{\"id\":6000000001}}}],\"hasMore\":true}}");
        var programs = await client.SearchProgramsAsync(" 晴天 ", 20, 20);
        var program = programs.Items.Single();
        check(fake.LastUri.AbsolutePath == "/weapi/search/voice/get" && (string)fake.LastPayload["keyword"] == "晴天" && (int)fake.LastPayload["offset"] == 20, "Program title search uses voice search instead of radio-name search");
        check(program.RadioId == 7000000001 && program.RadioName == "翻唱电台" && program.Track.Id == 6000000001 && program.Track.ProgramId == 9000000001 && programs.HasMore, "Search result preserves program, playable song and parent podcast identities");
        fake.Bodies.Enqueue("{\"code\":200,\"data\":{\"resources\":[{\"baseInfo\":{\"id\":9001,\"name\":\"晴天\",\"serialNum\":1702165503097,\"mainTrackId\":2107236950,\"radio\":{\"id\":7,\"name\":\"木槿花\"}}}],\"hasMore\":false}}");
        var sparse = (await client.SearchProgramsAsync("晴天")).Items.Single();
        check(sparse.SerialNumber == 1702165503097 && sparse.Track.Id == 2107236950 && sparse.RadioId == 7, "Real-world voice payload handles 64-bit serialNum and mainTrackId without mainSong");
        fake.Bodies.Enqueue("{\"code\":200,\"data\":{\"resources\":[{\"baseInfo\":{\"id\":9002,\"radio\":null,\"dj\":null,\"mainSong\":{\"id\":6,\"al\":null,\"ar\":null}}}],\"hasMore\":false}}");
        var nullable = (await client.SearchProgramsAsync("测试")).Items.Single();
        check(nullable.RadioId == 0 && nullable.Track.Id == 6, "Nullable program metadata does not crash result rendering");
        fake.Bodies.Enqueue("{\"code\":200,\"program\":{\"id\":9,\"name\":\"节目\",\"radio\":{\"id\":7,\"name\":\"播客\"},\"mainSong\":{\"id\":6}}}");
        var detail = await client.GetProgramAsync(9);
        check(detail.RadioId == 7 && detail.Track.Id == 6, "Program detail can resolve a sparse search result before navigation/playback");
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{\"id\":8,\"creator\":{\"userId\":12}}}");
        fake.Bodies.Enqueue("{\"code\":200}");
        await client.RemoveTracksFromPlaylistAsync(8, new long[] { 5000000001, 5000000001, 2 });
        check((string)fake.LastPayload["op"] == "del" && (long)fake.LastPayload["pid"] == 8 && JArray.Parse((string)fake.LastPayload["trackIds"]).Select(v => (long)v).SequenceEqual(new long[] { 5000000001, 2 }), "Playlist removal uses del, deduplicates IDs and preserves 64-bit identities");
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{\"id\":8,\"creator\":{\"userId\":12}}}");
        fake.Bodies.Enqueue("{\"code\":200}"); fake.Bodies.Enqueue("{\"code\":200}");
        await client.RemoveTracksFromPlaylistAsync(8, Enumerable.Range(1, 101).Select(i => (long)i));
        check(JArray.Parse((string)fake.LastPayload["trackIds"]).Count == 1 && (long)JArray.Parse((string)fake.LastPayload["trackIds"])[0] == 101, "Bulk removal splits safely into batches of 100");
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{\"id\":8,\"creator\":{\"userId\":99}}}");
        try { await client.RemoveTrackFromPlaylistAsync(8, 1); throw new Exception("Foreign playlist modification accepted"); }
        catch (MusicApiException e) { check(e.Code == 403 && fake.Bodies.Count == 0 && fake.LastUri.AbsolutePath.EndsWith("playlist/detail"), "Foreign playlists are rejected before any delete request"); }
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{\"id\":8,\"specialType\":5,\"creator\":{\"userId\":12}}}");
        fake.Bodies.Enqueue("{\"code\":200}");
        await client.RemoveTrackFromPlaylistAsync(8, 1);
        check(fake.LastUri.AbsolutePath == "/weapi/radio/like" && !(bool)fake.LastPayload["like"], "Removing from liked music cancels the like instead of editing the playlist");
        fake.Bodies.Enqueue("{\"code\":200,\"playlist\":{\"id\":8,\"creator\":{\"userId\":12}}}");
        fake.Bodies.Enqueue("{\"code\":403,\"message\":\"删除失败\"}");
        try { await client.RemoveTrackFromPlaylistAsync(8, 1); throw new Exception("Delete failure ignored"); }
        catch (MusicApiException e) { check(e.Code == 403 && e.Message == "删除失败", "Remote removal failures propagate without claiming success"); }
        try { await client.RemoveTracksFromPlaylistAsync(8, Array.Empty<long>()); throw new Exception("Empty removal accepted"); }
        catch (ArgumentException) { check(fake.Bodies.Count == 0, "Empty removal is rejected before network"); }
        await client.LogoutAsync();
        try { await client.RemoveTrackFromPlaylistAsync(8, 1); throw new Exception("Guest removal accepted"); }
        catch (MusicApiException e) { check(e.RequiresLogin && fake.Bodies.Count == 0, "Guests cannot modify playlists"); }
        try { await client.CreatePlaylistAsync("雨"); throw new Exception("Guest create accepted"); }
        catch (MusicApiException e) { check(e.RequiresLogin && fake.Bodies.Count == 0, "Guest playlist creation is rejected before network"); }
        fake.Bodies.Enqueue("{\"code\":200}");
        try { await client.LoginWithCodeAsync("13800138000", "123456"); throw new Exception("Cookie-less login accepted"); }
        catch (MusicApiException e) { check(e.Code == 502 && !client.IsLoggedIn, "Cookie-less login never reports success"); }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await client.SearchProgramsAsync("雨", ct: cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { check(true, "Program search cancellation is preserved"); }
    }

    public static async Task RunLiveAsync()
    {
        using var handler = new InspectHandler();
        using var client = new NeteaseClient(new DesktopCrypto(), new MemorySessionStore(), handler);
        var page = await client.SearchProgramsAsync("晴天", 3);
        Console.WriteLine("LIVE PROGRAM RESULTS " + page.Items.Count + ": " + string.Join(" / ", page.Items.Select(p => p.Name + " -> " + p.RadioId)));
        var item = page.Items.FirstOrDefault();
        if (item == null) throw new Exception("Public program search returned no results");
        var detail = await client.GetProgramAsync(item.Id);
        if (detail.RadioId <= 0 || detail.Track.Id <= 0) throw new Exception("Program detail lacks parent/playable identity");
        var radio = await client.GetRadioAsync(detail.RadioId);
        Console.WriteLine("LIVE PROGRAM -> PODCAST: " + radio.Name);
    }

    private sealed class InspectHandler : DelegatingHandler
    {
        public InspectHandler() : base(new HttpClientHandler { UseCookies = false, AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate }) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (request.RequestUri.AbsolutePath.Contains("search/voice"))
            {
                var json = JObject.Parse(await response.Content.ReadAsStringAsync(ct));
                var sample = (JObject)json.DeepClone();
                if (sample["data"]?["resources"] is JArray resources && resources.Count > 1) sample["data"]["resources"] = new JArray(resources[0]);
                var first = sample["data"]?["resources"]?.FirstOrDefault()?["baseInfo"];
                Console.WriteLine("PUBLIC VOICE SHAPE: " + string.Join(",", ((JObject?)first)?.Properties().Select(p => p.Name) ?? Array.Empty<string>()));
            }
            return response;
        }
    }
}
