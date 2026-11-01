using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;
public sealed class PodcastsPage : ListPage
{
    private bool subscribed;private int offset;
    private readonly Button more;
    private readonly Grid featured=new(){ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=Ui.S(14)};
    private readonly List<Button> chips=[];
    private readonly Label listTitle=Ui.Text("最近更新",22);
    private readonly View featuredTitle=Ui.Section("值得一听");
    public PodcastsPage():base("播客")
    {
        var search=new SearchField{Placeholder="找播客，或搜节目里的歌"};search.SearchButtonPressed+=async(_,_)=>await Open(new SearchPage(search.Text,4));Header.Add(Ui.Search(search));
        var tabs=new HorizontalStackLayout{Spacing=Ui.S(8)};
        for(int i=0;i<2;i++){int index=i;var chip=DesignControls.Chip(i==0?"热门":"我的订阅",()=>Run(async()=>{subscribed=index==1;for(int j=0;j<chips.Count;j++)DesignControls.SelectChip(chips[j],j==index);await Fetch(true);}),i==0);chips.Add(chip);tabs.Add(chip);}
        foreach(var name in new[]{"音乐故事","创作翻唱","情感"})tabs.Add(DesignControls.Chip(name,()=>Open(new SearchPage(name,4))));
        Header.Add(new ScrollView{Orientation=ScrollOrientation.Horizontal,Content=tabs,HorizontalScrollBarVisibility=ScrollBarVisibility.Never,Margin=Ui.Inset(0,16,0,8)});
        Header.Add(featuredTitle);Header.Add(featured);
        listTitle.Margin=Ui.Inset(0,26,0,14);Header.Add(listTitle);
        more=Ui.Button("加载更多",()=>Run(()=>Fetch(false)),"outlined");Footer.Add(more);
    }
    protected override Task LoadAsync()=>Fetch(true);
    private async Task Fetch(bool reset)
    {
        var skip=reset?0:offset;
        var page=subscribed?await AppServices.Api.GetSubscribedRadiosAsync(30,skip,Token):await AppServices.Api.GetHotRadiosAsync(30,skip,Token);
        Token.ThrowIfCancellationRequested();if(reset){Rows.Clear();featured.Clear();featured.IsVisible=featuredTitle.IsVisible=!subscribed;listTitle.Text=subscribed?"我的订阅":"最近更新";}
        if(reset&&!subscribed)
        {
            for(int i=0;i<Math.Min(2,page.Items.Count);i++){var radio=page.Items[i];var tile=new VerticalStackLayout{Spacing=Ui.S(8)};var image=Ui.Cover(radio.CoverUrl,156);image.WidthRequest=-1;image.Clip=null;image.SizeChanged+=(_,_)=>{if(image.Width>0&&Math.Abs(image.HeightRequest-image.Width)>.5)image.HeightRequest=image.Width;};tile.Add(Ui.Card(image,radius:16,padding:0));var name=Ui.Text(radio.Name,14);name.MaxLines=2;tile.Add(name);tile.Add(Ui.Text(radio.Description.Length>0?radio.Description:radio.DjName,12,"Muted"));var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await Open(new PodcastPage(radio.Id));tile.GestureRecognizers.Add(tap);featured.Add(tile,i);}
        }
        var rows=page.Items.Skip(reset&&!subscribed?2:0).Select(Actions.Row);if(reset)Replace(rows);else Rows.AddRange(rows);offset=skip+30;more.IsVisible=page.HasMore&&page.Items.Count>0;
    }
    protected override Task OnOpen(MediaRow row)=>Actions.Open(this,row.Value);
}
public sealed class PodcastPage : ListPage
{
    private readonly long id;private DjRadio? radio;
    private readonly List<DjProgram> programs=[];
    private readonly SearchField filter=new(){Placeholder="在这个播客内查找节目"};
    private readonly Button more;
    private bool hasMore,ascending;
    private int offset;
    public PodcastPage(long id):base("播客")
    {
        this.id=id;more=Ui.Button("加载更多节目",()=>Run(async()=>{await Fetch();Render();}),"tonal");Footer.Add(more);
        filter.SearchButtonPressed+=async(_,_)=>await Run(SearchAll);
        filter.TextChanged+=(_,e)=>{if(string.IsNullOrWhiteSpace(e.NewTextValue))Render();};
    }
    protected override async Task LoadAsync()
    {
        radio=await AppServices.Api.GetRadioAsync(id,Token);Token.ThrowIfCancellationRequested();Header.Clear();
        Header.Add(DesignControls.DetailHead(radio.CoverUrl,radio.Name,radio.DjName+" · "+radio.ProgramCount+"期",string.IsNullOrEmpty(radio.Category)?"PODCAST":radio.Category));
        var actions=new HorizontalStackLayout{Spacing=Ui.S(12),HorizontalOptions=LayoutOptions.Center,Margin=Ui.Inset(0,22,0,0)};
        actions.Add(Ui.Button(radio.Subscribed?"取消订阅":"订阅播客",()=>Run(async()=>{if(!await Actions.RequireAccount(this))return;await AppServices.Api.SetRadioSubscribedAsync(id,!radio.Subscribed,Token);await LoadAsync();}),"tonal","add"));
        actions.Add(Ui.Button("播放最新",()=>Run(async()=>{var latest=await AppServices.Api.GetRadioProgramsAsync(id,1,0,false,Token);if(latest.Items.Count>0)await Actions.PlayProgram(this,latest.Items[0]);}),"filled","play"));Header.Add(actions);
        if(!string.IsNullOrWhiteSpace(radio.Description)){var p=Ui.Text(radio.Description,13,"Muted");p.MaxLines=4;p.Margin=Ui.Inset(0,18,0,18);Header.Add(p);}
        Header.Add(Ui.Search(filter));Header.Add(Ui.Section("全部节目",()=>Run(async()=>{ascending=!ascending;programs.Clear();offset=0;await Fetch();Render();}),ascending?"从第一期开始":"最新优先"));
        programs.Clear();offset=0;await Fetch();Render();
    }
    private async Task Fetch()
    {
        var p=await AppServices.Api.GetRadioProgramsAsync(id,100,offset,ascending,Token);Token.ThrowIfCancellationRequested();
        programs.AddRange(p.Items);offset+=100;hasMore=p.HasMore&&p.Items.Count>0;more.IsVisible=hasMore;
    }
    private async Task SearchAll(){while(hasMore){await Fetch();}Render();}
    private void Render()
    {
        var q=filter.Text?.Trim()??"";Replace(programs.Where(p=>q.Length==0||p.Name.Contains(q,StringComparison.OrdinalIgnoreCase)).Select(Actions.Row));
        more.IsVisible=hasMore;
    }
    protected override Task OnOpen(MediaRow row)=>Play(Rows.Select(r=>((DjProgram)r.Value).Track),((DjProgram)row.Value).Track);
    protected override Task OnMenu(MediaRow row)=>Actions.TrackMenu(this,((DjProgram)row.Value).Track);
}
public sealed class ArtistPage : ListPage
{
    private readonly long id;public ArtistPage(long id):base("歌手"){this.id=id;}
    protected override async Task LoadAsync()
    {
        var a=await AppServices.Api.GetArtistAsync(id,Token);Header.Clear();
        Header.Add(DesignControls.DetailHead(a.CoverUrl,a.Name,a.SongCount+"首歌曲 · "+a.AlbumCount+"张专辑","ARTIST"));
        var actions=new HorizontalStackLayout{Spacing=Ui.S(12),HorizontalOptions=LayoutOptions.Center,Margin=Ui.Inset(0,22,0,0)};
        actions.Add(Ui.Button("播放热门",()=>Play(a.HotTracks),"filled","play"));
        actions.Add(Ui.Button("收藏歌手",()=>Run(async()=>{if(!await Actions.RequireAccount(this))return;var choice=await Actions.Sheet(this,a.Name,"收藏歌手",[("save","收藏歌手"),("remove","取消收藏")]);if(choice!=null){await AppServices.Api.SetArtistSavedAsync(id,choice=="save",Token);ShowMessage(choice=="save"?"已收藏歌手。":"已取消收藏。");}}),"tonal"));Header.Add(actions);
        Header.Add(Ui.Button("查看专辑",()=>Open(new ArtistAlbumsPage(id)),"text","album"));
        if(!string.IsNullOrWhiteSpace(a.Biography)){var desc=Ui.Text(a.Biography,13,"Muted");desc.MaxLines=3;desc.Margin=Ui.Inset(0,12);Header.Add(desc);}Header.Add(Ui.Section("热门歌曲"));
        Replace(a.HotTracks.Select(Actions.Row));
    }
    protected override Task OnOpen(MediaRow row)=>Play(Rows.Select(r=>(Track)r.Value),(Track)row.Value);
    protected override Task OnMenu(MediaRow row)=>Actions.TrackMenu(this,(Track)row.Value);
}
public sealed class ArtistAlbumsPage : ListPage
{
    private readonly long id;private int offset;private readonly Button more;
    public ArtistAlbumsPage(long id):base("歌手专辑"){this.id=id;more=Ui.Button("加载更多",()=>Run(()=>Fetch(false)),"tonal");Footer.Add(more);}
    protected override Task LoadAsync()=>Fetch(true);
    private async Task Fetch(bool reset){var skip=reset?0:offset;var page=await AppServices.Api.GetArtistAlbumsAsync(id,30,skip,Token);Token.ThrowIfCancellationRequested();var rows=page.Items.Select(Actions.Row);if(reset)Replace(rows);else Rows.AddRange(rows);offset=skip+30;more.IsVisible=page.HasMore&&page.Items.Count>0;}
    protected override Task OnOpen(MediaRow row)=>Actions.Open(this,row.Value);
}
public sealed class AlbumPage : ListPage
{
    private readonly long id;public AlbumPage(long id):base("专辑"){this.id=id;}
    protected override async Task LoadAsync()
    {
        var value=await AppServices.Api.GetAlbumAsync(id,Token);Header.Clear();
        Header.Add(DesignControls.DetailHead(value.Album.CoverUrl,value.Album.Name,string.Join(" / ",value.Artists.Select(a=>a.Name))+" · "+value.Tracks.Count+"首","ALBUM · "+value.PublishedAt.ToString("yyyy")));
        var actions=new HorizontalStackLayout{Spacing=Ui.S(12),HorizontalOptions=LayoutOptions.Center,Margin=Ui.Inset(0,22,0,0)};
        actions.Add(Ui.Button("播放全部",()=>Play(value.Tracks),"filled","play"));
        actions.Add(Ui.Button("收藏专辑",()=>Run(async()=>{if(!await Actions.RequireAccount(this))return;var choice=await Actions.Sheet(this,value.Album.Name,"收藏专辑",[("save","收藏专辑"),("remove","取消收藏")]);if(choice!=null){await AppServices.Api.SetAlbumSavedAsync(id,choice=="save",Token);ShowMessage(choice=="save"?"已收藏专辑。":"已取消收藏。");}}),"tonal"));Header.Add(actions);Header.Add(Ui.Section("曲目列表"));
        if(value.Artists.Any())Header.Add(Ui.Button("查看歌手",()=>Open(new ArtistPage(value.Artists[0].Id)),"text"));
        Replace(value.Tracks.Select(Actions.Row));
    }
    protected override Task OnOpen(MediaRow row)=>Play(Rows.Select(r=>(Track)r.Value),(Track)row.Value);
    protected override Task OnMenu(MediaRow row)=>Actions.TrackMenu(this,(Track)row.Value);
}
