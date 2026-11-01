// Host-side Android doubles let the real PlaybackController run with delayed
// native callbacks, cancelled URL requests and truncated streams deterministically.
namespace Java.IO { public class IOException : System.IO.IOException { } }
namespace Java.Lang {
    public class Object : IDisposable { public virtual void Dispose() { } }
    public class RuntimeException : Exception { }
    public class IllegalStateException : RuntimeException { }
}
namespace Android.Content {
    public class Context {
        public const string AudioService="audio", PowerService="power", WifiService="wifi";
        public object GetSystemService(string name) => name switch {
            AudioService => new Android.Media.AudioManager(), PowerService => new Android.OS.PowerManager(),
            _ => new Android.Net.Wifi.WifiManager()
        };
    }
}
namespace Android.App { public static class Application { public static Android.Content.Context Context { get; } = new(); } }
namespace Android.OS {
    public enum WakeLockFlags { Partial }
    public class PowerManager {
        public WakeLock NewWakeLock(WakeLockFlags flags, string tag) => new();
        public class WakeLock : IDisposable {
            public bool IsHeld { get; private set; }
            public void Acquire(long timeout) => IsHeld=true;
            public void Release() => IsHeld=false;
            public void Dispose() { }
        }
    }
}
namespace Android.Net { public enum WifiMode { FullHighPerf } }
namespace Android.Net.Wifi {
    public class WifiManager {
        public static int HeldLocks;
        public WifiLock CreateWifiLock(Android.Net.WifiMode mode, string tag) => new();
        public class WifiLock {
            public bool IsHeld { get; private set; }
            public void SetReferenceCounted(bool value) { }
            public void Acquire() { if(!IsHeld) HeldLocks++; IsHeld=true; }
            public void Release() { if(IsHeld) HeldLocks--; IsHeld=false; }
        }
    }
}
namespace Android.Util { public static class Log { public static void Warn(string tag,string text) { } } }
namespace Android.Media {
    public enum AudioUsageKind { Media }
    public enum AudioContentType { Music }
    public enum AudioFocus { Gain, Loss, LossTransient, LossTransientCanDuck }
    public enum AudioFocusRequest { Granted }
    public enum MetadataKey { Mimetype, Bitrate }
    public enum MediaPlayerSeekMode { Closest }
    public class AudioAttributes {
        public class Builder {
            public Builder SetUsage(AudioUsageKind value) => this;
            public Builder SetContentType(AudioContentType value) => this;
            public AudioAttributes Build() => new();
        }
    }
    public class AudioFocusRequestClass {
        public class Builder(AudioFocus value) {
            public Builder SetAudioAttributes(AudioAttributes attributes) => this;
            public Builder SetOnAudioFocusChangeListener(AudioManager.IOnAudioFocusChangeListener listener) => this;
            public AudioFocusRequestClass Build() => new();
        }
    }
    public class AudioManager {
        public interface IOnAudioFocusChangeListener { void OnAudioFocusChange(AudioFocus change); }
        public AudioFocusRequest RequestAudioFocus(AudioFocusRequestClass request) => AudioFocusRequest.Granted;
        public void AbandonAudioFocusRequest(AudioFocusRequestClass request) { }
    }
    public class MediaMetadataRetriever : IDisposable {
        public void SetDataSource(string source) { }
        public string? ExtractMetadata(MetadataKey key) => null;
        public void Dispose() { }
    }
    public class MediaErrorEventArgs : EventArgs {
        public bool Handled { get; set; }
        public int What => 1;
        public int Extra { get; set; } = -1004;
    }
    public class MediaPlayer : IDisposable {
        public static List<MediaPlayer> Created { get; } = [];
        public static bool AutoPrepare = true;
        public static int NativeDuration = 240000;
        public MediaPlayer() { Duration=NativeDuration; Created.Add(this); }
        public bool IsPlaying { get; private set; }
        public bool Released { get; private set; }
        public int CurrentPosition { get; set; }
        public int Duration { get; set; }
        public int Starts { get; private set; }
        public int StartPosition { get; private set; }
        public event EventHandler? Prepared, SeekComplete, Completion;
        public event EventHandler<MediaErrorEventArgs>? Error;
        public void SetAudioAttributes(AudioAttributes attributes) { }
        public void SetWakeMode(Android.Content.Context context, Android.OS.WakeLockFlags flags) { }
        public Task SetDataSourceAsync(string source) => Task.CompletedTask;
        public void PrepareAsync() { if(AutoPrepare) CompletePreparation(); }
        public void CompletePreparation() => Prepared?.Invoke(this,EventArgs.Empty);
        public void SeekTo(int position) { CurrentPosition=position; SeekComplete?.Invoke(this,EventArgs.Empty); }
        public void SeekTo(int position, MediaPlayerSeekMode mode) => SeekTo(position);
        public void Start() { if(Released)throw new Exception("Started released player"); IsPlaying=true; Starts++; StartPosition=CurrentPosition; }
        public void Pause() => IsPlaying=false;
        public void Release() { Released=true; IsPlaying=false; }
        public void Dispose() { }
        public void SetVolume(float left,float right) { }
        // A native callback may already be queued when Release is called.
        public void End() { IsPlaying=false; Completion?.Invoke(this,EventArgs.Empty); }
        public void RaiseError(int extra = -1004) => Error?.Invoke(this,new() { Extra = extra });
    }
}
namespace Petrichor.Mobile.Services {
    using Petrichor.Core;
    public static class MainThread { public static void BeginInvokeOnMainThread(Action action) => action(); }
    public static class Preferences { public static int Get(string key,int fallback) => fallback; }
    public static class PlaybackDiagnostics { public static bool Enabled => false; public static bool PowerSaveMode => false; public static void Initialize() { } public static void Write(string name,string details="") { } }
    public static class PlaybackService { public static void Start() { } public static void Refresh() { } }
    public static class AppServices {
        public static FakeApi Api { get; } = new();
        public static FakeOffline Offline { get; } = new();
    }
    public class FakeApi {
        public Func<long,CancellationToken,Task<StreamInfo>>? Request { get; set; }
        public Task<StreamInfo> GetStreamAsync(long id,AudioQuality quality,CancellationToken token) =>
            Request?.Invoke(id,token) ?? Task.FromResult(Stream(id));
        public static StreamInfo Stream(long id) => new() { TrackId=id, Url=new Uri("https://example.invalid/"+id), Format="flac" };
    }
    public class FakeOffline {
        public string? FindAudio(Track track) => track.LocalFilePath;
        public Task RememberAsync(Track track) => Task.CompletedTask;
    }
}