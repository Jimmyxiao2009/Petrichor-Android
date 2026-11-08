using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;
public sealed class PodcastsPage : ListPage
{
    private bool subscribed;private int offset;
    private readonly Button more;
    private readonly Grid featured=new(){ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=12};
    private readonly List<FilterChip> chips=[];
    private readonly Label listTitle=Ui.Line("最近更新",TypeRole.TitleLarge);
    private readonly View featuredTitle=Ui.Section("值得一听");
    public PodcastsPage():base("播客")
    {
        var search=new SearchField{Placeholder="找播客，或搜节目里的歌"};search.SearchButtonPressed+=async(_,_)=>await Open(new SearchPage(search.Text,4));Header.Add(search);
        var row=new HorizontalStackLayout{Spacing=8,Padding=new Thickness(0,8)};
        for(int i=0;i<2;i++){int index=i;var chip=DesignControls.Chip(i==0?"热门":"我的订阅",()=>Run(async()=>{subscribed=index==1;for(int j=0;j<chips.Count;j++)chips[j].IsSelected=j==index;await Fetch(true);}),i==0);chips.Add(chip);row.Add(chip);}
        foreach(var name in new[]{"音乐故事","创作翻唱","情感"}){var chip=new FilterChip(name,()=>Open(new SearchPage(name,4)),showCheck:false);row.Add(chip);}
        var scroller=DesignControls.ChipScroller(row);scroller.Margin=new Thickness(-Md.Margin,8,-Md.Margin,0);Header.Add(scroller);
        Header.Add(featuredTitle);Header.Add(featured);
        listTitle.Margin=new Thickness(0,24,0,8);Header.Add(listTitle);
        more=Ui.Button("加载更多",()=>Run(()=>Fetch(false)),"outlined");more.HorizontalOptions=LayoutOptions.Center;Footer.Add(more);
    }
    protected override Task LoadAsync()=>Fetch(true);
    private async Task Fetch(bool reset)
    {
        var skip=reset?0:offset;
        var page=subscribed?await AppServices.Api.GetSubscribedRadiosAsync(30,skip,Token):await AppServices.Api.GetHotRadiosAsync(30,skip,Token);
        Token.ThrowIfCancellationRequested();if(reset){Rows.Clear();featured.Clear();featured.IsVisible=featuredTitle.IsVisible=!subscribed;listTitle.Text=subscribed?"我的订阅":"最近更新";}
        if(reset&&!subscribed)
        {
            for(int i=0;i<Math.Min(2,page.Items.Count);i++)
            {
                var radio=page.Items[i];var tile=new VerticalStackLayout{Spacing=0,InputTransparent=true};var image=new Image{Source=Ui.Art(radio.CoverUrl,400)};tile.Add(Ui.Artwork(image,Md.ShapeLarge));
                var name=Ui.Text(radio.Name,TypeRole.TitleSmall);name.MaxLines=2;name.LineBreakMode=LineBreakMode.TailTruncation;name.Margin=new Thickness(0,8,0,2);tile.Add(name);
                var about=Ui.Text(radio.Description.Length>0?radio.Description:radio.DjName,TypeRole.BodySmall,"OnSurfaceVariant");about.MaxLines=2;about.LineBreakMode=LineBreakMode.TailTruncation;tile.Add(about);
                var card=new ContentView{Content=tile};Pressable.Attach(card,()=>Open(new PodcastPage(radio.Id)),radius:Md.ShapeLarge,description:radio.Name);featured.Add(card,i);
            }
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
        this.id=id;more=Ui.Button("加载更多节目",()=>Run(async()=>{await Fetch();Render();}),"tonal");more.HorizontalOptions=LayoutOptions.Center;Footer.Add(more);
        filter.SearchButtonPressed+=async(_,_)=>await Run(SearchAll);
        filter.TextChanged+=(_,e)=>{if(string.IsNullOrWhiteSpace(e.NewTextValue))Render();};
    }
    protected override async Task LoadAsync()
    {
        radio=await AppServices.Api.GetRadioAsync(id,Token);Token.ThrowIfCancellationRequested();Header.Clear();
        Header.Add(DesignControls.DetailHead(radio.CoverUrl,radio.Name,radio.DjName+" · "+radio.ProgramCount+"期",string.IsNullOrEmpty(radio.Category)?"PODCAST":radio.Category));
        Header.Add(DesignControls.Actions(
            Ui.Button("播放最新",()=>Run(async()=>{var latest=await AppServices.Api.GetRadioProgramsAsync(id,1,0,false,Token);if(latest.Items.Count>0)await Actions.PlayProgram(this,latest.Items[0]);}),"filled","play"),
            Ui.Button(radio.Subscribed?"已订阅":"订阅",()=>Run(async()=>{if(!await Actions.RequireAccount(this))return;await AppServices.Api.SetRadioSubscribedAsync(id,!radio.Subscribed,Token);await LoadAsync();}),"tonal",radio.Subscribed?"check":"add")));
        if(!string.IsNullOrWhiteSpace(radio.Description)){var p=Ui.Text(radio.Description,TypeRole.BodyMedium,"OnSurfaceVariant");p.MaxLines=4;p.LineBreakMode=LineBreakMode.TailTruncation;p.Margin=new Thickness(0,16,0,16);Header.Add(p);}
        Header.Add(filter);Header.Add(Ui.Section("全部节目",()=>Run(async()=>{ascending=!ascending;programs.Clear();offset=0;await Fetch();Render();}),ascending?"从第一期开始":"最新优先"));
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
        Header.Add(DesignControls.DetailHead(a.CoverUrl,a.Name,a.SongCount+"首歌曲 · "+a.AlbumCount+"张专辑","ARTIST",true));
        Header.Add(DesignControls.Actions(
            Ui.Button("播放热门",()=>Play(a.HotTracks),"filled","play"),
            Ui.Button("收藏",()=>Run(async()=>{if(!await Actions.RequireAccount(this))return;var choice=await Actions.Sheet(this,a.Name,"收藏歌手",[("save","收藏歌手"),("remove","取消收藏")]);if(choice!=null){await AppServices.Api.SetArtistSavedAsync(id,choice=="save",Token);ShowMessage(choice=="save"?"已收藏歌手。":"已取消收藏。");}}),"tonal","like")));
        if(!string.IsNullOrWhiteSpace(a.Biography)){var desc=Ui.Text(a.Biography,TypeRole.BodyMedium,"OnSurfaceVariant");desc.MaxLines=3;desc.LineBreakMode=LineBreakMode.TailTruncation;desc.Margin=new Thickness(0,16,0,0);Header.Add(desc);}
        var albums=Ui.ListItem("全部专辑",a.AlbumCount+" 张专辑",()=>Open(new ArtistAlbumsPage(id)),"album");albums.Margin=new Thickness(-Md.Margin,8,-Md.Margin,0);Header.Add(albums);
        Header.Add(Ui.Section("热门歌曲"));
        Replace(a.HotTracks.Select(Actions.Row));
    }
    protected override Task OnOpen(MediaRow row)=>Play(Rows.Select(r=>(Track)r.Value),(Track)row.Value);
    protected override Task OnMenu(MediaRow row)=>Actions.TrackMenu(this,(Track)row.Value);
}
public sealed class ArtistAlbumsPage : ListPage
{
    private readonly long id;private int offset;private readonly Button more;
    public ArtistAlbumsPage(long id):base("歌手专辑"){this.id=id;more=Ui.Button("加载更多",()=>Run(()=>Fetch(false)),"tonal");more.HorizontalOptions=LayoutOptions.Center;Footer.Add(more);}
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
        Header.Add(DesignControls.Actions(
            Ui.Button("播放全部",()=>Play(value.Tracks),"filled","play"),
            Ui.Button("收藏",()=>Run(async()=>{if(!await Actions.RequireAccount(this))return;var choice=await Actions.Sheet(this,value.Album.Name,"收藏专辑",[("save","收藏专辑"),("remove","取消收藏")]);if(choice!=null){await AppServices.Api.SetAlbumSavedAsync(id,choice=="save",Token);ShowMessage(choice=="save"?"已收藏专辑。":"已取消收藏。");}}),"tonal","like")));
        if(value.Artists.Any()){var artist=Ui.ListItem(value.Artists[0].Name,"歌手",()=>Open(new ArtistPage(value.Artists[0].Id)),"person");artist.Margin=new Thickness(-Md.Margin,8,-Md.Margin,0);Header.Add(artist);}
        Header.Add(Ui.Section("曲目列表"));
        Replace(value.Tracks.Select(Actions.Row));
    }
    protected override Task OnOpen(MediaRow row)=>Play(Rows.Select(r=>(Track)r.Value),(Track)row.Value);
    protected override Task OnMenu(MediaRow row)=>Actions.TrackMenu(this,(Track)row.Value);
}
