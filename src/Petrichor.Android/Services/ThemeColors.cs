using Android.Content;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;
using Color = Microsoft.Maui.Graphics.Color;
namespace Petrichor.Mobile.Services;

public static class ThemeColors
{
    public static void Apply(Context? context = null)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app == null) return;
        context ??= MainActivity.Instance ?? (Context)global::Android.App.Application.Context;
        var dark = app.UserAppTheme == AppTheme.Dark || (app.UserAppTheme == AppTheme.Unspecified && app.RequestedTheme == AppTheme.Dark);
        var dynamic = OperatingSystem.IsAndroidVersionAtLeast(31) && Preferences.Get("dynamicColors", true);
        Color Get(string palette, int tone, string fallback)
        {
            if (dynamic) {
                var id = context.Resources!.GetIdentifier("system_" + palette + "_" + tone, "color", "android");
                if (id != 0) { var c = context.Resources.GetColor(id, context.Theme); return Color.FromRgba(c.R, c.G, c.B, c.A); }
            }
            return Color.FromArgb(fallback);
        }
        var values = new Dictionary<string, Color> {
            ["Primary"] = Get("accent1",dark?200:600,dark?"#FFB1C4":"#B91C42"),
            ["OnPrimary"] = Get("accent1",dark?800:0,dark?"#650027":"#FFFFFF"),
            ["PrimaryContainer"] = Get("accent1",dark?700:100,dark?"#8E113F":"#FFD9E2"),
            ["OnPrimaryContainer"] = Get("accent1",dark?100:900,dark?"#FFD9E2":"#3F0016"),
            ["Surface"] = Get("neutral1",dark?900:10,dark?"#191114":"#FFFBFC"),
            ["SurfaceContainer"] = Get("neutral1",dark?800:50,dark?"#251D21":"#F7ECEF"),
            ["SurfaceHigh"] = Get("neutral2",dark?800:100,dark?"#33282E":"#F0E3E7"),
            ["OnSurface"] = Get("neutral1",dark?50:900,dark?"#F0DEE3":"#23191C"),
            ["Muted"] = Get("neutral2",dark?200:700,dark?"#D0BDC5":"#76666C"),
            ["Outline"] = Get("neutral2",dark?600:200,dark?"#51434A":"#DAC2CA"),
            ["Error"] = Color.FromArgb(dark?"#FFB4AB":"#BA1A1A")
        };
        // Separate low-emphasis supporting content from selected/action containers.
        values["SurfaceContainerLow"]=Blend(values["Surface"],values["SurfaceContainer"],.45f);
        values["SecondaryContainer"]=Get("accent2",dark?700:100,dark?"#4A4458":"#E8DEF8");
        values["OnSecondaryContainer"]=Get("accent2",dark?100:900,dark?"#E8DEF8":"#1D192B");
        values["OutlineVariant"]=Blend(values["Outline"],values["Surface"],.5f);
        static Color Blend(Color a,Color b,float t)=>new(a.Red+(b.Red-a.Red)*t,a.Green+(b.Green-a.Green)*t,a.Blue+(b.Blue-a.Blue)*t);
        // Resume and theme callbacks can run repeatedly without a palette
        // change. Reassigning identical resources repaints every bound control.
        foreach (var pair in values)if(!app.Resources.TryGetValue(pair.Key,out var existing)||existing is not Color color||!color.Equals(pair.Value))app.Resources[pair.Key] = pair.Value;
        if(context is global::Android.App.Activity activity && activity.Window?.DecorView is {} root)
        {
            var surface=values[Shell.Current?.CurrentPage is Petrichor.Mobile.Pages.PlayerPage?"SurfaceContainer":"Surface"].ToPlatform();
            activity.Window.SetBackgroundDrawable(new global::Android.Graphics.Drawables.ColorDrawable(surface));
            root.SetBackgroundColor(surface);
            // Android 15 draws transparent system bars over the app window.
            // Earlier versions require the explicit matching bar background.
            if(!OperatingSystem.IsAndroidVersionAtLeast(35))activity.Window.SetStatusBarColor(surface);
            activity.Window.SetNavigationBarColor(values["SurfaceContainer"].ToPlatform());
            if(OperatingSystem.IsAndroidVersionAtLeast(29)){activity.Window.StatusBarContrastEnforced=false;activity.Window.NavigationBarContrastEnforced=false;}
            var bars=new AndroidX.Core.View.WindowInsetsControllerCompat(activity.Window,root);
            bars.AppearanceLightStatusBars=!dark;bars.AppearanceLightNavigationBars=!dark;
            ApplyNavigation(root);
        }
        void ApplyNavigation(global::Android.Views.View view)
        {
            if(view is Google.Android.Material.BottomNavigation.BottomNavigationView navigation)
            {
                var density=navigation.Resources!.DisplayMetrics!.Density;
                navigation.ItemIconSize=(int)Math.Round(26*density);
                navigation.LabelVisibilityMode=Google.Android.Material.Navigation.NavigationBarView.LabelVisibilityUnlabeled;
                var c=values["PrimaryContainer"];
                navigation.ItemActiveIndicatorColor=global::Android.Content.Res.ColorStateList.ValueOf(global::Android.Graphics.Color.Argb(255,(int)(c.Red*255),(int)(c.Green*255),(int)(c.Blue*255)));
            }
            if(view is global::Android.Views.ViewGroup group)for(int i=0;i<group.ChildCount;i++)if(group.GetChildAt(i) is {} child)ApplyNavigation(child);
        }
    }
}
