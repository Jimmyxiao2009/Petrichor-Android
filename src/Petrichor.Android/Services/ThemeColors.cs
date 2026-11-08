using Android.Content;
using Android.Content.Res;
using Google.Android.Material.Color;
using Microsoft.Maui.Platform;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.UI;
using Color = Microsoft.Maui.Graphics.Color;
namespace Petrichor.Mobile.Services;

// The activity theme is the single source of the palette: dynamic or static overlays are applied to it,
// then every Material 3 color role is read back for MAUI. Native Material views and MAUI views therefore
// always agree, including the API 31-33 surface-container tones that Material derives from system colors.
public static class ThemeColors
{
    private static readonly (string Key,string Attr,string Static)[] roles=[
        ("Primary","colorPrimary","primary"),("OnPrimary","colorOnPrimary","on_primary"),
        ("PrimaryContainer","colorPrimaryContainer","primary_container"),("OnPrimaryContainer","colorOnPrimaryContainer","on_primary_container"),
        ("Secondary","colorSecondary","secondary"),("OnSecondary","colorOnSecondary","on_secondary"),
        ("SecondaryContainer","colorSecondaryContainer","secondary_container"),("OnSecondaryContainer","colorOnSecondaryContainer","on_secondary_container"),
        ("Tertiary","colorTertiary","tertiary"),("OnTertiary","colorOnTertiary","on_tertiary"),
        ("TertiaryContainer","colorTertiaryContainer","tertiary_container"),("OnTertiaryContainer","colorOnTertiaryContainer","on_tertiary_container"),
        ("Error","colorError","error"),("OnError","colorOnError","on_error"),
        ("ErrorContainer","colorErrorContainer","error_container"),("OnErrorContainer","colorOnErrorContainer","on_error_container"),
        ("Surface","colorSurface","surface"),("OnSurface","colorOnSurface","on_surface"),
        ("SurfaceVariant","colorSurfaceVariant","surface_variant"),("OnSurfaceVariant","colorOnSurfaceVariant","on_surface_variant"),
        ("SurfaceDim","colorSurfaceDim","surface_dim"),("SurfaceBright","colorSurfaceBright","surface_bright"),
        ("SurfaceContainerLowest","colorSurfaceContainerLowest","surface_container_lowest"),("SurfaceContainerLow","colorSurfaceContainerLow","surface_container_low"),
        ("SurfaceContainer","colorSurfaceContainer","surface_container"),("SurfaceContainerHigh","colorSurfaceContainerHigh","surface_container_high"),
        ("SurfaceContainerHighest","colorSurfaceContainerHighest","surface_container_highest"),
        ("Outline","colorOutline","outline"),("OutlineVariant","colorOutlineVariant","outline_variant"),
        ("InverseSurface","colorSurfaceInverse","inverse_surface"),("InverseOnSurface","colorOnSurfaceInverse","inverse_on_surface"),
        ("InversePrimary","colorPrimaryInverse","inverse_primary")];
    public static event Action? Changed;
    public static bool IsDark { get; private set; }
    public static bool DynamicAvailable => OperatingSystem.IsAndroidVersionAtLeast(31) && DynamicColors.IsDynamicColorAvailable;
    private static bool UseDynamic => DynamicAvailable && Preferences.Get("dynamicColors", true);

    public static bool ResolveDark(Context context)
    {
        var mode = Microsoft.Maui.Controls.Application.Current?.UserAppTheme ?? (AppTheme)Preferences.Get("theme", 0);
        if (mode == AppTheme.Dark) return true;
        if (mode == AppTheme.Light) return false;
        return (context.Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
    }
    public static void ApplyOverlay(Context context, Resources.Theme? theme)
    {
        if (theme == null) return;
        var dark = ResolveDark(context);
        var style = UseDynamic
            ? (dark ? Resource.Style.ThemeOverlay_Material3_DynamicColors_Dark : Resource.Style.ThemeOverlay_Material3_DynamicColors_Light)
            : (dark ? Resource.Style.ThemeOverlay_Petrichor_Dark : Resource.Style.ThemeOverlay_Petrichor_Light);
        theme.ApplyStyle(style, true);
    }
    public static void Apply(Context? context = null)
    {
        var app = Microsoft.Maui.Controls.Application.Current;
        if (app == null) return;
        context ??= MainActivity.Instance ?? (Context)global::Android.App.Application.Context;
        var dark = IsDark = ResolveDark(context);
        var activity = context as global::Android.App.Activity;
        if (activity != null) ApplyOverlay(activity, activity.Theme);
        var changed = false;
        void Set(string key, Color value)
        {
            // Resume and focus callbacks run repeatedly; reassigning equal values repaints every bound view.
            if (app.Resources.TryGetValue(key, out var existing) && existing is Color color && color.Equals(value)) return;
            app.Resources[key] = value; changed = true;
        }
        foreach (var role in roles) Set(role.Key, Read(context, activity, role.Attr, role.Static, dark));
        Set("Scrim", Colors.Black);
        if (activity != null) { UpdateSystemBars(activity); NavigationBar.Restyle(); }
        if (changed) Changed?.Invoke();
    }
    private static Color Read(Context context, global::Android.App.Activity? activity, string attr, string name, bool dark)
    {
        var id = context.Resources!.GetIdentifier("pt_" + (dark ? "dark_" : "light_") + name, "color", context.PackageName);
        var fallback = id == 0 ? Colors.Magenta : Color.FromInt(context.Resources.GetColor(id, null).ToArgb());
        if (activity == null) return fallback;
        var attribute = context.Resources.GetIdentifier(attr, "attr", context.PackageName);
        return attribute == 0 ? fallback : Color.FromInt(MaterialColors.GetColor(activity, attribute, fallback.ToInt()));
    }
    // Before Android 15 the bars are opaque; afterwards they are transparent over the edge-to-edge window.
    public static void UpdateSystemBars(global::Android.App.Activity? activity = null, Color? statusBar = null)
    {
        activity ??= MainActivity.Instance;
        if (activity?.Window is not { } window || window.DecorView is not { } root || Microsoft.Maui.Controls.Application.Current == null) return;
        var page = Shell.Current?.CurrentPage as BasePage;
        var background = Ui.Color(page?.BackgroundKey ?? "Surface").ToPlatform();
        window.SetBackgroundDrawable(new global::Android.Graphics.Drawables.ColorDrawable(background));
        root.SetBackgroundColor(background);
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetStatusBarColor((statusBar ?? page?.AppBarColor ?? Ui.Color("Surface")).ToPlatform());
            window.SetNavigationBarColor(Ui.Color(NavigationBar.IsShown ? "SurfaceContainer" : page?.BottomKey ?? "Surface").ToPlatform());
        }
        if (OperatingSystem.IsAndroidVersionAtLeast(29)) { window.StatusBarContrastEnforced = false; window.NavigationBarContrastEnforced = false; }
        var bars = new AndroidX.Core.View.WindowInsetsControllerCompat(window, root);
        bars.AppearanceLightStatusBars = !IsDark; bars.AppearanceLightNavigationBars = !IsDark;
    }
}
