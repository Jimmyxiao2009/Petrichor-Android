using Petrichor.Core;
namespace Petrichor.Mobile.UI;

// Chart artwork is a small identifier inside a filled Material 3 card.
internal sealed class ChartTile:ContentView
{
    public ChartTile(Func<Playlist,Task> open)
    {
        var art=Ui.Cover(null,48);
        var title=Ui.Text("",TypeRole.TitleSmall);title.MaxLines=2;title.LineBreakMode=LineBreakMode.TailTruncation;
        var content=new Grid{ColumnDefinitions=[new(48),new(GridLength.Star)],ColumnSpacing=12,Padding=new Thickness(12),MinimumHeightRequest=72,InputTransparent=true};content.Add(art);content.Add(title,1);
        var card=Ui.Card(content,"SurfaceContainerHighest",Md.ShapeLarge,0);Content=card;
        Pressable.Attach(card,async()=>{var p=BindingContext as Playlist??(BindingContext as MediaRow)?.Value as Playlist;if(p!=null)await open(p);},radius:Md.ShapeLarge);
        BindingContextChanged+=(_,_)=>{var p=BindingContext as Playlist??(BindingContext as MediaRow)?.Value as Playlist;if(p==null)return;art.Source=Ui.Art(p.CoverUrl,200);title.Text=p.Name;SemanticProperties.SetDescription(card,p.Name);};
    }
}
