using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
using Petrichor.Mobile.UI;
namespace Petrichor.Mobile;

public sealed class App : Application
{
    public App()
    {
        Resources["Transparent"]=Colors.Transparent;
        InputStyle(typeof(Entry),Entry.TextColorProperty,Entry.PlaceholderColorProperty);
        InputStyle(typeof(NativeEntry),NativeEntry.TextColorProperty,NativeEntry.PlaceholderColorProperty);
        InputStyle(typeof(Editor),Editor.TextColorProperty,Editor.PlaceholderColorProperty);
        InputStyle(typeof(Picker),Picker.TextColorProperty,Picker.TitleColorProperty);
        // Switches, check boxes and radio buttons keep the Material 3 theme tints from the activity
        // theme overlay; a single MAUI color would flatten their selected/unselected states.
        UserAppTheme = (AppTheme)Preferences.Get("theme", 0);
        ThemeColors.Apply();
        RequestedThemeChanged += (_, _) => ThemeColors.Apply();
        void InputStyle(Type type,BindableProperty text,BindableProperty hint)
        {
            var style=new Style(type);style.Setters.AddDynamicResource(text,"OnSurface");style.Setters.AddDynamicResource(hint,"OnSurfaceVariant");if(type==typeof(Entry))style.Setters.Add(new Setter{Property=Entry.VerticalTextAlignmentProperty,Value=TextAlignment.Center});Resources.Add(style);
        }
    }
    protected override Window CreateWindow(IActivationState? activationState) { ThemeColors.Apply(); return new(new AppShell()); }
}
public sealed class AppShell : Shell
{
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;
        this.SetDynamicResource(BackgroundColorProperty,"Surface");

        SetNavBarIsVisible(this, false);
        this.SetDynamicResource(Shell.TabBarBackgroundColorProperty,"SurfaceContainer");
        this.SetDynamicResource(Shell.TabBarForegroundColorProperty,"OnSecondaryContainer");
        this.SetDynamicResource(Shell.TabBarTitleColorProperty,"OnSurface");
        this.SetDynamicResource(Shell.TabBarUnselectedColorProperty,"OnSurfaceVariant");
        var tabs = new TabBar {Route="main"};
        Add("discover","主页",()=>new DiscoverPage());
        Add("podcasts","探索",()=>new ExplorePage());
        Add("library","我的",()=>new LibraryPage());
        Items.Add(tabs);
        Navigated += (_, e) => {
            if (e.Source == ShellNavigationSource.ShellSectionChanged) PageMotion.FadeThrough(CurrentPage);
            ThemeColors.UpdateSystemBars(); MiniPlayer.Refresh();
        };
        // Icons are drawn natively by NavigationBar so the selected destination can use the filled glyph.
        void Add(string route,string title,Func<ContentPage> factory)
        {
            var tab = new Tab {Route=route+"Tab",Title=title};
            tab.Items.Add(new ShellContent {Route=route,ContentTemplate=new DataTemplate(factory)});
            tabs.Items.Add(tab);
        }
    }
}
