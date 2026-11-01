using Petrichor.Core;
namespace Petrichor.Mobile.UI;

// Chart artwork is a small identifier inside a native, theme-aware card.
internal sealed class ChartTile:ContentView
{
    public ChartTile(Func<Playlist,Task> open)
    {
        var art=Ui.Cover(null);art.WidthRequest=40;art.HeightRequest=40;art.Clip=new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry(new CornerRadius(8),new Rect(0,0,40,40));
        var title=MooicLayout.Text("",14,bold:true);title.MaxLines=2;title.LineBreakMode=LineBreakMode.TailTruncation;
        var content=new Grid{ColumnDefinitions=[new(40),new(GridLength.Star)],ColumnSpacing=10,Padding=12,MinimumHeightRequest=88,InputTransparent=true};content.Add(art);content.Add(title,1);
        var hit=Ui.Button("",async()=>{var p=BindingContext as Playlist??(BindingContext as MediaRow)?.Value as Playlist;if(p!=null)await open(p);},"text");hit.HeightRequest=-1;hit.MinimumHeightRequest=48;hit.CornerRadius=16;hit.HorizontalOptions=hit.VerticalOptions=LayoutOptions.Fill;
        var grid=new Grid();grid.Add(content);grid.Add(hit);Content=Ui.Card(grid,"SurfaceContainer",20,0);
        BindingContextChanged+=(_,_)=>{var p=BindingContext as Playlist??(BindingContext as MediaRow)?.Value as Playlist;if(p==null)return;art.Source=Ui.Art(p.CoverUrl,200);title.Text=p.Name;SemanticProperties.SetDescription(hit,p.Name);};
    }
}
