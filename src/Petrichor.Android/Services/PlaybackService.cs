using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.Media.Session;
using Android.OS;
using Android.Graphics;
using Petrichor.Core;
using AndroidX.Core.Content;
namespace Petrichor.Mobile.Services;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public class PlaybackService : Service
{
    private const int NotificationId = 42;
    private const string Channel = "petrichor.playback";
    private MediaSession? session;
    private NoisyReceiver? noisy;
    private Bitmap? artwork;
    private string? artworkKey;
    private CancellationTokenSource? artworkLoading;
    private static PlaybackService? instance;
    public static void Start()
    {
        var context = global::Android.App.Application.Context;
        ContextCompat.StartForegroundService(context, new Intent(context, typeof(PlaybackService)));
    }
    public static void Refresh() => MainThread.BeginInvokeOnMainThread(() => instance?.Update());
    public override void OnCreate()
    {
        base.OnCreate(); instance = this; PlaybackDiagnostics.Write("service.create");
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(new NotificationChannel(Channel, "音乐播放", NotificationImportance.Low) { Description = "播放控制与锁屏媒体信息" });
        session = new MediaSession(this, "Petrichor"); session.SetCallback(new SessionControls()); session.Active = true;
        noisy = new NoisyReceiver();
        if (OperatingSystem.IsAndroidVersionAtLeast(33)) RegisterReceiver(noisy, new IntentFilter(AudioManager.ActionAudioBecomingNoisy), ReceiverFlags.NotExported);
        else RegisterReceiver(noisy, new IntentFilter(AudioManager.ActionAudioBecomingNoisy));
        StartForeground(NotificationId, CreateNotification());
        Update();
    }
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, CreateNotification());
        _ = HandleAsync(intent?.Action);
        return StartCommandResult.NotSticky;
    }
    private async Task HandleAsync(string? action)
    {
        try { switch(action) { case "toggle": await AppServices.Playback.ToggleAsync(); break; case "next": await AppServices.Playback.NextAsync(); break; case "previous": await AppServices.Playback.PreviousAsync(); break; } }
        catch { /* Playback controller retains the error for the foreground UI. */ }
        Update();
    }
    private PendingIntent ActionIntent(string action, int id) => PendingIntent.GetService(this, id, new Intent(this, typeof(PlaybackService)).SetAction(action), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)!;
    private Notification CreateNotification()
    {
        var playback = AppServices.Playback; var track = playback.Queue.Current;
        var open = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)).AddFlags(ActivityFlags.SingleTop), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var notification = new Notification.Builder(this, Channel);
        notification.SetSmallIcon(Android.Resource.Drawable.IcMediaPlay);
        notification.SetContentTitle(track?.Name ?? "Petrichor"); notification.SetContentText(track?.ArtistText ?? "正在准备播放");
        notification.SetContentIntent(open); notification.SetVisibility(NotificationVisibility.Public);
        notification.SetOnlyAlertOnce(true); notification.SetOngoing(playback.IsPlaying);
        if (artwork != null) notification.SetLargeIcon(artwork);
        notification.AddAction(new Notification.Action.Builder(Android.Resource.Drawable.IcMediaPrevious, "上一首", ActionIntent("previous",1)).Build());
        notification.AddAction(new Notification.Action.Builder(playback.IsPlaying?Android.Resource.Drawable.IcMediaPause:Android.Resource.Drawable.IcMediaPlay, playback.IsPlaying?"暂停":"播放", ActionIntent("toggle",2)).Build());
        notification.AddAction(new Notification.Action.Builder(Android.Resource.Drawable.IcMediaNext, "下一首", ActionIntent("next",3)).Build());
        var style = new Notification.MediaStyle(); style.SetMediaSession(session!.SessionToken); style.SetShowActionsInCompactView(0,1,2);
        notification.SetStyle(style); return notification.Build();
    }
    private void Update()
    {
        if (session == null) return;
        var p = AppServices.Playback; var track = p.Queue.Current;
        UpdateArtwork(track);
        var metadata = new MediaMetadata.Builder(); metadata.PutString(MediaMetadata.MetadataKeyTitle,track?.Name);
        metadata.PutString(MediaMetadata.MetadataKeyArtist,track?.ArtistText); metadata.PutString(MediaMetadata.MetadataKeyAlbum,track?.Album?.Name);
        metadata.PutString(MediaMetadata.MetadataKeyMediaId,track?.Id.ToString());
        if (artwork != null) {
            metadata.PutBitmap(MediaMetadata.MetadataKeyAlbumArt,artwork);
            metadata.PutBitmap(MediaMetadata.MetadataKeyArt,artwork);
            metadata.PutBitmap(MediaMetadata.MetadataKeyDisplayIcon,artwork);
        }
        metadata.PutLong(MediaMetadata.MetadataKeyDuration,(long)p.Duration.TotalMilliseconds); session.SetMetadata(metadata.Build());
        var state = new PlaybackState.Builder(); state.SetActions(PlaybackState.ActionPlay | PlaybackState.ActionPause | PlaybackState.ActionPlayPause | PlaybackState.ActionSkipToNext | PlaybackState.ActionSkipToPrevious | PlaybackState.ActionSeekTo);
        state.SetState(p.IsLoading?PlaybackStateCode.Buffering:p.Error!=null?PlaybackStateCode.Error:p.IsPlaying?PlaybackStateCode.Playing:PlaybackStateCode.Paused,(long)p.Position.TotalMilliseconds,p.IsPlaying?1:0); session.SetPlaybackState(state.Build());
        if (p.IsPlaying || p.IsLoading) StartForeground(NotificationId,CreateNotification());
        else { StopForeground(StopForegroundFlags.Detach); ((NotificationManager)GetSystemService(NotificationService)!).Notify(NotificationId,CreateNotification()); }
    }
    private void UpdateArtwork(Track? track)
    {
        var source = track == null ? "" : AppServices.Offline.Cover(track);
        var key = track == null ? "" : track.Id + "|" + source;
        if (key == artworkKey) return;
        artworkKey = key;
        artworkLoading?.Cancel();
        artwork?.Dispose(); artwork = null;
        if (track == null) return;
        var request = new CancellationTokenSource();
        artworkLoading = request;
        _ = LoadArtworkAsync(source, key, request);
    }
    private async Task LoadArtworkAsync(string source, string key, CancellationTokenSource request)
    {
        Bitmap? loaded = null;
        try {
            loaded = await Task.Run(() => MediaArtwork.LoadAsync(source, request.Token));
            if (request.IsCancellationRequested || instance != this || artworkKey != key) return;
            artwork = loaded; loaded = null;
            Update();
        } catch (System.OperationCanceledException) { }
        catch (Exception error) { global::Android.Util.Log.Warn("Petrichor", "Media artwork: " + error.Message); }
        finally {
            loaded?.Dispose();
            if (ReferenceEquals(artworkLoading, request)) artworkLoading = null;
            request.Dispose();
        }
    }
    public override IBinder? OnBind(Intent? intent) => null;
    public override void OnDestroy()
    {
        PlaybackDiagnostics.Write("service.destroy");
        instance = null;
        artworkLoading?.Cancel(); artworkLoading = null;
        artwork?.Dispose(); artwork = null;
        if (noisy != null) { UnregisterReceiver(noisy); noisy.Dispose(); }
        session?.Release(); session?.Dispose(); session = null;
        AppServices.Playback.Pause();
        base.OnDestroy();
    }
    private sealed class NoisyReceiver : BroadcastReceiver
    { public override void OnReceive(Context? context, Intent? intent) { if (intent?.Action == AudioManager.ActionAudioBecomingNoisy) AppServices.Playback.Pause(); } }
    private sealed class SessionControls : MediaSession.Callback
    {
        private static async void Run(Func<Task> action) { try { await action(); } catch { } }
        public override void OnPlay() { if (!AppServices.Playback.IsPlaying && !AppServices.Playback.IsLoading) Run(AppServices.Playback.ToggleAsync); }
        public override void OnPause() => AppServices.Playback.Pause();
        public override void OnSkipToNext() => Run(() => AppServices.Playback.NextAsync());
        public override void OnSkipToPrevious() => Run(AppServices.Playback.PreviousAsync);
        public override void OnSeekTo(long pos) => AppServices.Playback.Seek(TimeSpan.FromMilliseconds(pos));
        public override void OnStop() => AppServices.Playback.Pause();
    }
}
