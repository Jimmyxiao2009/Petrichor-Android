using Petrichor.Core;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

internal sealed class DailyArtworkHeader:Grid
{
    private readonly Image artwork=new(){Aspect=Aspect.AspectFill,Source="forest.png"};
    private readonly Label count=MooicLayout.Text("",12,"Muted");
    public DailyArtworkHeader(Func<Task> play)
    {
        HeightRequest=260;IsClippedToBounds=true;SafeAreaEdges=SafeAreaEdges.None;Add(artwork);
        var clear=new GradientStop(Colors.Transparent,0);var surface=new GradientStop();surface.Offset=1;surface.SetDynamicResource(GradientStop.ColorProperty,"Surface");
        var fade=new BoxView{InputTransparent=true,Background=new LinearGradientBrush(new GradientStopCollection{clear,new(Colors.Transparent,.22f),surface},new Point(0,0),new Point(0,1))};Add(fade);
        var date=new VerticalStackLayout{Spacing=0,Padding=new Thickness(12,8),HorizontalOptions=LayoutOptions.Start,VerticalOptions=LayoutOptions.Start,Margin=16};
        date.Add(MooicLayout.Text(DateTime.Now.ToString("dd"),32,bold:true));date.Add(MooicLayout.Text(DateTime.Now.ToString("MM 月"),12));Add(Ui.Card(date,"SurfaceContainer",16,0));
        // The date card wraps content; it must not cover the entire artwork.
        var badge=(Border)Children[^1];badge.HorizontalOptions=LayoutOptions.Start;badge.VerticalOptions=LayoutOptions.Start;badge.Margin=16;date.Margin=0;
        var bottom=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Padding=new Thickness(20,12,20,8),VerticalOptions=LayoutOptions.End,ColumnSpacing=12};
        var caption=new VerticalStackLayout{Spacing=4};caption.Add(MooicLayout.Text("每日推荐",24,bold:true));caption.Add(count);bottom.Add(caption);bottom.Add(Ui.Button("播放全部",play,"filled","play"),1);Add(bottom);
    }
    public void Update(IReadOnlyList<Track> tracks){var track=tracks.FirstOrDefault();artwork.Source=Ui.Art(track==null?null:AppServices.Offline.Cover(track),800);count.Text=tracks.Count+" 首歌曲";}
}
