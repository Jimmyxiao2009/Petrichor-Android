using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class DiscoverPage : BasePage
{
    protected override bool RefreshWhenCacheChanges=>true;
    private readonly ContentView recommendations=new(),songs=new();
    public DiscoverPage():base("主页",true)
    {
        var stack=new VerticalStackLayout{Spacing=0,Padding=new Thickness(0,0,0,20)};
        stack.Add(MooicLayout.Section("推荐歌曲",()=>Open(new CatalogPage(CatalogKind.Daily))));
        songs.Content=LoadingSkeleton.Rows();songs.HeightRequest=256;stack.Add(songs);
        stack.Add(MooicLayout.Section("推荐歌单",()=>Open(new CatalogPage(CatalogKind.Playlists))));
        recommendations.Content=LoadingSkeleton.Tiles();recommendations.MinimumHeightRequest=182;stack.Add(recommendations);
        Body.Content=Scroll(stack);
    }
    protected override void AccountInvalidated(){songs.Content=LoadingSkeleton.Rows();recommendations.Content=LoadingSkeleton.Tiles();}
    protected override Task LoadAsync()=>Task.WhenAll(LoadSongs(),LoadPlaylists());
    private async Task LoadSongs()
    {
        try{
            List<Track> tracks;
            if(AppServices.Api.IsLoggedIn)tracks=await AppServices.Api.GetDailySongsAsync(Token);
            else{
                var charts=await AppServices.Api.GetChartsAsync(Token);Token.ThrowIfCancellationRequested();
                var chart=charts.FirstOrDefault()??throw new InvalidOperationException("暂无推荐歌曲");
                var detail=await AppServices.Api.GetPlaylistAsync(chart.Id,Token);
                tracks=detail.Tracks.Take(12).ToList();
                if(tracks.Count==0)tracks=await AppServices.Api.GetPlaylistTracksAsync(detail,12,0,Token);
            }
            Token.ThrowIfCancellationRequested();songs.Content=MooicLayout.Songs(tracks.Take(24).ToList(),this);
        }catch(OperationCanceledException){throw;}
        catch{if(songs.Content is not CollectionView)songs.Content=Ui.Button("重新加载推荐歌曲",()=>Run(LoadSongs),"text");}
    }
    private async Task LoadPlaylists()
    {
        try{var playlists=await AppServices.Api.GetRecommendedPlaylistsAsync(12,Token);Token.ThrowIfCancellationRequested();recommendations.Content=MooicLayout.Playlists(playlists,this);}
        catch(OperationCanceledException){throw;}
        catch{if(recommendations.Content is not CollectionView)recommendations.Content=Ui.Button("重新加载推荐歌单",()=>Run(LoadPlaylists),"text");}
    }
    public static View PlaylistGrid(IReadOnlyList<Playlist> lists,BasePage page)
    {
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=Ui.S(14),RowSpacing=Ui.S(20)};
        for(int i=0;i<lists.Count;i++)
        {
            if(i%3==0)grid.RowDefinitions.Add(new(GridLength.Auto));
            var playlist=lists[i];var content=new VerticalStackLayout{Spacing=0};
            var image=Ui.Cover(playlist.CoverUrl,156);image.WidthRequest=-1;image.Clip=null;
            image.SizeChanged+=(_,_)=>{if(image.Width>0&&Math.Abs(image.HeightRequest-image.Width)>.5)image.HeightRequest=image.Width;};
            var art=Ui.Card(image,radius:16,padding:0);
            var artwork=new Grid();artwork.Add(art);
            if(playlist.PlayCount>0){var badge=new HorizontalStackLayout{Spacing=Ui.S(4),HorizontalOptions=LayoutOptions.End,VerticalOptions=LayoutOptions.End,Margin=Ui.Inset(0,0,10,10)};var icon=Ui.Glyph("headphones",14);icon.TextColor=Colors.White;badge.Add(icon);var count=Ui.Text(playlist.PlayCount>=10000?(playlist.PlayCount/10000d).ToString("0.#")+"万":playlist.PlayCount.ToString(),11);count.TextColor=Colors.White;count.Shadow=new Shadow{Brush=Colors.Black,Offset=new(0,1),Radius=3,Opacity=.8f};badge.Add(count);artwork.Add(badge);}
            content.Add(artwork);var title=Ui.Text(playlist.Name,14);title.MaxLines=2;title.Margin=Ui.Inset(0,10,0,4);content.Add(title);
            content.Add(Ui.Text(playlist.TrackCount>0?playlist.TrackCount+"首":"推荐歌单",12,"Muted"));
            var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await page.Open(new PlaylistPage(playlist.Id));content.GestureRecognizers.Add(tap);
            SemanticProperties.SetDescription(content,playlist.Name);grid.Add(content,i%3,i/3);
        }
        return grid;
    }
}
public enum CatalogKind{Playlists,Charts,Daily,Liked,Recent,Downloads,Local,SavedArtists,SavedAlbums,SubscribedRadios}
public sealed class CatalogPage : ListPage
{
    private CatalogKind kind;
    private string category="全部";
    private int offset;
    private readonly Button more;
    private readonly DailyArtworkHeader? dailyHeader;
    protected override bool RefreshOnAppear=>kind is CatalogKind.Recent or CatalogKind.Downloads or CatalogKind.Local or CatalogKind.Liked;
    public CatalogPage(CatalogKind kind):base(Name(kind))
    {
        this.kind=kind;
        string heading=kind switch{CatalogKind.Playlists=>"总有一份，为你。",CatalogKind.Charts=>"此刻，正在流行。",CatalogKind.Daily=>"今天，也有好音乐。",CatalogKind.Liked=>"喜欢，都有回响。",CatalogKind.Recent=>"好听，就再听一遍。",CatalogKind.SubscribedRadios=>"那些熟悉的声音。",CatalogKind.Local=>"你收藏的每一段声音。",CatalogKind.SavedArtists or CatalogKind.SavedAlbums=>"熟悉的声音。",_=>Name(kind)};
        more=Ui.Button("加载更多",()=>Run(()=>Fetch(false)),"outlined");more.IsVisible=false;Footer.Add(more);
        if(kind==CatalogKind.Playlists){var chips=new HorizontalStackLayout{Spacing=Ui.S(8)};var filters=new List<Button>();foreach(var name in new[]{"全部","华语","流行","轻音乐","独立"}){var b=DesignControls.Chip(name,()=>Run(async()=>{category=name;foreach(var button in filters)DesignControls.SelectChip(button,button.AutomationId==name);await Fetch(true);}),name=="全部");filters.Add(b);chips.Add(b);}Header.Add(new ScrollView{Content=chips,Orientation=ScrollOrientation.Horizontal,HorizontalScrollBarVisibility=ScrollBarVisibility.Never,Margin=Ui.Inset(0,0,0,24)});}
        if(kind is CatalogKind.Playlists or CatalogKind.Charts)
        {
            List.Margin=Ui.Inset(24,0);Header.Padding=Ui.Inset(0,8,0,8);SetEmptyView(null);
            List.ItemsLayout=new GridItemsLayout(kind==CatalogKind.Charts?2:3,ItemsLayoutOrientation.Vertical){HorizontalItemSpacing=12,VerticalItemSpacing=kind==CatalogKind.Charts?12:20};
            List.ItemSizingStrategy=ItemSizingStrategy.MeasureAllItems;
            List.ItemTemplate=kind==CatalogKind.Charts?new DataTemplate(()=>new ChartTile(p=>Open(new PlaylistPage(p.Id)))):new DataTemplate(()=>new PlaylistTile(row=>Run(()=>OnOpen(row),false)));
        }
        if(kind==CatalogKind.Daily){Header.Padding=0;dailyHeader=new DailyArtworkHeader(()=>Play(Rows.Where(r=>r.Value is Track).Select(r=>(Track)r.Value)));Header.Add(dailyHeader);}
        if(kind==CatalogKind.Liked){Header.Add(Ui.Button("播放全部",()=>Play(Rows.Where(r=>r.Value is Track).Select(r=>(Track)r.Value)),"filled","play"));Header.Add(Ui.Section("所有喜欢"));}
        if(kind is CatalogKind.SavedArtists or CatalogKind.SavedAlbums)Header.Add(new TabStrip(["歌手","专辑"],index=>Run(async()=>{this.kind=index==0?CatalogKind.SavedArtists:CatalogKind.SavedAlbums;await Fetch(true);}),kind==CatalogKind.SavedArtists?0:1));
        if(kind==CatalogKind.Local)Header.Add(Ui.Button("导入本地音乐",()=>Run(async()=>{await AppServices.Offline.ImportAsync();await Fetch(true);}),"filled","add"));
    }
    private static string Name(CatalogKind kind)=>kind switch{CatalogKind.Playlists=>"歌单广场",CatalogKind.Charts=>"排行榜",CatalogKind.Daily=>"每日推荐",CatalogKind.Liked=>"我喜欢的音乐",CatalogKind.Recent=>"最近播放",CatalogKind.Downloads=>"已下载",CatalogKind.Local=>"本地音乐",CatalogKind.SavedArtists=>"收藏歌手",CatalogKind.SavedAlbums=>"收藏专辑",_=>"订阅的播客"};
    protected override Task LoadAsync()=>Fetch(true);
    private async Task Fetch(bool reset)
    {
        var skip=reset?0:offset;var api=AppServices.Api;IEnumerable<object> values;bool hasMore=false;
        switch(kind)
        {
            case CatalogKind.Playlists:var p=await api.GetPlaylistsAsync(category,30,skip,Token);values=p.Items;hasMore=p.HasMore;break;
            case CatalogKind.Charts:values=await api.GetChartsAsync(Token);break;
            case CatalogKind.Daily:var daily=await api.GetDailySongsAsync(Token);dailyHeader!.Update(daily);values=daily;break;
            case CatalogKind.Liked:
                var ids=await api.GetLikedTrackIdsAsync(Token);values=await api.GetTracksAsync(ids.Skip(skip).Take(100),Token);hasMore=skip+100<ids.Count;break;
            case CatalogKind.Recent:values=AppServices.Offline.Recent;break;
            case CatalogKind.Downloads:values=AppServices.Offline.Downloads;break;
            case CatalogKind.Local:values=AppServices.Offline.Local;break;
            case CatalogKind.SavedArtists:var artists=await api.GetSavedArtistsAsync(30,skip,Token);values=artists.Items;hasMore=artists.HasMore;break;
            case CatalogKind.SavedAlbums:var albums=await api.GetSavedAlbumsAsync(30,skip,Token);values=albums.Items;hasMore=albums.HasMore;break;
            default:var radios=await api.GetSubscribedRadiosAsync(30,skip,Token);values=radios.Items;hasMore=radios.HasMore;break;
        }
        Token.ThrowIfCancellationRequested();
        var rows=values.Select(Actions.Row);if(reset){Replace(rows);ScrollTop();}else Rows.AddRange(rows);
        offset=skip+(kind==CatalogKind.Liked?100:30);more.IsVisible=hasMore;
    }
    protected override Task OnOpen(MediaRow row)=>row.Value is Track t?Play(Rows.Where(r=>r.Value is Track).Select(r=>(Track)r.Value),t):Actions.Open(this,row.Value);
    protected override async Task OnMenu(MediaRow row)
    {
        if(row.Value is not Track t){await OnOpen(row);return;}
        if(kind is CatalogKind.Local or CatalogKind.Downloads)
        {
            var choice=await Actions.Sheet(this,t.Name,t.ArtistText,[("play","立即播放"),("remove","移除本机文件")]);
            if(choice=="play")await OnOpen(row);
            if(choice=="remove"&&await DisplayAlertAsync("移除本机文件","将删除应用保存的这份音频文件。","移除","取消")){await AppServices.Offline.DeleteAsync(t);await Fetch(true);}
        }
        else await Actions.TrackMenu(this,t);
    }
}
