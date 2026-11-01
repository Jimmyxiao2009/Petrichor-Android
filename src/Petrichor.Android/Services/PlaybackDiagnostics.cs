using Android.Content;
using Android.OS;
namespace Petrichor.Mobile.Services;
internal static class PlaybackDiagnostics
{
    private static readonly RollingDiagnosticLog log = new(Path.Combine(FileSystem.AppDataDirectory, "diagnostics"));
    private static bool initialized;
    public static bool Enabled { get; private set; } = Preferences.Get("playbackDiagnostics", true);
    public static bool PowerSaveMode {
        get {
            try { return ((PowerManager)global::Android.App.Application.Context.GetSystemService(Context.PowerService)!).IsPowerSaveMode; }
            catch { return false; }
        }
    }
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        Connectivity.Current.ConnectivityChanged += (_, e) => Write("network", "access=" + e.NetworkAccess + " profiles=" + string.Join(",", e.ConnectionProfiles));
        Write("app.start", Summary());
    }
    public static void SetEnabled(bool enabled)
    {
        if (Enabled) Write("logging", "enabled=" + enabled);
        Enabled = enabled; Preferences.Set("playbackDiagnostics", enabled);
        if (enabled) Write("logging", "enabled=true");
    }
    public static void Write(string name, string details = "") { if (Enabled) log.Write(name, details); }
    public static string Summary() =>
        "Petrichor " + AppInfo.Current.VersionString + " (" + AppInfo.Current.BuildString + ")" +
        "\nAndroid " + Build.VERSION.Release + " / API " + (int)Build.VERSION.SdkInt +
        "\nDevice " + Build.Manufacturer + " " + Build.Model +
        "\nSystem power saver: " + PowerSaveMode +
        "\nNetwork: " + Connectivity.Current.NetworkAccess + " / " + string.Join(",", Connectivity.Current.ConnectionProfiles);
    public static Task<string> ReadAsync() => log.ReadAsync();
    public static Task ClearAsync() => log.ClearAsync();
    public static async Task<string> ExportAsync()
    {
        var contents = Summary() + "\nExported: " + DateTimeOffset.Now.ToString("O") + "\n\n" + await log.ReadAsync();
        var path = Path.Combine(FileSystem.CacheDirectory, "Petrichor-playback-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
        await File.WriteAllTextAsync(path, contents); return path;
    }
}