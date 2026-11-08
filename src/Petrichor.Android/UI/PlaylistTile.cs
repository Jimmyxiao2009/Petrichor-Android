using Petrichor.Core;
namespace Petrichor.Mobile.UI;

// Recycled grid cell. The catalogue previously rebuilt every cover in a giant
// CollectionView header whenever another page of playlists arrived.
public sealed class PlaylistTile : ContentView
{
    private readonly Image art=new();
    private readonly Label title=Ui.Text("",TypeRole.TitleSmall),subtitle=Ui.Line("",TypeRole.BodySmall,"OnSurfaceVariant");
    private readonly Label playCount=Ui.Line("",TypeRole.LabelSmall);
    private readonly Border badge;
    public PlaylistTile(Func<MediaRow,Task> open)
    {
        title.MaxLines=2;title.LineBreakMode=LineBreakMode.TailTruncation;title.Margin=new Thickness(0,8,0,2);
        var row=new HorizontalStackLayout{Spacing=2,InputTransparent=true};var icon=Ui.Glyph("headphones",12,"InverseOnSurface");row.Add(icon);playCount.SetDynamicResource(Label.TextColorProperty,"InverseOnSurface");row.Add(playCount);
        // Play counts sit on a small scrim chip so they stay legible on any artwork.
        badge=Ui.Card(row,"InverseSurface",Md.ShapeSmall,new Thickness(6,2));badge.Opacity=.86;badge.HorizontalOptions=LayoutOptions.End;badge.VerticalOptions=LayoutOptions.End;badge.Margin=new Thickness(0,0,6,6);badge.InputTransparent=true;
        var artwork=new Grid{InputTransparent=true};artwork.Add(Ui.Artwork(art,Md.ShapeLarge));artwork.Add(badge);
        var content=new VerticalStackLayout{Spacing=0,InputTransparent=true};content.Add(artwork);content.Add(title);content.Add(subtitle);
        var frame=new ContentView{Content=content,Padding=new Thickness(0,0,0,4)};Content=frame;
        Pressable.Attach(frame,async()=>{if(BindingContext is MediaRow row)await open(row);},radius:Md.ShapeLarge);
        BindingContextChanged+=(_,_)=>{if(BindingContext is MediaRow{Value:Playlist playlist}){art.Source=Ui.Art(playlist.CoverUrl);title.Text=playlist.Name;subtitle.Text=playlist.TrackCount>0?playlist.TrackCount+"首":"歌单";badge.IsVisible=playlist.PlayCount>0;playCount.Text=playlist.PlayCount>=10000?(playlist.PlayCount/10000d).ToString("0.#")+"万":playlist.PlayCount.ToString();SemanticProperties.SetDescription(frame,playlist.Name);}};
    }
}
