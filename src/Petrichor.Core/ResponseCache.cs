using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
namespace Petrichor.Core
{
    public sealed class CachedResponse
    {
        public DateTimeOffset SavedAt { get; set; }
        public JObject Data { get; set; }
    }
    public interface IResponseCacheStore
    {
        Task<CachedResponse> ReadAsync(string key);
        Task WriteAsync(string key, CachedResponse value);
        Task ClearAsync();
    }
    /// <summary>Read-through cache: stale data is returned before revalidation. Shared work outlives a page.</summary>
    public sealed class ResponseCache
    {
        private readonly IResponseCacheStore store;
        private readonly object sync = new object();
        private readonly Dictionary<string, CachedResponse> memory = new Dictionary<string, CachedResponse>();
        private readonly Dictionary<string, Task<CachedResponse>> reads = new Dictionary<string, Task<CachedResponse>>();
        private readonly Dictionary<string, Task<JObject>> flights = new Dictionary<string, Task<JObject>>();
        private readonly SemaphoreSlim storageGate = new SemaphoreSlim(1, 1);
        private int generation;
        private long invalidatedAt;
        private readonly Dictionary<string,long> revisions=new Dictionary<string,long>();
        private readonly AsyncLocal<Action<string>> observer = new AsyncLocal<Action<string>>();
        public event Action<string> Changed;
        public long Version { get; private set; }
        public ResponseCache(IResponseCacheStore store) { this.store = store ?? throw new ArgumentNullException(nameof(store)); }
        public bool HasChanged(IEnumerable<string> keys,long version){lock(sync)return invalidatedAt>version||keys.Any(key=>revisions.ContainsKey(key)&&revisions[key]>version);}
        public IDisposable ObserveReads(Action<string> onRead) { var old=observer.Value; observer.Value=onRead; return new Scope(()=>observer.Value=old); }
        private sealed class Scope : IDisposable { private readonly Action close; public Scope(Action close){this.close=close;} public void Dispose()=>close(); }
        public async Task<JObject> GetAsync(string key, TimeSpan freshFor, Func<Task<JObject>> fetch, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            observer.Value?.Invoke(key);
            int epoch; Task<CachedResponse> read;
            lock (sync) {
                epoch = generation;
                if (!reads.TryGetValue(key, out read)) { read = Task.Run(() => ReadAsync(key, epoch)); reads[key] = read; }
            }
            var cached = await WaitAsync(read, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            lock(sync){if(epoch!=generation)throw new OperationCanceledException();}
            if (cached != null && DateTimeOffset.UtcNow - cached.SavedAt < TimeSpan.FromDays(7)) {
                if (DateTimeOffset.UtcNow - cached.SavedAt > freshFor) Observe(Refresh(key, epoch, fetch));
                return (JObject)cached.Data.DeepClone();
            }
            var result=await WaitAsync(Refresh(key, epoch, fetch), ct).ConfigureAwait(false);
            lock(sync){if(epoch!=generation)throw new OperationCanceledException();}
            return (JObject)result.DeepClone();
        }
        private async Task<CachedResponse> ReadAsync(string key, int epoch)
        {
            lock (sync) { CachedResponse hot; if (memory.TryGetValue(key, out hot)) return hot; }
            CachedResponse value = null;
            await storageGate.WaitAsync().ConfigureAwait(false);
            try { lock (sync) { if (epoch != generation) return null; } value = await store.ReadAsync(key).ConfigureAwait(false); }
            catch { /* A corrupt/evicted cache entry is a miss, never an application error. */ }
            finally { storageGate.Release(); }
            if (value?.Data != null) lock (sync) { if (epoch == generation) Remember(key, value); }
            return value?.Data != null ? value : null;
        }
        private Task<JObject> Refresh(string key, int epoch, Func<Task<JObject>> fetch)
        {
            var flightKey=epoch+"/"+key;
            lock (sync) {
                Task<JObject> flight;
                if (flights.TryGetValue(flightKey, out flight)) return flight;
                flight = Task.Run(async () => {
                    try {
                        var data = await fetch().ConfigureAwait(false);
                        var value = new CachedResponse { SavedAt = DateTimeOffset.UtcNow, Data = (JObject)data.DeepClone() };
                        bool changed;
                        lock (sync) {
                            if (epoch != generation) return data;
                            CachedResponse old; changed = !memory.TryGetValue(key, out old) || !JToken.DeepEquals(old.Data, data);
                            Remember(key, value); reads[key] = Task.FromResult(value);
                            if (changed) {Version++;revisions[key]=Version;}
                        }
                        await storageGate.WaitAsync().ConfigureAwait(false);
                        try { bool valid; lock (sync) valid = epoch == generation; if (valid) await store.WriteAsync(key, value).ConfigureAwait(false); }
                        catch { /* Disk pressure must not discard a successful network response. */ }
                        finally { storageGate.Release(); }
                        if (changed) Changed?.Invoke(key);
                        return data;
                    }
                    finally { lock (sync) flights.Remove(flightKey); }
                });
                flights[flightKey] = flight; return flight;
            }
        }
        private void Remember(string key, CachedResponse value)
        {
            if (memory.Count >= 128 && !memory.ContainsKey(key)) { var oldest = memory.OrderBy(x => x.Value.SavedAt).First().Key; memory.Remove(oldest); reads.Remove(oldest); revisions.Remove(oldest); }
            memory[key] = value;
            // Failed and missing reads may otherwise accumulate during a long search session.
            if (reads.Count > 256) foreach (var k in reads.Keys.Where(k => !memory.ContainsKey(k)).Take(128).ToArray()) reads.Remove(k);
        }
        public async Task ClearAsync()
        {
            lock (sync) { generation++; Version++;invalidatedAt=Version; memory.Clear(); reads.Clear(); flights.Clear();revisions.Clear(); }
            await storageGate.WaitAsync().ConfigureAwait(false);
            try { await store.ClearAsync().ConfigureAwait(false); } catch { }
            finally { storageGate.Release(); }
            Changed?.Invoke("*");
        }
        private static async void Observe(Task task) { try { await task.ConfigureAwait(false); } catch { /* Keep stale content on a failed background refresh. */ } }
        private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken ct)
        {
            if (!ct.CanBeCanceled) return await task.ConfigureAwait(false);
            var cancelled = new TaskCompletionSource<bool>();
            using (ct.Register(() => cancelled.TrySetResult(true))) {
                if (await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false) != task) { ct.ThrowIfCancellationRequested(); }
                return await task.ConfigureAwait(false);
            }
        }
    }
}
