// MAUI adaptation of Mooic 3.0.1 UI proportions and composition.
// Source: kid-depress/Mooic, tag 3.0.1, GPL-3.0. See Resources/Raw/Mooic-LICENSE.txt.
// Actions and data remain Petrichor's AppServices / Core implementation.
using Petrichor.Core;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
namespace Petrichor.Mobile.UI;

internal static class MooicLayout
{
    public static Label Text(string text,double size=14,string color="OnSurface",bool bold=false)
    {
        var label=Ui.Text(text,size,color);label.FontSize=size;label.FontAttributes=bold?FontAttributes.Bold:FontAttributes.None;return label;
    }
    public static View Section(string text,Func<Task>? action=null)
    {
        var row=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Padding=new(12,12),MinimumHeightRequest=56};
        row.Add(Text(text,22,"OnSurface",false));
        if(action!=null)row.Add(Icon("chevron",action,"查看"+text),1);
        return row;
    }
    public static Button Icon(string name,Func<Task> action,string description,double size=24,double area=48,string background="Transparent",string foreground="Primary")
    {
        var button=Ui.Icon(name,action,description);if(name is "play" or "pause" or "next" or "previous")button.FontFamily="IconsFilled";button.WidthRequest=area;button.HeightRequest=area;button.MinimumHeightRequest=area;button.FontSize=size;button.CornerRadius=(int)(area/2);button.HorizontalOptions=LayoutOptions.Center;button.VerticalOptions=LayoutOptions.Center;button.SetDynamicResource(Button.BackgroundColorProperty,background);button.SetDynamicResource(Button.TextColorProperty,foreground);return button;
    }
    public static View Songs(IReadOnlyList<Track> tracks,BasePage page)
    {
        var width=Math.Min(340,DeviceDisplay.Current.MainDisplayInfo.Width/DeviceDisplay.Current.MainDisplayInfo.Density-60);
        var list=new CollectionView{HeightRequest=256,SelectionMode=SelectionMode.None,ItemsSource=tracks.Select(t=>new MediaRow{Title=t.Name,Subtitle=t.ArtistText,Cover=AppServices.Offline.Cover(t),Value=t}).ToArray(),ItemsLayout=new GridItemsLayout(4,ItemsLayoutOrientation.Horizontal),ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,HorizontalScrollBarVisibility=ScrollBarVisibility.Never};
        list.ItemTemplate=new DataTemplate(()=>new SongCard(width,row=>page.Play(tracks,(Track)row.Value),row=>page.Run(()=>Actions.TrackMenu(page,(Track)row.Value),false)));
        return list;
    }
    private sealed class SongCard:ContentView
    {
        public SongCard(double width,Func<MediaRow,Task> open,Func<MediaRow,Task> menu)
        {
            WidthRequest=width;HeightRequest=64;
            var item=new MediaRowView(open,menu){WidthRequest=width};Content=item;
        }
    }
    public static View Playlists(IReadOnlyList<Playlist> lists,BasePage page)
    {
        var view=new CollectionView{HeightRequest=182,SelectionMode=SelectionMode.None,ItemsSource=lists,ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Horizontal){ItemSpacing=12},HorizontalScrollBarVisibility=ScrollBarVisibility.Never,Margin=new(12,6,0,12)};
        view.ItemTemplate=new DataTemplate(()=>new PlaylistCard(page));return view;
    }
    private sealed class PlaylistCard:ContentView
    {
        private Playlist? playlist;
        public PlaylistCard(BasePage page)
        {
            WidthRequest=132;HeightRequest=180;
            var cover=Ui.Cover(null);cover.WidthRequest=120;cover.HeightRequest=120;cover.Clip=new RoundRectangleGeometry(new CornerRadius(10),new Rect(0,0,120,120));
            var title=Text("",14,bold:true);title.MaxLines=2;title.LineBreakMode=LineBreakMode.TailTruncation;
            var body=new VerticalStackLayout{Spacing=8};body.Add(cover);body.Add(title);Content=body;
            var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>{if(playlist!=null)await page.Open(new PlaylistPage(playlist.Id));};GestureRecognizers.Add(tap);
            BindingContextChanged+=(_,_)=>{if(BindingContext is Playlist value){playlist=value;cover.Source=Ui.Art(value.CoverUrl,300);title.Text=value.Name;SemanticProperties.SetDescription(this,value.Name);}};
        }
    }
}
