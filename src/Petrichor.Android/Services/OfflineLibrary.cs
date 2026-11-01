using System.Collections.Concurrent;
using Newtonsoft.Json;
using Petrichor.Core;
namespace Petrichor.Mobile.Services;

public sealed class DownloadStatus
{
    public Track Track { get; init; } = new();
    public double Progress { get; set; }
    public string? Error { get; set; }
}
public sealed class LibraryState
{
    public List<Track> Downloads { get; set; } = [];
    public List<Track> Local { get; set; } = [];
    public List<Track> Recent { get; set; } = [];
}
public sealed class OfflineLibrary
{
    private readonly SemaphoreSlim stateGate = new(1, 1), downloadGate = new(1, 1);
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private LibraryState state = new();
    private Task? initialization;
    private string Root => Path.Combine(FileSystem.AppDataDirectory, "library");
    public IReadOnlyList<Track> Downloads => state.Downloads.ToArray();
    public IReadOnlyList<Track> Local => state.Local.ToArray();
    public IReadOnlyList<Track> Recent => state.Recent.ToArray();
    public ConcurrentDictionary<long, DownloadStatus> Jobs { get; } = new();
    public event Action? Changed;
    public Task InitializeAsync() => initialization ??= LoadAsync();
    private async Task LoadAsync()
    {
        Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, "library.json");
        if (File.Exists(path)) try { state = JsonConvert.DeserializeObject<LibraryState>(await File.ReadAllTextAsync(path)) ?? new(); }
        catch (JsonException) { File.Copy(path, path + ".invalid", true); }
        state.Downloads.RemoveAll(t => !File.Exists(t.LocalFilePath));
        state.Local.RemoveAll(t => !File.Exists(t.LocalFilePath));
    }
    private DateTimeOffset lastProgress;
    private void Notify(bool force = true)
    {
        var now = DateTimeOffset.UtcNow;
        if (!force && now - lastProgress < TimeSpan.FromMilliseconds(250)) return;
        lastProgress = now; MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke());
    }
    private async Task SaveAsync()
    {
        var path = Path.Combine(Root, "library.json");
        await File.WriteAllTextAsync(path + ".tmp", JsonConvert.SerializeObject(state));
        File.Move(path + ".tmp", path, true);
        Notify();
    }
    public string? FindAudio(Track track)
    {
        if (!string.IsNullOrEmpty(track.LocalFilePath) && File.Exists(track.LocalFilePath)) return track.LocalFilePath;
        return state.Downloads.FirstOrDefault(t => t.Id == track.Id && File.Exists(t.LocalFilePath))?.LocalFilePath;
    }
    public string Cover(Track track)
    {
        var cached = Path.Combine(Root, track.Id + ".jpg");
        return File.Exists(cached) ? cached : track.Album?.CoverUrl ?? "";
    }
    public async Task RememberAsync(Track track)
    {
        await InitializeAsync(); await stateGate.WaitAsync();
        try { state.Recent.RemoveAll(t => t.Id == track.Id); state.Recent.Insert(0, track); state.Recent = state.Recent.Take(200).ToList(); await SaveAsync(); }
        finally { stateGate.Release(); }
    }
    public async Task DownloadAsync(Track track, bool allowMobile = false)
    {
        await InitializeAsync();
        if (FindAudio(track) != null || !Jobs.TryAdd(track.Id, new() { Track = track })) return;
        Notify();
        var entered = false;
        var audioPath = Path.Combine(Root, track.Id + ".audio");
        try
        {
            await downloadGate.WaitAsync(); entered = true;
            if (!allowMobile && Preferences.Get("wifiDownloads", true) && !Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi))
                throw new InvalidOperationException("当前不是 Wi-Fi 网络，请在设置中关闭「仅 Wi-Fi 下载」后重试。");
            var source = await AppServices.Api.GetStreamAsync(track.Id, (AudioQuality)Preferences.Get("quality", 192000));
            using var response = await http.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (source.IsPreview) throw new InvalidOperationException("当前账号只能试听，未下载试听片段。");
            var length = response.Content.Headers.ContentLength;
            await using (var input = await response.Content.ReadAsStreamAsync())
            await using (var output = File.Create(audioPath + ".part"))
            {
                var buffer = new byte[65536]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer)) > 0)
                { await output.WriteAsync(buffer.AsMemory(0, read)); total += read; Jobs[track.Id].Progress = length > 0 ? (double)total / length.Value : 0; Notify(false); }
                if (total == 0) throw new IOException("下载文件为空");
            }
            await DownloadFolder.ExportAsync(audioPath+".part",track.ArtistText+" - "+track.Name,source.Format);
            File.Move(audioPath + ".part", audioPath, true);
            var saved = JsonConvert.DeserializeObject<Track>(JsonConvert.SerializeObject(track))!;
            saved.LocalFilePath = audioPath;
            try { if (Uri.TryCreate(track.Album?.CoverUrl, UriKind.Absolute, out var cover)) await File.WriteAllBytesAsync(Path.Combine(Root, track.Id + ".jpg"), await http.GetByteArrayAsync(cover)); } catch (HttpRequestException) { }
            try { await GetLyricsAsync(track); } catch (MusicApiException) { }
            await stateGate.WaitAsync();
            try { state.Downloads.RemoveAll(t => t.Id == track.Id); state.Downloads.Add(saved); await SaveAsync(); }
            finally { stateGate.Release(); }
            Jobs.TryRemove(track.Id, out _);
        }
        catch (Exception e) { Jobs[track.Id].Error = e.Message; if (File.Exists(audioPath + ".part")) File.Delete(audioPath + ".part"); throw; }
        finally { if (entered) downloadGate.Release(); Notify(); }
    }
    public void DismissFailed(long id) { if (Jobs.TryGetValue(id, out var job) && job.Error != null) Jobs.TryRemove(id, out _); Notify(); }
    public async Task DeleteAsync(Track track)
    {
        await stateGate.WaitAsync();
        try
        {
            var stored = state.Downloads.Concat(state.Local).FirstOrDefault(t => t.Id == track.Id);
            if (stored != null && stored.LocalFilePath?.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) == true && File.Exists(stored.LocalFilePath)) File.Delete(stored.LocalFilePath);
            state.Downloads.RemoveAll(t => t.Id == track.Id); state.Local.RemoveAll(t => t.Id == track.Id);
            await SaveAsync();
        }
        finally { stateGate.Release(); }
    }
    public async Task ImportAsync()
    {
        var types = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { [DevicePlatform.Android] = ["audio/*"] });
        var files = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = "导入本地音乐", FileTypes = types });
        if (files == null) return;
        foreach (var file in files)
        {
            var id = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Random.Shared.Next(1000);
            var dest = Path.Combine(Root, id + Path.GetExtension(file.FileName));
            await using (var input = await file.OpenReadAsync())
            await using (var output = File.Create(dest)) { await input.CopyToAsync(output); }
            var track = new Track { Id = id, Name = Path.GetFileNameWithoutExtension(file.FileName), LocalFilePath = dest, Artists = [new Artist { Name = "本地音乐" }] };
            try
            {
                using var metadata = new global::Android.Media.MediaMetadataRetriever();
                metadata.SetDataSource(dest);
                track.Name = metadata.ExtractMetadata(global::Android.Media.MetadataKey.Title) ?? track.Name;
                track.Artists[0].Name = metadata.ExtractMetadata(global::Android.Media.MetadataKey.Artist) ?? "本地音乐";
                if (int.TryParse(metadata.ExtractMetadata(global::Android.Media.MetadataKey.Duration), out var duration)) track.DurationMs = duration;
                var art = metadata.GetEmbeddedPicture();
                if (art?.Length > 0) { var artPath = Path.Combine(Root, id + ".jpg"); await File.WriteAllBytesAsync(artPath, art); track.Album.CoverUrl = artPath; }
            }
            catch (Java.Lang.RuntimeException) { }
            await stateGate.WaitAsync(); try { state.Local.Add(track); await SaveAsync(); } finally { stateGate.Release(); }
        }
    }
    public async Task<Lyrics> GetLyricsAsync(Track track)
    {
        var path = Path.Combine(Root, track.Id + ".lyrics.json");
        if (File.Exists(path)) try { return JsonConvert.DeserializeObject<Lyrics>(await File.ReadAllTextAsync(path))!; } catch (JsonException) { }
        if (track.Id <= 0) return new Lyrics { Lines = [], TranslatedLines = [], Original = "", Translation = "" };
        var lyrics = await AppServices.Api.GetLyricsAsync(track.Id);
        await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(lyrics));
        return lyrics;
    }
}
