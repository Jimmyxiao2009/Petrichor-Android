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
        var stack=new VerticalStackLayout{Spacing=0,Padding=new Thickness(0,0,0,24)};
        stack.Add(Padded(Ui.Section("推荐歌曲",()=>Open(new CatalogPage(CatalogKind.Daily)))));
        songs.Content=LoadingSkeleton.Rows(Md.Margin);songs.HeightRequest=256;stack.Add(songs);
        stack.Add(Padded(Ui.Section("推荐歌单",()=>Open(new CatalogPage(CatalogKind.Playlists)))));
        recommendations.Content=LoadingSkeleton.Tiles();recommendations.MinimumHeightRequest=196;stack.Add(recommendations);
        Body.Content=Scroll(stack);
    }
    private static View Padded(View view){view.Margin=new Thickness(Md.Margin,view.Margin.Top,Md.Margin,view.Margin.Bottom);return view;}
    protected override void AccountInvalidated(){songs.Content=LoadingSkeleton.Rows(Md.Margin);recommendations.Content=LoadingSkeleton.Tiles();}
    protected override Task LoadAsync()=>Task.WhenAll(LoadSongs(),LoadPlaylists());
    private static void Show(ContentView host,View content)
    {
        var first=host.Content is not CollectionView;host.Content=content;
        if(first)host.Dispatcher.Dispatch(()=>PageMotion.Reveal(content,8));
    }
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
            Token.ThrowIfCancellationRequested();Show(songs,MooicLayout.Songs(tracks.Take(24).ToList(),this));
        }catch(OperationCanceledException){throw;}
        catch{if(songs.Content is not CollectionView)songs.Content=Retry("推荐歌曲暂时没有加载出来",()=>Run(LoadSongs));}
    }
    private async Task LoadPlaylists()
    {
        try{var playlists=await AppServices.Api.GetRecommendedPlaylistsAsync(12,Token);Token.ThrowIfCancellationRequested();Show(recommendations,MooicLayout.Playlists(playlists,this));}
        catch(OperationCanceledException){throw;}
        catch{if(recommendations.Content is not CollectionView)recommendations.Content=Retry("推荐歌单暂时没有加载出来",()=>Run(LoadPlaylists));}
    }
    private static View Retry(string text,Func<Task> retry)
    {
        var row=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Padding=new Thickness(Md.Margin,8,8,8),VerticalOptions=LayoutOptions.Center};
        row.Add(Ui.Text(text,TypeRole.BodyMedium,"OnSurfaceVariant"));row.Add(Ui.Button("重试",retry,"text"),1);return row;
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
        more=Ui.Button("加载更多",()=>Run(()=>Fetch(false)),"outlined");more.IsVisible=false;more.HorizontalOptions=LayoutOptions.Center;Footer.Add(more);
        if(kind==CatalogKind.Playlists)
        {
            var filters=new List<FilterChip>();var row=new HorizontalStackLayout{Spacing=8,Padding=new Thickness(0,8)};
            foreach(var name in new[]{"全部","华语","流行","轻音乐","独立"}){var chip=DesignControls.Chip(name,()=>Run(async()=>{category=name;foreach(var c in filters)c.IsSelected=c.Title==name;await Fetch(true);}),name=="全部");filters.Add(chip);row.Add(chip);}
            Header.Add(DesignControls.ChipScroller(row));
        }
        if(kind is CatalogKind.Playlists or CatalogKind.Charts)
        {
            List.Margin=new Thickness(Md.Margin,0);Header.Padding=new Thickness(0,0,0,8);SetEmptyView(null);
            List.ItemsLayout=new GridItemsLayout(kind==CatalogKind.Charts?2:3,ItemsLayoutOrientation.Vertical){HorizontalItemSpacing=kind==CatalogKind.Charts?8:12,VerticalItemSpacing=kind==CatalogKind.Charts?8:16};
            List.ItemSizingStrategy=ItemSizingStrategy.MeasureAllItems;
            List.ItemTemplate=kind==CatalogKind.Charts?new DataTemplate(()=>new ChartTile(p=>Open(new PlaylistPage(p.Id)))):new DataTemplate(()=>new PlaylistTile(row=>Run(()=>OnOpen(row),false)));
        }
        if(kind==CatalogKind.Daily){dailyHeader=new DailyArtworkHeader(()=>Play(Rows.Where(r=>r.Value is Track).Select(r=>(Track)r.Value)));Header.Add(dailyHeader);}
        if(kind==CatalogKind.Liked)
        {
            var play=Ui.Button("播放全部",()=>Play(Rows.Where(r=>r.Value is Track).Select(r=>(Track)r.Value)),"filled","play");play.HorizontalOptions=LayoutOptions.Start;play.Margin=new Thickness(0,8,0,0);Header.Add(play);
            Header.Add(Ui.Section("所有喜欢"));
        }
        if(kind is CatalogKind.SavedArtists or CatalogKind.SavedAlbums){var tabs=new TabStrip(["歌手","专辑"],index=>Run(async()=>{this.kind=index==0?CatalogKind.SavedArtists:CatalogKind.SavedAlbums;await Fetch(true);}),kind==CatalogKind.SavedArtists?0:1);tabs.Margin=new Thickness(-Md.Margin,0,-Md.Margin,8);Header.Add(tabs);}
        if(kind==CatalogKind.Local){var import=Ui.Button("导入本地音乐",()=>Run(async()=>{await AppServices.Offline.ImportAsync();await Fetch(true);}),"tonal","add");import.HorizontalOptions=LayoutOptions.Start;import.Margin=new Thickness(0,8);Header.Add(import);}
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
            var choice=await Actions.Sheet(this,t.Name,t.ArtistText,[("play","立即播放"),("remove","移除本机文件")],AppServices.Offline.Cover(t));
            if(choice=="play")await OnOpen(row);
            if(choice=="remove"&&await Dialogs.Confirm("移除本机文件？","将删除应用保存的这份音频文件。","移除",destructive:true,icon:"delete")){await AppServices.Offline.DeleteAsync(t);await Fetch(true);}
        }
        else await Actions.TrackMenu(this,t);
    }
}
