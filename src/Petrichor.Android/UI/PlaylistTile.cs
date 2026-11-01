using Petrichor.Core;
namespace Petrichor.Mobile.UI;

// Recycled grid cell. The catalogue previously rebuilt every cover in a giant
// CollectionView header whenever another page of playlists arrived.
public sealed class PlaylistTile : ContentView
{
    private readonly Image art=Ui.Cover("",156);
    private readonly Label title=Ui.Text("",14),subtitle=Ui.Text("",12,"Muted");
    private readonly Label playCount=Ui.Text("",11);
    private readonly HorizontalStackLayout badge=new(){Spacing=Ui.S(4),HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.End,Margin=Ui.Inset(0,0,10,10)};
    public PlaylistTile(Func<MediaRow,Task> open)
    {
        art.WidthRequest=-1;art.Clip=null;
        art.SizeChanged+=(_,_)=>{if(art.Width>0&&Math.Abs(art.HeightRequest-art.Width)>.5)art.HeightRequest=art.Width;};
        title.MaxLines=2;title.MinimumHeightRequest=Ui.S(40);title.Margin=Ui.Inset(0,10,0,4);
        var icon=Ui.Glyph("headphones",14);icon.TextColor=Colors.White;playCount.TextColor=Colors.White;playCount.Shadow=new Shadow{Brush=Colors.Black,Offset=new(0,1),Radius=3,Opacity=.8f};badge.Add(icon);badge.Add(playCount);
        var artwork=new Grid();artwork.Add(art);artwork.Add(badge);
        var content=new VerticalStackLayout{Spacing=0};content.Add(Ui.Card(artwork,radius:16,padding:0));content.Add(title);content.Add(subtitle);Content=content;
        var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>{if(BindingContext is MediaRow row)await open(row);};GestureRecognizers.Add(tap);
        BindingContextChanged+=(_,_)=>{if(BindingContext is MediaRow{Value:Playlist playlist}){art.Source=Ui.Art(playlist.CoverUrl);title.Text=playlist.Name;subtitle.Text=playlist.TrackCount>0?playlist.TrackCount+"首":"歌单";badge.IsVisible=playlist.PlayCount>0;playCount.Text=playlist.PlayCount>=10000?(playlist.PlayCount/10000d).ToString("0.#")+"万":playlist.PlayCount.ToString();SemanticProperties.SetDescription(this,playlist.Name);}};
    }
}
