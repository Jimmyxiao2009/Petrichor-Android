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
        var body=new VerticalStackLayout{Spacing=0};body.Add(MooicLayout.Section("排行榜",()=>Open(new CatalogPage(CatalogKind.Charts))));
        charts.Content=LoadingSkeleton.Rows();body.Add(charts);
        body.Add(MooicLayout.Section("更多音乐"));
        var group=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],RowDefinitions=[new(GridLength.Auto),new(GridLength.Auto)],ColumnSpacing=12,RowSpacing=12,Padding=new Thickness(16,0,16,20)};
        group.Add(PlayerFunctions.Tile("每日推荐","today",()=>Open(new CatalogPage(CatalogKind.Daily))));
        group.Add(PlayerFunctions.Tile("私人 FM","radio",()=>Open(new FmPage())),1);
        group.Add(PlayerFunctions.Tile("播客与节目","podcasts",()=>Open(new PodcastsPage())),0,1);
        group.Add(PlayerFunctions.Tile("歌单广场","playlist",()=>Open(new CatalogPage(CatalogKind.Playlists))),1,1);
        body.Add(group);Body.Content=Scroll(body);
    }
    protected override async Task LoadAsync()
    {
        var playlists=await AppServices.Api.GetChartsAsync(Token);Token.ThrowIfCancellationRequested();
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],RowDefinitions=[new(GridLength.Auto),new(GridLength.Auto)],ColumnSpacing=12,RowSpacing=12,Padding=new Thickness(16,0)};
        var featured=playlists.Take(4).ToArray();for(var i=0;i<featured.Length;i++){var tile=new ChartTile(p=>Open(new PlaylistPage(p.Id))){BindingContext=featured[i]};grid.Add(tile,i%2,i/2);}charts.Content=grid;
    }
}
