using Petrichor.Core;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

// Hero header for the daily mix: the first track's artwork fades into the page surface.
internal sealed class DailyArtworkHeader:Grid
{
    private readonly Image artwork=new(){Aspect=Aspect.AspectFill,Source="forest.png"};
    private readonly Label count=Ui.Line("",TypeRole.BodyMedium,"OnSurfaceVariant");
    public DailyArtworkHeader(Func<Task> play)
    {
        HeightRequest=280;IsClippedToBounds=true;SafeAreaEdges=SafeAreaEdges.None;Margin=new Thickness(-Md.Margin,0,-Md.Margin,8);Add(artwork);
        var clear=new GradientStop(Colors.Transparent,0);var surface=new GradientStop();surface.Offset=1;surface.SetDynamicResource(GradientStop.ColorProperty,"Surface");
        var fade=new BoxView{InputTransparent=true,Background=new LinearGradientBrush(new GradientStopCollection{clear,new(Colors.Transparent,.3f),surface},new Point(0,0),new Point(0,1))};Add(fade);
        var date=new VerticalStackLayout{Spacing=0,HorizontalOptions=LayoutOptions.Center};
        var day=Ui.Text(DateTime.Now.ToString("dd"),TypeRole.HeadlineMedium);day.HorizontalTextAlignment=TextAlignment.Center;date.Add(day);
        var month=Ui.Line(DateTime.Now.ToString("M 月"),TypeRole.LabelMedium,"OnSurfaceVariant");month.HorizontalTextAlignment=TextAlignment.Center;date.Add(month);
        var badge=Ui.Card(date,"SurfaceContainerHigh",Md.ShapeLarge,new Thickness(16,8));badge.HorizontalOptions=LayoutOptions.Start;badge.VerticalOptions=LayoutOptions.Start;badge.Margin=new Thickness(Md.Margin);Add(badge);
        var bottom=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Padding=new Thickness(Md.Margin,12,Md.Margin,4),VerticalOptions=LayoutOptions.End,ColumnSpacing=12};
        var caption=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center};caption.Add(Ui.Line("每日推荐",TypeRole.HeadlineSmall));caption.Add(count);bottom.Add(caption);
        var playAll=Ui.Button("播放全部",play,"filled","play");playAll.VerticalOptions=LayoutOptions.Center;bottom.Add(playAll,1);Add(bottom);
    }
    public void Update(IReadOnlyList<Track> tracks){var track=tracks.FirstOrDefault();artwork.Source=Ui.Art(track==null?null:AppServices.Offline.Cover(track),800);count.Text=tracks.Count+" 首歌曲";}
}
