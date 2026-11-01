using Android.Media;
using Android.Net.Wifi;
using Android.OS;
using Petrichor.Core;
namespace Petrichor.Mobile.Services;

public sealed class PlaybackController : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
{
    public PlaybackQueue Queue { get; } = new() { Repeat = RepeatMode.All };
    public SleepSchedule Sleep { get; } = new();
    public MediaPlayer? Player { get; private set; }
    public bool IsPlaying { get { try { return Player?.IsPlaying == true; } catch(Java.Lang.IllegalStateException) { return false; } } }
    public bool IsLoading { get; private set; }
    public bool IsPreview { get; private set; }
    public string? Error { get; private set; }
    public StreamInfo? CurrentStream { get; private set; }
    public string QualityLabel => CurrentStream is{} s ? (s.IsLossless?"无损 · ":"")+(string.IsNullOrEmpty(s.Format)?"音频":s.Format.ToUpperInvariant())+(s.Bitrate>0?" · "+s.Bitrate/1000+" kbps":"") : Player==null?"尚未加载音频":"音质信息不可用";
    public bool QualityLimited => CurrentStream is{IsLossless:false,RequestedQuality:AudioQuality.Lossless or AudioQuality.HiRes};
    public TimeSpan Position { get { try { return TimeSpan.FromMilliseconds(Player?.CurrentPosition ?? 0); } catch(Java.Lang.IllegalStateException) { return TimeSpan.Zero; } } }
    public TimeSpan Duration { get { try { return TimeSpan.FromMilliseconds(Math.Max(0, Player?.Duration ?? Queue.Current?.DurationMs ?? 0)); } catch(Java.Lang.IllegalStateException) { return TimeSpan.FromMilliseconds(Queue.Current?.DurationMs ?? 0); } } }
    public event Action? Changed;
    private CancellationTokenSource? loading;
    private readonly AudioManager audio = (AudioManager)global::Android.App.Application.Context.GetSystemService(global::Android.Content.Context.AudioService)!;
    private AudioFocusRequestClass? focus;
    private WifiManager.WifiLock? wifiLock;
    private bool resumeAfterFocus, remoteSource, playRequested;
    private int pendingTick;
    private long lastHeartbeat;
    private long positionAtSample, sampledAt;
    private int displayDuration;
    private bool samplePlaying;
    public bool IsUiActive { get; private set; } = true;
    public bool AnimateProgress => IsUiActive && samplePlaying;
    public int DisplayDurationMs => displayDuration;
    public double DisplayPositionMs => Math.Clamp(positionAtSample + (samplePlaying ? System.Environment.TickCount64 - sampledAt : 0), 0, Math.Max(0, displayDuration));
    private void SamplePosition()
    {
        positionAtSample = (long)Position.TotalMilliseconds; sampledAt = System.Environment.TickCount64;
        displayDuration = (int)Duration.TotalMilliseconds; samplePlaying = IsPlaying;
    }
    public void SetUiActive(bool active)
    {
        if (IsUiActive == active) return;
        IsUiActive = active;
        LogState("ui", "active=" + active);
        if (active) { SamplePosition(); Changed?.Invoke(); }
    }
    private readonly System.Threading.Timer timer;
    public PlaybackController()
    {
        PlaybackDiagnostics.Initialize();
        timer = new(_ => {
            var now = System.Environment.TickCount64;
            if (playRequested && PlaybackDiagnostics.Enabled && now - Interlocked.Read(ref lastHeartbeat) >= 30000) {
                Interlocked.Exchange(ref lastHeartbeat, now);
                MainThread.BeginInvokeOnMainThread(() => { if (playRequested) LogState("heartbeat"); });
            }
            if ((!Sleep.IsActive && (!IsUiActive || Changed == null || !IsPlaying)) || Interlocked.Exchange(ref pendingTick, 1) != 0) return;
            MainThread.BeginInvokeOnMainThread(() => {
                try {
                    if (Sleep.OnDeadline(DateTimeOffset.Now, IsPlaying) == SleepAction.PauseNow) Pause();
                    if (IsUiActive) { SamplePosition(); Changed?.Invoke(); }
                } finally { Volatile.Write(ref pendingTick, 0); }
            });
        }, null, 500, 500);
    }
    public async Task PlayAsync(IEnumerable<Track> tracks, Track? start = null)
    {
        var list = tracks.ToList(); if (list.Count == 0) return;
        Queue.Replace(list, start == null ? 0 : Math.Max(0, list.FindIndex(t => t.Id == start.Id)));
        await PlayCurrentAsync();
    }
    public Task PlayCurrentAsync() => LoadCurrentAsync(0, 0);
    private async Task LoadCurrentAsync(int resumePosition, int recoveryAttempt)
    {
        // Each request owns its cancellation source and candidate player. Older
        // continuations must never clear a newer request's state or metadata.
        loading?.Cancel();
        var request = new CancellationTokenSource();
        loading = request;
        var token = request.Token;
        var track = Queue.Current;
        playRequested = track != null; resumeAfterFocus = false;
        IsLoading = track != null; Error = null; IsPreview = false;
        ReleasePlayer(); CurrentStream = null; Notify();
        LogState("load", "attempt=" + recoveryAttempt + " resumeMs=" + resumePosition);
        MediaPlayer? next = null;
        PowerManager.WakeLock? preparing = null;
        try
        {
            if (track == null) return;
            PlaybackService.Start();
            var power = (PowerManager)global::Android.App.Application.Context.GetSystemService(global::Android.Content.Context.PowerService)!;
            preparing = power.NewWakeLock(WakeLockFlags.Partial, "Petrichor:prepare");
            preparing!.Acquire(60000);
            var local = AppServices.Offline.FindAudio(track);
            remoteSource = local == null;
            LogState("source", remoteSource ? "remote" : "local");
            SetNetworkLock(remoteSource);
            if (recoveryAttempt > 0) await Task.Delay(500 * recoveryAttempt, token);
            string source;
            StreamInfo stream;
            if (local != null) {
                source = local;
                stream = new StreamInfo { Format = Path.GetExtension(local).TrimStart('.') };
                using var metadata = new MediaMetadataRetriever();
                try {
                    metadata.SetDataSource(local);
                    var mime = metadata.ExtractMetadata(MetadataKey.Mimetype) ?? "";
                    if (mime.Contains("flac")) stream.Format = "flac";
                    stream.Bitrate = int.TryParse(metadata.ExtractMetadata(MetadataKey.Bitrate), out var rate) ? rate : 0;
                } catch(Java.Lang.RuntimeException) { }
            } else {
                stream = await AppServices.Api.GetStreamAsync(track.Id, (AudioQuality)Preferences.Get("quality", 192000), token);
                source = stream.Url.ToString();
            }
            token.ThrowIfCancellationRequested();
            CurrentStream = stream; IsPreview = stream.IsPreview; remoteSource = local == null;
            SetNetworkLock(remoteSource);
            next = new MediaPlayer();
            next.SetAudioAttributes(new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media)!.SetContentType(AudioContentType.Music)!.Build()!);
            next.SetWakeMode(global::Android.App.Application.Context, WakeLockFlags.Partial);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var seeked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            next.Prepared += (_, _) => ready.TrySetResult();
            next.SeekComplete += (sender, _) => { seeked.TrySetResult(); if (ReferenceEquals(Player, sender)) Notify(); };
            next.Error += (sender, e) => {
                e.Handled = true;
                var message = "音频加载失败（"+stream.Format+"，"+e.What+"/"+e.Extra+"），请检查网络或更换音质重试。";
                LogState("native.error", "what=" + (int)e.What + " extra=" + (int)e.Extra + " attempt=" + recoveryAttempt);
                var transient = (int)e.Extra is -1004 or -110 || (int)e.What == 100;
                Exception error = transient ? new PlaybackNetworkException(message) : new IOException(message);
                ready.TrySetException(error);
                if (resumePosition > 0) seeked.TrySetException(error);
                if (ReferenceEquals(Player, sender)) {
                    var position = (int)Math.Max(Position.TotalMilliseconds, DisplayPositionMs);
                    var recover = remoteSource && playRequested && transient && recoveryAttempt < 2;
                    playRequested = false; ReleasePlayer();
                    if (recover) _ = RecoverAsync(position, recoveryAttempt + 1); else Fail(error);
                }
            };
            next.Completion += async (sender, _) => {
                // Released/replaced players may still have callbacks queued on
                // the Android looper. Only the active player may advance.
                if (!ReferenceEquals(Player, sender) || !playRequested) return;
                playRequested = false;
                try {
                    var position = (int)Position.TotalMilliseconds;
                    var duration = (int)Duration.TotalMilliseconds;
                    LogState("completion", "premature=" + PlaybackCompletion.IsPremature(position, duration));
                    if (PlaybackCompletion.IsPremature(position, duration)) {
                        if (remoteSource && recoveryAttempt < 2) await LoadCurrentAsync(position, recoveryAttempt + 1);
                        else Fail(new IOException("音频连接提前结束，请重试播放。"));
                        return;
                    }
                    if (Sleep.OnTrackEnded()) Pause(); else await NextAsync(false);
                } catch (Exception error) { Fail(error); }
            };
            await next.SetDataSourceAsync(source);
            token.ThrowIfCancellationRequested();
            next.PrepareAsync();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(45), token);
            token.ThrowIfCancellationRequested();
            if (resumePosition > 0) {
                next.SeekTo(resumePosition, MediaPlayerSeekMode.Closest);
                await seeked.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            }
            token.ThrowIfCancellationRequested();
            if (!RequestFocus()) throw new InvalidOperationException("暂时无法获得音频焦点，请稍后重试。");
            Player = next; next = null;
            Player.Start();
            LogState("started", "attempt=" + recoveryAttempt + " resumeMs=" + resumePosition);
            _ = RememberAsync(track);
        }
        catch (System.OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) {
            if (!token.IsCancellationRequested) {
                LogState("load.error", "type=" + error.GetType().Name + " code=" + error.HResult);
                if (remoteSource && playRequested && recoveryAttempt < 2 &&
                    error is HttpRequestException or PlaybackNetworkException or TimeoutException or Java.IO.IOException)
                    await LoadCurrentAsync(resumePosition, recoveryAttempt + 1);
                else { Fail(error); throw; }
            }
        }
        finally {
            next?.Release(); next?.Dispose();
            if (preparing?.IsHeld == true) preparing.Release();
            preparing?.Dispose();
            if (ReferenceEquals(loading, request)) {
                loading = null; IsLoading = false;
                if (!IsPlaying) SetNetworkLock(false);
                Notify();
            }
            request.Dispose();
        }
    }
    private sealed class PlaybackNetworkException(string message) : IOException(message);
    private async Task RecoverAsync(int position, int attempt)
    {
        try { await LoadCurrentAsync(position, attempt); }
        catch { /* The controller exposes the final error after bounded recovery. */ }
    }
    private static async Task RememberAsync(Track track)
    {
        try { await AppServices.Offline.RememberAsync(track); }
        catch (Exception error) { global::Android.Util.Log.Warn("Petrichor", "Recent playback: " + error.Message); }
    }
    private bool RequestFocus()
    {
        focus ??= new AudioFocusRequestClass.Builder(AudioFocus.Gain)
            .SetAudioAttributes(new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media)!.SetContentType(AudioContentType.Music)!.Build()!)!
            .SetOnAudioFocusChangeListener(this)!.Build()!;
        return audio.RequestAudioFocus(focus!) == AudioFocusRequest.Granted;
    }
    private void SetNetworkLock(bool enabled)
    {
        if (enabled) {
            if (wifiLock == null) {
                var wifi = (WifiManager)global::Android.App.Application.Context.GetSystemService(global::Android.Content.Context.WifiService)!;
                wifiLock = wifi.CreateWifiLock(global::Android.Net.WifiMode.FullHighPerf, "Petrichor:stream");
                wifiLock!.SetReferenceCounted(false);
            }
            if (!wifiLock.IsHeld) wifiLock.Acquire();
        } else if (wifiLock?.IsHeld == true) wifiLock.Release();
    }
    private void ReleasePlayer()
    {
        var previous = Player; Player = null;
        previous?.Release(); previous?.Dispose(); SetNetworkLock(false);
    }
    private void PausePlayer() { if (IsPlaying) Player?.Pause(); SetNetworkLock(false); }
    public void Pause()
    {
        LogState("pause");
        playRequested = false; loading?.Cancel(); IsLoading = false; resumeAfterFocus = false;
        PausePlayer(); if (focus != null) audio.AbandonAudioFocusRequest(focus); Notify();
    }
    public async Task ToggleAsync()
    {
        if (Queue.Current == null) return;
        if (IsPlaying || IsLoading) Pause();
        else if (Player != null) {
            if (RequestFocus()) {
                PlaybackService.Start(); SetNetworkLock(remoteSource); playRequested = true;
                Player.Start(); Notify();
            }
        } else await PlayCurrentAsync();
    }
    public async Task NextAsync(bool user = true)
    { LogState("next", "user=" + user); if (Queue.MoveNext(user)) await PlayCurrentAsync(); else Pause(); }
    public async Task PreviousAsync()
    { LogState("previous"); if (Queue.MovePrevious()) await PlayCurrentAsync(); }
    public void Seek(TimeSpan time) { Player?.SeekTo((int)Math.Clamp(time.TotalMilliseconds, 0, Duration.TotalMilliseconds)); Notify(); }
    public new void Notify() { SamplePosition(); if (IsUiActive) Changed?.Invoke(); PlaybackService.Refresh(); }
    private void Fail(Exception error)
    {
        LogState("failure", "type=" + error.GetType().Name + " code=" + error.HResult);
        Error = error.Message; playRequested = resumeAfterFocus = false; PausePlayer();
        if (focus != null) audio.AbandonAudioFocusRequest(focus);
        Notify();
    }
    private void LogState(string name, string details = "") => PlaybackDiagnostics.Write(name, "track=" + (Queue.Current?.Id ?? 0) + " index=" + Queue.Index + " positionMs=" + (long)Position.TotalMilliseconds + " durationMs=" + (long)Duration.TotalMilliseconds + " playing=" + IsPlaying + " loading=" + IsLoading + " ui=" + IsUiActive + " powerSave=" + PlaybackDiagnostics.PowerSaveMode + " " + details);
    public void OnAudioFocusChange(AudioFocus change)
    {
        LogState("focus", "change=" + change);
        MainThread.BeginInvokeOnMainThread(() => {
            if (change == AudioFocus.LossTransientCanDuck) Player?.SetVolume(.2f, .2f);
            else if (change == AudioFocus.LossTransient) { resumeAfterFocus = IsPlaying || IsLoading; playRequested = false; loading?.Cancel(); IsLoading = false; PausePlayer(); Notify(); }
            else if (change == AudioFocus.Loss) Pause();
            else if (change == AudioFocus.Gain) {
                Player?.SetVolume(1, 1);
                if (resumeAfterFocus) {
                    resumeAfterFocus = false;
                    if (Player != null) { SetNetworkLock(remoteSource); playRequested = true; Player.Start(); Notify(); }
                    else _ = RecoverAsync(0, 0);
                }
            }
        });
    }
}