using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class ExplorePage:BasePage
{
    private readonly ContentView charts=new();
    protected override bool RefreshWhenCacheChanges=>true;
    public ExplorePage():base("探索",true)
    {
        var body=new VerticalStackLayout{Spacing=0,Padding=new Thickness(Md.Margin,0,Md.Margin,24)};
        body.Add(Ui.Section("排行榜",()=>Open(new CatalogPage(CatalogKind.Charts))));
        charts.Content=LoadingSkeleton.Grid2x2();charts.Margin=new Thickness(-Md.Margin,0);body.Add(charts);
        body.Add(Ui.Section("更多音乐"));
        var group=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],RowDefinitions=[new(GridLength.Auto),new(GridLength.Auto)],ColumnSpacing=8,RowSpacing=8};
        group.Add(PlayerFunctions.Tile("每日推荐","today",()=>Open(new CatalogPage(CatalogKind.Daily))));
        group.Add(PlayerFunctions.Tile("私人 FM","radio",()=>Open(new FmPage())),1);
        group.Add(PlayerFunctions.Tile("播客与节目","podcasts",()=>Open(new PodcastsPage())),0,1);
        group.Add(PlayerFunctions.Tile("歌单广场","playlist",()=>Open(new CatalogPage(CatalogKind.Playlists))),1,1);
        body.Add(group);Body.Content=Scroll(body);
    }
    protected override async Task LoadAsync()
    {
        var playlists=await AppServices.Api.GetChartsAsync(Token);Token.ThrowIfCancellationRequested();
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],RowDefinitions=[new(GridLength.Auto),new(GridLength.Auto)],ColumnSpacing=8,RowSpacing=8,Padding=new Thickness(Md.Margin,0)};
        var featured=playlists.Take(4).ToArray();for(var i=0;i<featured.Length;i++){var tile=new ChartTile(p=>Open(new PlaylistPage(p.Id))){BindingContext=featured[i]};grid.Add(tile,i%2,i/2);}
        var first=charts.Content is not Grid{Children.Count:>0} existing||existing.Children[0] is not ChartTile;
        charts.Content=grid;if(first)Dispatcher.Dispatch(()=>PageMotion.Reveal(grid,8));
    }
}
