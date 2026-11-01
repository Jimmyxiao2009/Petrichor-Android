using Petrichor.Core;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

public abstract class BasePage : ContentPage
{
    protected readonly ContentView Body = new();
    private readonly VerticalStackLayout notice = new() {Spacing=Ui.S(8),Padding=Ui.Inset(24,12),IsVisible=false};
    private readonly ActivityIndicator spinner = new() {HeightRequest=Ui.S(24),WidthRequest=Ui.S(24),IsVisible=false,InputTransparent=true,VerticalOptions=LayoutOptions.Center};
    private readonly MiniPlayer? mini;
    internal View? MiniCover=>mini?.ArtworkView;
    private readonly View headerView;
    private readonly Label headerCaption;
    private readonly Button? headerBack;
    private readonly Grid titleHost;
    private Grid? compactHeader;
    private Image? compactCover;
    private double compactThreshold=180;
    protected void SetPlayerHeader(PlayerPane pane){
        headerCaption.Text=pane==PlayerPane.Functions?"播放功能":pane==PlayerPane.Queue?"正在播放":"";headerCaption.IsVisible=pane!=PlayerPane.Player;
        if(headerBack!=null){headerBack.Text=Ui.Glyphs[pane==PlayerPane.Queue?"up":"down"];SemanticProperties.SetDescription(headerBack,pane==PlayerPane.Player?"返回":"返回播放");}
    }
    protected void ConfigureScrollHeader(string cover,string name,double threshold=180){
        compactThreshold=threshold;
        if(compactHeader==null){
            compactCover=Ui.Cover(null);compactCover.WidthRequest=32;compactCover.HeightRequest=32;compactCover.Clip=new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry(new CornerRadius(8),new Rect(0,0,32,32));
            var label=MooicLayout.Text("",16,bold:true);label.MaxLines=1;label.LineBreakMode=LineBreakMode.TailTruncation;
            compactHeader=new Grid{ColumnDefinitions=[new(32),new(GridLength.Star)],ColumnSpacing=10,VerticalOptions=LayoutOptions.Center,InputTransparent=true,IsVisible=false};compactHeader.Add(compactCover);compactHeader.Add(label,1);titleHost.Add(compactHeader);
        }
        compactCover!.Source=Ui.Art(cover,400);((Label)compactHeader.Children[1]).Text=name;SemanticProperties.SetDescription(compactHeader,"歌单："+name);UpdateScrollHeader(0);
    }
    protected void UpdateScrollHeader(double offset){
        if(compactHeader==null)return;var p=Math.Clamp((offset-compactThreshold)/64,0,1);
        compactHeader.IsVisible=p>0;compactHeader.Opacity=p;compactHeader.TranslationY=8*(1-p);compactHeader.TranslationX=10*(1-p);compactCover!.Scale=.82+.18*p;
        headerCaption.IsVisible=p<1;headerCaption.Opacity=1-p;
    }
    protected void SetHeaderVisible(bool visible)=>headerView.IsVisible=visible;
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
    protected BasePage(string title,bool root=false,bool showMini=true)
    {
        Title=title;Shell.SetNavBarIsVisible(this,false);
        Shell.SetTabBarIsVisible(this,root);
        this.SetDynamicResource(BackgroundColorProperty,"Surface");
        var player=title=="正在播放";
        var header=new Grid{ColumnDefinitions=[new(root?16:48),new(GridLength.Star),new(48),new(48)],HeightRequest=56,Padding=new Thickness(0,0,4,0)};
        if(!root){headerBack=MooicLayout.Icon(player?"down":"back",GoBack,"返回",foreground:"OnSurface");header.Add(headerBack);}
        var caption=headerCaption=MooicLayout.Text(title,22);caption.VerticalTextAlignment=TextAlignment.Center;caption.IsVisible=!player;titleHost=new Grid();titleHost.Add(caption);header.Add(titleHost,1);
        caption.SetBinding(Label.TextProperty,new Binding(nameof(Title),source:this));
        if(root){header.Add(MooicLayout.Icon("search",()=>Open(new SearchPage()),"搜索",foreground:"OnSurface"),2);header.Add(MooicLayout.Icon("settings",()=>Open(new SettingsPage()),"设置",foreground:"OnSurface"),3);}
        else if(!player&&title is not "设置" and not "登录")header.Add(MooicLayout.Icon("more",()=>Run(OnToolbar,false),"更多操作",foreground:"OnSurface"),3);
        spinner.SetDynamicResource(ActivityIndicator.ColorProperty,"Primary");
        spinner.HorizontalOptions=LayoutOptions.Center;
        if(!root)header.Add(spinner,2);
        notice.SetDynamicResource(BackgroundColorProperty,"SurfaceHigh");
        // Shell's visible TabBar already consumes the bottom system inset.
        var layout=new Grid{SafeAreaEdges=new SafeAreaEdges(SafeAreaRegions.Container,SafeAreaRegions.Container,SafeAreaRegions.Container,root?SafeAreaRegions.None:SafeAreaRegions.Container),RowDefinitions=[new(GridLength.Auto),new(0),new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)]};
        layout.Add(header);layout.Add(notice,0,2);layout.Add(Body,0,3);
        if(showMini){mini=new();layout.Add(mini,0,4);}
        if(player)this.SetDynamicResource(BackgroundColorProperty,"SurfaceContainer");
        headerView=header;Content=layout;keyboard=new(this,Body);
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();life.Dispose();life=new();visible=true;
        AppServices.Api.Cache.Changed+=CacheChanged;AppServices.AccountChanged+=AccountChanged;
        mini?.Attach();keyboard.Attach();
        Dispatcher.Dispatch(()=>{if(Handler?.PlatformView is global::Android.Views.View native)native.Post(()=>AndroidX.Core.View.ViewCompat.RequestApplyInsets(native));});
        var activation=life.Token;
        await Run(async()=>{
            if(!loaded){if(Body.Content==null)Body.Content=initialSkeleton=LoadingSkeleton.Rows();LoadingChanged(true);}
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
    protected override void OnDisappearing(){visible=false;life.Cancel();AppServices.Api.Cache.Changed-=CacheChanged;AppServices.AccountChanged-=AccountChanged;keyboard.Detach();mini?.Detach();base.OnDisappearing();}
    protected virtual Task LoadAsync()=>Task.CompletedTask;
    protected virtual Task OnToolbar()=>Ui.Push(new SettingsPage());
    protected virtual Task GoBack()=>Ui.Pop();
    protected override bool OnBackButtonPressed()
    {
        if(Navigation.NavigationStack.Count<=1)return base.OnBackButtonPressed();
        _=Run(GoBack,false);return true;
    }
    protected void ShowMini(bool visible){if(mini!=null)mini.IsVisible=visible;}
    protected ScrollView Scroll(View view)=>new(){Content=view,VerticalScrollBarVisibility=ScrollBarVisibility.Never};
    protected VerticalStackLayout Stack()=>new(){Padding=Ui.Inset(24,8,24,24),Spacing=0};
    public async Task Run(Func<Task> action,bool showBusy=true)
    {
        if(showBusy){workers++;spinner.IsRunning=true;spinner.IsVisible=true;}notice.IsVisible=false;
        try {await action();}
        catch(OperationCanceledException){}
        catch(Exception e){ShowMessage(Ui.ErrorText(e),e is MusicApiException api&&api.RequiresLogin);if(Body.Content==null||ReferenceEquals(Body.Content,initialSkeleton))Body.Content=Scroll(Ui.Empty("暂时没连上","请检查网络连接，然后再试一次。\n已下载的音乐仍然可以播放。",()=>Run(async()=>{await AppServices.InitializeAsync();await LoadAsync();loaded=true;})));}
        finally{if(showBusy)workers--;if(workers==0){spinner.IsRunning=false;spinner.IsVisible=false;LoadingChanged(false);}}
    }
    public void ShowMessage(string text,bool login=false)
    {
        notice.Clear();notice.Add(Ui.Text(text,13));if(login)notice.Add(Ui.Button("登录",()=>Run(()=>Ui.Push(new LoginPage())),"tonal"));
        notice.IsVisible=true;
    }
    public Task Open(Page page)=>Run(()=>Ui.Push(page),false);
    public Task Play(IEnumerable<Track> tracks,Track? start=null)=>Run(()=>AppServices.Playback.PlayAsync(tracks,start));
    public Task Alert(string message)=>DisplayAlertAsync("Petrichor",message,"知道了");
}
public sealed class MiniPlayer : ContentView
{
    internal static View? ActiveCover{get;private set;}
    internal View ArtworkView=>cover;
    private readonly Label title=Ui.Text("让音乐陪你走一段路",14),artist=Ui.Text("选择一首歌，开始播放",12,"Muted");
    private readonly Image cover=Ui.Cover("forest.png",44);
    private readonly Button play;
    private readonly PlaybackProgress progress=new();
    private bool attached;
    private long coverId=long.MinValue;
    public MiniPlayer()
    {
        HeightRequest=64;IsVisible=false;Margin=0;this.SetDynamicResource(BackgroundColorProperty,"SurfaceContainer");
        play=MooicLayout.Icon("play",async()=>{var page=Shell.Current.CurrentPage as BasePage;if(page!=null)await page.Run(async()=>{if(AppServices.Playback.Queue.Current==null)await Ui.Tab("search");else await AppServices.Playback.ToggleAsync();});},"播放或暂停");
        title.FontSize=14;title.FontAttributes=FontAttributes.Bold;artist.FontSize=12;cover.WidthRequest=48;cover.HeightRequest=48;cover.Clip=new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry(new CornerRadius(8),new Rect(0,0,48,48));
        var row=new Grid{SafeAreaEdges=SafeAreaEdges.None,ColumnDefinitions=[new(48),new(GridLength.Star),new(48),new(48)],ColumnSpacing=8,Padding=new Thickness(8,6)};play.WidthRequest=48;
        var info=new VerticalStackLayout{Spacing=Ui.S(4),VerticalOptions=LayoutOptions.Center};title.LineBreakMode=LineBreakMode.TailTruncation;artist.LineBreakMode=LineBreakMode.TailTruncation;
        info.Add(title);info.Add(artist);
        var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>{if(Shell.Current.CurrentPage is BasePage p)await p.Open(new PlayerPage());};info.GestureRecognizers.Add(tap);cover.GestureRecognizers.Add(tap);
        var next=MooicLayout.Icon("queue",async()=>{if(Shell.Current.CurrentPage is BasePage p)await p.Open(new QueuePage());},"播放队列");next.WidthRequest=48;
        row.Add(cover);row.Add(info,1);row.Add(play,2);row.Add(next,3);
        var grid=new Grid{SafeAreaEdges=SafeAreaEdges.None,RowDefinitions=[new(Ui.S(1)),new(GridLength.Star),new(Ui.S(2))]};
        grid.Add(Ui.Divider());grid.Add(row,0,1);grid.Add(progress,0,2);Content=grid;
    }
    public void Attach(){if(attached)return;attached=true;ActiveCover=cover;AppServices.Playback.Changed+=Update;Update();}
    public void Detach(){if(!attached)return;attached=false;if(ReferenceEquals(ActiveCover,cover))ActiveCover=null;AppServices.Playback.Changed-=Update;}
    private void Update()
    {
        var p=AppServices.Playback;var track=p.Queue.Current;
        IsVisible=track!=null;
        title.Text=track?.Name??"让音乐陪你走一段路";artist.Text=p.IsLoading?"正在加载…":p.Error??track?.ArtistText??"选择一首歌，开始播放";
        play.Text=Ui.Glyphs[p.IsPlaying?"pause":"play"];
        progress.Fraction=p.Duration.TotalMilliseconds>0?Math.Clamp(p.Position.TotalMilliseconds/p.Duration.TotalMilliseconds,0,1):0;
        if(track!=null&&coverId!=track.Id){coverId=track.Id;cover.Source=Ui.Art(AppServices.Offline.Cover(track),800);}
    }
}

// Drawing progress invalidates only two pixels of content, without repeatedly
// measuring the mini player and the list above it while audio is playing.
internal sealed class PlaybackProgress : GraphicsView,IDrawable
{
    private double fraction;
    public double Fraction{set{if(Math.Abs(fraction-value)<.00001)return;fraction=value;Invalidate();}}
    public PlaybackProgress(){HeightRequest=Ui.S(2);Drawable=this;}
    public void Draw(ICanvas canvas,RectF bounds){canvas.FillColor=Ui.Color("SurfaceHigh");canvas.FillRectangle(bounds);canvas.FillColor=Ui.Color("Primary");canvas.FillRectangle(0,0,bounds.Width*(float)fraction,bounds.Height);}
}
