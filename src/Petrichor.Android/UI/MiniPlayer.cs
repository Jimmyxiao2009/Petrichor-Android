using Android.Widget;
using Microsoft.Maui.Platform;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
using AView=Android.Views.View;
namespace Petrichor.Mobile.UI;

// One mini player for the whole app, inserted natively between Shell's page area and the navigation
// bar. It stays still while pages transition underneath it and shares the navigation bar's surface.
public sealed class MiniPlayer : ContentView
{
    internal static MiniPlayer? Shared{get;private set;}
    internal View ArtworkView=>cover;
    private readonly Label title=Ui.Line("",TypeRole.TitleSmall),artist=Ui.Line("",TypeRole.BodySmall,"OnSurfaceVariant");
    private readonly Image cover=Ui.Cover(null,48);
    private readonly IconButton play;
    private readonly PlaybackProgress progress=new();
    private AView? native;
    private Page? suppressedBy;
    private long coverId=long.MinValue;
    private bool? playing;
    private MiniPlayer()
    {
        HeightRequest=66;IsVisible=false;SafeAreaEdges=SafeAreaEdges.None;this.SetDynamicResource(BackgroundColorProperty,"SurfaceContainer");
        play=new IconButton("play",async()=>{if(Shell.Current?.CurrentPage is BasePage page)await page.Run(async()=>{if(AppServices.Playback.Queue.Current==null)await Ui.Tab("search");else await AppServices.Playback.ToggleAsync();});},"播放或暂停",filled:true,foreground:"OnSurface");
        var queue=new IconButton("queue",async()=>{if(Shell.Current?.CurrentPage is BasePage page)await page.Open(new QueuePage());},"播放队列");
        var row=new Grid{SafeAreaEdges=SafeAreaEdges.None,ColumnDefinitions=[new(48),new(GridLength.Star),new(GridLength.Auto),new(GridLength.Auto)],ColumnSpacing=12,Padding=new Thickness(12,8,4,8)};
        var info=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center,InputTransparent=true};info.Add(title);info.Add(artist);
        row.Add(cover);row.Add(info,1);row.Add(play,2);row.Add(queue,3);
        Pressable.Attach(row,async()=>{if(Shell.Current?.CurrentPage is BasePage page)await page.Open(new PlayerPage());},description:"打开正在播放");
        var grid=new Grid{SafeAreaEdges=SafeAreaEdges.None,RowDefinitions=[new(GridLength.Star),new(2)]};grid.Add(row);grid.Add(progress,0,1);Content=grid;
        AppServices.Playback.Changed+=Update;NavigationBar.VisibilityChanged+=()=>Sync();
        Update();
    }
    internal static void Host(AView root,Shell shell,IMauiContext context)
    {
        if(root is not LinearLayout outer||NavigationBar.Find(outer) is not{} bar)return;
        var mini=Shared??=new MiniPlayer();
        if(mini.Parent==null)mini.Parent=shell;
        var platform=mini.ToPlatform(context);
        (platform.Parent as Android.Views.ViewGroup)?.RemoveView(platform);
        outer.AddView(platform,outer.IndexOfChild(bar),new LinearLayout.LayoutParams(Android.Views.ViewGroup.LayoutParams.MatchParent,Android.Views.ViewGroup.LayoutParams.WrapContent));
        mini.native=platform;mini.Sync();
    }
    // A page in a selection mode can hide the mini player while it is current.
    internal static void Suppress(Page page,bool suppress)
    {
        if(Shared==null)return;
        if(suppress)Shared.suppressedBy=page;else if(ReferenceEquals(Shared.suppressedBy,page))Shared.suppressedBy=null;
        Shared.Sync(true);
    }
    internal static void Refresh()=>Shared?.Sync();
    private void Sync(bool animate=false)
    {
        var page=Shell.Current?.CurrentPage;
        if(suppressedBy!=null&&!ReferenceEquals(suppressedBy,page))suppressedBy=null;
        var visible=AppServices.Playback.Queue.Current!=null&&NavigationBar.IsShown&&suppressedBy==null&&(page as BasePage)?.ShowsChrome!=false;
        if(visible==IsVisible)return;
        IsVisible=visible;ThemeColors.UpdateSystemBars();
        if(visible&&animate&&PageMotion.Enabled&&native!=null)
        {
            var offset=(float)(HeightRequest*native.Resources!.DisplayMetrics!.Density);
            native.TranslationY=offset;native.Alpha=0;
            native.Animate()!.TranslationY(0).Alpha(1).SetDuration(Md.Medium2).SetInterpolator(Md.NativeEmphasizedDecelerate()).Start();
        }
    }
    private void Update()
    {
        var p=AppServices.Playback;var track=p.Queue.Current;
        title.Text=track?.Name??"";artist.Text=p.IsLoading?"正在加载…":p.Error??track?.ArtistText??"";
        if(playing!=p.IsPlaying){var first=playing==null;playing=p.IsPlaying;play.SetIcon(p.IsPlaying?"pause":"play",true,!first);}
        progress.Fraction=p.Duration.TotalMilliseconds>0?Math.Clamp(p.Position.TotalMilliseconds/p.Duration.TotalMilliseconds,0,1):0;
        if(track!=null&&coverId!=track.Id){coverId=track.Id;cover.Source=Ui.Art(AppServices.Offline.Cover(track),300);}
        if((track!=null)!=IsVisible)Sync(true);
    }
}

// Drawing progress invalidates only two pixels of content, without repeatedly
// measuring the mini player and the list above it while audio is playing.
internal sealed class PlaybackProgress : GraphicsView,IDrawable
{
    private double fraction;
    public double Fraction{set{if(Math.Abs(fraction-value)<.00001)return;fraction=value;Invalidate();}}
    public PlaybackProgress(){HeightRequest=2;Drawable=this;InputTransparent=true;}
    public void Draw(ICanvas canvas,RectF bounds){canvas.FillColor=Ui.Color("SurfaceContainerHighest");canvas.FillRectangle(bounds);canvas.FillColor=Ui.Color("Primary");canvas.FillRectangle(0,0,bounds.Width*(float)fraction,bounds.Height);}
}
