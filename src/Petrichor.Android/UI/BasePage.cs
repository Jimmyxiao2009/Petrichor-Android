using AndroidX.CoordinatorLayout.Widget;
using Google.Android.Material.Snackbar;
using Microsoft.Maui.Platform;
using Petrichor.Core;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
using AView=Android.Views.View;
namespace Petrichor.Mobile.UI;

public abstract class BasePage : ContentPage
{
    protected readonly ContentView Body = new();
    private readonly LinearProgress progress = new(){VerticalOptions=LayoutOptions.End,Margin=new Thickness(-4,0)};
    private readonly Grid layout,header,titleHost;
    private readonly HorizontalStackLayout actions=new(){Spacing=0,VerticalOptions=LayoutOptions.Center};
    private readonly Label headerCaption;
    private readonly IconButton? headerBack;
    private Grid? compactHeader;
    private Image? compactCover;
    private double compactThreshold=180;
    private ScrollView? primaryScroll;
    private bool appBarScrolled;
    private double appBarProgress;
    internal bool ShowsChrome {get;}
    internal virtual string BackgroundKey=>"Surface";
    internal virtual string BottomKey=>BackgroundKey;
    internal Color? AppBarColor {get;private set;}
    protected virtual bool TintAppBarOnScroll=>true;
    protected virtual bool HasToolbarMenu=>false;
    protected void SetPlayerHeader(PlayerPane pane){
        headerCaption.Text=pane==PlayerPane.Functions?"播放功能":pane==PlayerPane.Queue?"正在播放":"";headerCaption.IsVisible=pane!=PlayerPane.Player;
        if(headerBack!=null){headerBack.SetIcon(pane==PlayerPane.Queue?"up":"down",false,true);SemanticProperties.SetDescription(headerBack,pane==PlayerPane.Player?"返回":"返回播放");}
    }
    protected void ConfigureScrollHeader(string cover,string name,double threshold=180){
        compactThreshold=threshold;
        if(compactHeader==null){
            compactCover=Ui.Cover(null,32,Md.ShapeSmall);
            var label=Ui.Line("",TypeRole.TitleMedium);
            compactHeader=new Grid{ColumnDefinitions=[new(32),new(GridLength.Star)],ColumnSpacing=12,Margin=new Thickness(4,0,0,0),VerticalOptions=LayoutOptions.Center,InputTransparent=true,IsVisible=false};compactHeader.Add(compactCover);compactHeader.Add(label,1);titleHost.Add(compactHeader);
        }
        compactCover!.Source=Ui.Art(cover,200);((Label)compactHeader.Children[1]).Text=name;SemanticProperties.SetDescription(compactHeader,"歌单："+name);UpdateScrollHeader(0);
    }
    private bool compactSuspended;
    protected void SuspendScrollHeader(bool suspend){compactSuspended=suspend;if(suspend&&compactHeader!=null){compactHeader.IsVisible=false;headerCaption.IsVisible=true;headerCaption.Opacity=1;}}
    protected void UpdateScrollHeader(double offset){
        SetScrolled(offset>1);
        if(compactHeader==null||compactSuspended)return;var p=Math.Clamp((offset-compactThreshold)/64,0,1);
        compactHeader.IsVisible=p>0;compactHeader.Opacity=p;compactHeader.TranslationY=8*(1-p);compactCover!.Scale=.85+.15*p;
        headerCaption.IsVisible=p<1;headerCaption.Opacity=1-p;
    }
    protected void SetHeaderVisible(bool visible)=>header.IsVisible=visible;
    private CancellationTokenSource life = new();
    private bool loaded;
    private bool visible,cacheDirty;
    private long accountId;
    private long cacheVersion;
    private DateTimeOffset lastLoaded;
    private View? initialSkeleton;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,byte> dependencies=new();
    protected virtual bool RefreshWhenCacheChanges=>false;
    protected virtual void LoadingChanged(bool loading){}
    protected virtual void AccountInvalidated(){}
    protected bool IsRevalidation{get;private set;}
    private readonly KeyboardAvoidance keyboard;
    private int workers;
    protected CancellationToken Token => life.Token;
    protected virtual bool RefreshOnAppear => false;
    // showMini=false marks a full-screen page: the navigation bar and mini player are hidden.
    protected BasePage(string title,bool root=false,bool showMini=true)
    {
        Title=title;Shell.SetNavBarIsVisible(this,false);
        ShowsChrome=showMini;Shell.SetTabBarIsVisible(this,showMini);
        this.SetDynamicResource(BackgroundColorProperty,BackgroundKey);
        var player=this is PlayerPage;
        // Material 3 small top app bar: 64dp, leading navigation icon, Title Large, trailing actions.
        header=new Grid{ColumnDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)],HeightRequest=64,Padding=new Thickness(4,0)};
        if(!root){headerBack=new IconButton(player?"down":"back",GoBack,"返回",foreground:"OnSurface");header.Add(headerBack);}
        var caption=headerCaption=Ui.Line(title,TypeRole.TitleLarge);caption.Margin=new Thickness(root?12:4,0,0,0);caption.IsVisible=!player;
        titleHost=new Grid{VerticalOptions=LayoutOptions.Center};titleHost.Add(caption);header.Add(titleHost,1);
        caption.SetBinding(Label.TextProperty,new Binding(nameof(Title),source:this));
        if(root){actions.Add(Ui.Icon("search",()=>Open(new SearchPage()),"搜索",foreground:"OnSurface"));actions.Add(Ui.Icon("settings",()=>Open(new SettingsPage()),"设置"));}
        else if(HasToolbarMenu)actions.Add(Ui.Icon("more",()=>Run(OnToolbar,false),"更多操作"));
        header.Add(actions,2);
        header.Add(progress);Grid.SetColumnSpan(progress,3);
        // Shell's visible navigation bar already consumes the bottom system inset.
        layout=new Grid{SafeAreaEdges=new SafeAreaEdges(SafeAreaRegions.Container,SafeAreaRegions.Container,SafeAreaRegions.Container,showMini?SafeAreaRegions.None:SafeAreaRegions.Container),RowDefinitions=[new(GridLength.Auto),new(GridLength.Star)]};
        layout.SetDynamicResource(BackgroundColorProperty,BackgroundKey);Body.SetDynamicResource(BackgroundColorProperty,BackgroundKey);
        layout.Add(header);layout.Add(Body,0,1);
        Content=layout;keyboard=new(this,Body);
    }
    // The top app bar, and the status bar behind it, take the surface-container color once content
    // scrolls under them (Material 3 "on scroll" app bar).
    protected void SetScrolled(bool scrolled)
    {
        if(!TintAppBarOnScroll||scrolled==appBarScrolled)return;appBarScrolled=scrolled;
        var from=appBarProgress;var to=scrolled?1d:0d;this.AbortAnimation("AppBar");
        if(!PageMotion.Enabled){ApplyAppBar(to);return;}
        new Animation(t=>ApplyAppBar(from+(to-from)*t)).Commit(this,"AppBar",16,Md.Short4,Md.Standard);
    }
    private void ApplyAppBar(double t)
    {
        appBarProgress=t;
        if(t<=0){AppBarColor=null;layout.SetDynamicResource(BackgroundColorProperty,BackgroundKey);}
        else{var a=Ui.Color(BackgroundKey);var b=Ui.Color("SurfaceContainer");AppBarColor=new Color(a.Red+(b.Red-a.Red)*(float)t,a.Green+(b.Green-a.Green)*(float)t,a.Blue+(b.Blue-a.Blue)*(float)t);layout.BackgroundColor=AppBarColor;}
        if(visible&&!OperatingSystem.IsAndroidVersionAtLeast(35))MainActivity.Instance?.Window?.SetStatusBarColor((AppBarColor??Ui.Color(BackgroundKey)).ToPlatform());
    }
    private void ThemeChanged(){if(appBarProgress>0)ApplyAppBar(appBarProgress);}
    // A page revealed by a fragment transition can keep the padding it had while it was covered,
    // leaving its header under the status bar. Ask for insets as soon as the view exists, and again
    // after the transition settles; asking once sometimes lands before the reveal.
    private void ApplyInsets()
    {
        Dispatcher.Dispatch(()=>{
            if(Handler?.PlatformView is not AView native)return;
            native.Post(()=>AndroidX.Core.View.ViewCompat.RequestApplyInsets(native));
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(420),()=>{
                if(visible&&Handler?.PlatformView is AView current)AndroidX.Core.View.ViewCompat.RequestApplyInsets(current);
            });
        });
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();life.Dispose();life=new();visible=true;
        AppServices.Api.Cache.Changed+=CacheChanged;AppServices.AccountChanged+=AccountChanged;ThemeColors.Changed+=ThemeChanged;
        keyboard.Attach();ThemeColors.UpdateSystemBars();MiniPlayer.Refresh();
        ApplyInsets();
        var activation=life.Token;
        await Run(async()=>{
            if(!loaded){if(Body.Content==null)Body.Content=initialSkeleton=LoadingSkeleton.Rows(Md.Margin);LoadingChanged(true);}
            // Give Android a frame to draw the page before disk work or constructing rows.
            await Task.Delay(32,activation);
            await AppServices.InitializeAsync();
            activation.ThrowIfCancellationRequested();
            await PageMotion.WaitForEntryAsync(this,activation);
            if(accountId!=(AppServices.Api.CurrentUser?.Id??0)){if(loaded)AccountInvalidated();loaded=false;accountId=AppServices.Api.CurrentUser?.Id??0;}
            cacheDirty|=AppServices.Api.Cache.HasChanged(dependencies.Keys,cacheVersion)||DateTimeOffset.UtcNow-lastLoaded>TimeSpan.FromMinutes(5);
            if(!loaded||RefreshOnAppear||cacheDirty){await LoadContentAsync();activation.ThrowIfCancellationRequested();loaded=true;}
        });
    }
    private async Task LoadContentAsync()
    {
        cacheDirty=false;IsRevalidation=loaded;cacheVersion=AppServices.Api.Cache.Version;
        try{using(AppServices.Api.Cache.ObserveReads(key=>dependencies.TryAdd(key,0)))await LoadAsync();accountId=AppServices.Api.CurrentUser?.Id??0;lastLoaded=DateTimeOffset.UtcNow;}
        finally{LoadingChanged(false);IsRevalidation=false;}
    }
    private void CacheChanged(string key)
    {
        if(key!="*"&&!dependencies.ContainsKey(key))return;
        MainThread.BeginInvokeOnMainThread(async()=>{cacheDirty=true;if(visible&&loaded&&workers==0&&RefreshWhenCacheChanges)await Run(LoadContentAsync,false);});
    }
    private async void AccountChanged()
    {
        var id=AppServices.Api.CurrentUser?.Id??0;if(id==accountId)return;
        accountId=id;loaded=false;cacheDirty=true;dependencies.Clear();
        // An account switch must immediately remove the previous account's content.
        AccountInvalidated();
        if(visible)await Run(async()=>{await LoadContentAsync();loaded=true;});
    }
    protected override void OnDisappearing(){visible=false;life.Cancel();AppServices.Api.Cache.Changed-=CacheChanged;AppServices.AccountChanged-=AccountChanged;ThemeColors.Changed-=ThemeChanged;keyboard.Detach();MiniPlayer.Suppress(this,false);base.OnDisappearing();}
    protected virtual Task LoadAsync()=>Task.CompletedTask;
    protected virtual Task OnToolbar()=>Task.CompletedTask;
    protected virtual Task GoBack()=>Ui.Pop();
    internal virtual void ScrollToTop(){if(primaryScroll!=null)_=primaryScroll.ScrollToAsync(0,0,PageMotion.Enabled);}
    protected override bool OnBackButtonPressed()
    {
        if(Navigation.NavigationStack.Count<=1)return base.OnBackButtonPressed();
        _=Run(GoBack,false);return true;
    }
    protected void ShowMini(bool visible)=>MiniPlayer.Suppress(this,!visible);
    protected ScrollView Scroll(View view)
    {
        var scroll=new ScrollView{Content=view,VerticalScrollBarVisibility=ScrollBarVisibility.Never};
        scroll.Scrolled+=(_,e)=>SetScrolled(e.ScrollY>1);primaryScroll??=scroll;return scroll;
    }
    protected static VerticalStackLayout Stack()=>new(){Padding=new Thickness(Md.Margin,8,Md.Margin,24),Spacing=0};
    public async Task Run(Func<Task> action,bool showBusy=true)
    {
        if(showBusy){workers++;progress.IsRunning=true;}
        try {await action();}
        catch(OperationCanceledException){}
        catch(Exception e){ShowMessage(Ui.ErrorText(e),e is MusicApiException api&&api.RequiresLogin);if(Body.Content==null||ReferenceEquals(Body.Content,initialSkeleton))Body.Content=Scroll(Ui.Empty("暂时没连上","请检查网络连接，然后再试一次。\n已下载的音乐仍然可以播放。",()=>Run(async()=>{await AppServices.InitializeAsync();await LoadAsync();loaded=true;}),"offline"));}
        finally{if(showBusy)workers--;if(workers==0){progress.IsRunning=false;LoadingChanged(false);}}
    }
    // Brief messages use a Material 3 snackbar at the bottom of the page area, above the mini player.
    public void ShowMessage(string text,bool login=false)
    {
        var page=Shell.Current?.CurrentPage as BasePage??this;
        if(page.Handler?.PlatformView is not AView target||!target.IsAttachedToWindow)return;
        try
        {
            var bar=Snackbar.Make(target,text,login?Snackbar.LengthLong:Snackbar.LengthShort);
            bar.SetBackgroundTint(Ui.Color("InverseSurface").ToPlatform().ToArgb());bar.SetTextColor(Ui.Color("InverseOnSurface").ToPlatform().ToArgb());bar.SetActionTextColor(Ui.Color("InversePrimary").ToPlatform().ToArgb());
            if(login)bar.SetAction("登录",view=>{_=page.Open(new LoginPage());});
            var parent=target.Parent;var coordinated=false;while(parent!=null){if(parent is CoordinatorLayout){coordinated=true;break;}parent=parent.Parent;}
            // Outside a coordinator the snackbar would sit on the window bottom, over the navigation bar.
            if(!coordinated&&MiniPlayer.Shared is{IsVisible:true} mini&&mini.Handler?.PlatformView is AView anchor)bar.SetAnchorView(anchor);
            bar.Show();
        }
        catch(Java.Lang.IllegalArgumentException){}
    }
    public Task Open(Page page)=>Run(()=>Ui.Push(page),false);
    public Task Play(IEnumerable<Track> tracks,Track? start=null)=>Run(()=>AppServices.Playback.PlayAsync(tracks,start));
    public Task Alert(string message)=>Dialogs.Alert("Petrichor",message);
}
