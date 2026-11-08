using Android.Content.Res;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Navigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;
using Microsoft.Maui.Platform;
using AView=Android.Views.View;
namespace Petrichor.Mobile.UI;

// Shell extension points: Material 3 navigation bar, shared-axis page transitions and the persistent
// mini player, which lives between the page area and the navigation bar instead of inside every page.
internal sealed class PetrichorShellRenderer:ShellRenderer
{
    protected override IShellItemRenderer CreateShellItemRenderer(ShellItem shellItem)=>new PetrichorShellItemRenderer(this);
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem)=>new NavigationBarAppearance(this,shellItem);
}

internal sealed class PetrichorShellItemRenderer(IShellContext context):ShellItemRenderer(context)
{
    public override AView OnCreateView(LayoutInflater inflater,ViewGroup? container,Bundle? savedInstanceState)
    {
        var root=base.OnCreateView(inflater,container!,savedInstanceState!);
        if(ShellContext.Shell.Handler?.MauiContext is{} mauiContext)MiniPlayer.Host(root,ShellContext.Shell,mauiContext);
        return root;
    }
    protected override void SetupMenu(IMenu menu,int maxBottomItems,ShellItem shellItem)
    {
        base.SetupMenu(menu,maxBottomItems,shellItem);
        NavigationBar.ApplyIcons(menu,Context!);
    }
    protected override void SetupAnimation(ShellNavigationSource navSource,FragmentTransaction t,Page page)=>PageMotion.Configure(navSource,t,GetNavigationTarget());
    protected override void OnTabReselected(ShellSection shellSection)=>PageMotion.Reselect(shellSection);
    protected override void UpdateTabBarVisibility()
    {
        base.UpdateTabBarVisibility();
        NavigationBar.Track(this);
    }
    internal BottomNavigationView? Bar=>this.View is ViewGroup root?NavigationBar.Find(root):null;
}

internal sealed class NavigationBarAppearance(IShellContext context,ShellItem item):ShellBottomNavViewAppearanceTracker(context,item)
{
    public override void SetAppearance(BottomNavigationView bottomView,IShellAppearanceElement appearance)=>NavigationBar.Style(bottomView);
    public override void ResetAppearance(BottomNavigationView bottomView)=>NavigationBar.Style(bottomView);
}

internal static class NavigationBar
{
    private static readonly string[] destinations=["home","explore","library"];
    private static WeakReference<BottomNavigationView>? current;
    private static bool shellVisible=true,imeVisible;
    public static bool IsShown=>shellVisible&&!imeVisible;
    public static event Action? VisibilityChanged;

    public static BottomNavigationView? Find(ViewGroup group)
    {
        for(var i=0;i<group.ChildCount;i++){var child=group.GetChildAt(i);if(child is BottomNavigationView bar)return bar;if(child is ViewGroup inner&&Find(inner) is{} found)return found;}
        return null;
    }
    public static void Track(PetrichorShellItemRenderer renderer)
    {
        if(renderer.Bar is not{} bar)return;
        current=new(bar);shellVisible=bar.Visibility==ViewStates.Visible;
        if(imeVisible)bar.Visibility=ViewStates.Gone;
        VisibilityChanged?.Invoke();
    }
    // Android resizes the window for the keyboard; a navigation bar riding on top of it hides content.
    public static void SetImeVisible(bool visible)
    {
        if(imeVisible==visible)return;imeVisible=visible;
        if(current?.TryGetTarget(out var bar)==true&&bar.Handle!=IntPtr.Zero&&shellVisible)bar.Visibility=visible?ViewStates.Gone:ViewStates.Visible;
        VisibilityChanged?.Invoke();
    }
    public static void Style(BottomNavigationView bar)
    {
        current=new(bar);
        var density=bar.Resources!.DisplayMetrics!.Density;
        static ColorStateList Checked(Microsoft.Maui.Graphics.Color on,Microsoft.Maui.Graphics.Color off)=>new([[global::Android.Resource.Attribute.StateChecked],[]],[on.ToPlatform().ToArgb(),off.ToPlatform().ToArgb()]);
        bar.LabelVisibilityMode=NavigationBarView.LabelVisibilityLabeled;
        bar.ItemIconSize=(int)Math.Round(24*density);
        bar.ItemActiveIndicatorColor=ColorStateList.ValueOf(Ui.Color("SecondaryContainer").ToPlatform());
        bar.ItemIconTintList=Checked(Ui.Color("OnSecondaryContainer"),Ui.Color("OnSurfaceVariant"));
        bar.ItemTextColor=Checked(Ui.Color("OnSurface"),Ui.Color("OnSurfaceVariant"));
        bar.ItemRippleColor=ColorStateList.ValueOf(Ui.Color("OnSurface").WithAlpha(Md.PressedState).ToPlatform());
        bar.SetBackgroundColor(Ui.Color("SurfaceContainer").ToPlatform());
        ApplyIcons(bar.Menu,bar.Context!);
    }
    public static void Restyle(){if(current?.TryGetTarget(out var bar)==true&&bar.Handle!=IntPtr.Zero)Style(bar);}
    public static void ApplyIcons(IMenu menu,global::Android.Content.Context context)
    {
        for(var i=0;i<menu.Size()&&i<destinations.Length;i++)
        {
            var item=menu.GetItem(i);if(item==null||item.ItemId!=i)continue;
            // Selected destinations use the filled glyph, others the outlined one (Material 3 navigation bar).
            var icon=new global::Android.Graphics.Drawables.StateListDrawable();
            icon.AddState([global::Android.Resource.Attribute.StateChecked],new GlyphDrawable(context,destinations[i],true));
            icon.AddState([],new GlyphDrawable(context,destinations[i],false));
            item.SetIcon(icon);
        }
    }
}
