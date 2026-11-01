using Android.App;
using Android.Content.PM;
using Android.OS;
using Google.Android.Material.Color;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile;
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    public static MainActivity? Instance { get; private set; }
    protected override void OnCreate(Bundle? state)
    {
        Instance = this;
        base.OnCreate(state);
        VolumeControlStream=Android.Media.Stream.Music;
        Window?.SetSoftInputMode(Android.Views.SoftInput.AdjustResize);
        if (Preferences.Get("dynamicColors", true)) DynamicColors.ApplyToActivityIfAvailable(this);
        ThemeColors.Apply(this);
    }
#if DEBUG
    protected override void OnNewIntent(Android.Content.Intent? intent){base.OnNewIntent(intent);if(intent?.GetStringExtra("qa-page") is{} page)_=UI.ReviewRoutes.OpenAsync(page,intent.GetLongExtra("qa-id",0));}
#endif
    protected override void OnResume() { base.OnResume(); AppServices.Playback.SetUiActive(true); ThemeColors.Apply(this); }
    protected override void OnPause() { AppServices.Playback.SetUiActive(false); base.OnPause(); }
    public override void OnWindowFocusChanged(bool hasFocus){base.OnWindowFocusChanged(hasFocus);if(hasFocus)ThemeColors.Apply(this);}
    protected override void OnActivityResult(int requestCode,Result resultCode,Android.Content.Intent? data)
    {base.OnActivityResult(requestCode,resultCode,data);if(requestCode==DownloadFolder.RequestCode)DownloadFolder.Complete(resultCode,data);}
    public override void OnConfigurationChanged(Android.Content.Res.Configuration config)
    { base.OnConfigurationChanged(config); ThemeColors.Apply(this); }
}
