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
        var toggle=new Style(typeof(Switch));toggle.Setters.AddDynamicResource(Switch.OnColorProperty,"Primary");toggle.Setters.AddDynamicResource(Switch.ThumbColorProperty,"OnPrimary");Resources.Add(toggle);
        var check=new Style(typeof(CheckBox));check.Setters.AddDynamicResource(CheckBox.ColorProperty,"Primary");Resources.Add(check);
        ThemeColors.Apply();
        UserAppTheme = (AppTheme)Preferences.Get("theme", 0);
        RequestedThemeChanged += (_, _) => ThemeColors.Apply();
        void InputStyle(Type type,BindableProperty text,BindableProperty hint)
        {
            var style=new Style(type);style.Setters.AddDynamicResource(text,"OnSurface");style.Setters.AddDynamicResource(hint,"Muted");if(type==typeof(Entry))style.Setters.Add(new Setter{Property=Entry.VerticalTextAlignmentProperty,Value=TextAlignment.Center});Resources.Add(style);
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
        this.SetDynamicResource(Shell.TabBarForegroundColorProperty,"Primary");
        this.SetDynamicResource(Shell.TabBarUnselectedColorProperty,"Muted");
        var tabs = new TabBar {Route="main"};
        Add("discover","主页","home",()=>new DiscoverPage());
        Add("podcasts","探索","explore",()=>new ExplorePage());
        Add("library","我的","library",()=>new LibraryPage());
        Items.Add(tabs);
        Page? lastRoot = null;
        var lastTab = 0;
        Navigated += (_, _) => {
            var page = CurrentPage;
            if (page == null || Navigation.NavigationStack.Count > 1) return;
            var index = tabs.Items.IndexOf(tabs.CurrentItem);
            if (!ReferenceEquals(lastRoot, page)) PageMotion.ShowTab(page, index < lastTab ? -1 : 1);
            lastRoot = page; lastTab = index;
        };
        void Add(string route,string title,string icon,Func<ContentPage> factory)
        {
            var tab = new Tab {Route=route+"Tab",Title=title,Icon=Ui.IconSource(icon)};
            tab.Items.Add(new ShellContent {Route=route,ContentTemplate=new DataTemplate(factory)});
            tabs.Items.Add(tab);
        }
    }
}
