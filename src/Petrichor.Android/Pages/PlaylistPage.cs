using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class PlaylistPage : ListPage
{
    private readonly long id;
    private Playlist? playlist;
    private bool selecting;
    private long renderedAccount=-1;
    private bool updatingSelection;
    private Label? selectionCount;
    private readonly VerticalStackLayout selectionBar=new(){Spacing=Ui.S(8),Padding=Ui.Inset(24,8),IsVisible=false};
    private View? selectionActions;
    private readonly List<View> normalHeader=[];
    // Mutations explicitly reload the playlist. Returning from a song menu or
    // player should preserve its rows and scroll position.
    protected override bool RefreshOnAppear=>renderedAccount!=(AppServices.Api.CurrentUser?.Id??0);
    public PlaylistPage(long id):base("歌单")
    {
        this.id=id;
        // Selection controls must stay visible when the detail header collapses
        // or the user scrolls through a long playlist.
        ListContent.RowDefinitions.Insert(0,new RowDefinition(GridLength.Auto));
        Grid.SetRow(List,1);Grid.SetRow(Footer,2);ListContent.Add(selectionBar);
        List.Scrolled+=(_,e)=>{if(!selecting)UpdateScrollHeader(e.VerticalOffset);};
    }
    protected override async Task LoadAsync()
    {
        var value=await AppServices.Api.GetPlaylistAsync(id,Token);
        var tracks=await AppServices.Api.GetAllPlaylistTracksAsync(value,Token);Token.ThrowIfCancellationRequested();
        playlist=value;renderedAccount=AppServices.Api.CurrentUser?.Id??0;selecting=false;selectionBar.IsVisible=false;selectionBar.Clear();PinFooter(false);ShowMini(true);Title="歌单";Header.Clear();Footer.Clear();normalHeader.Clear();
        Header.Add(DesignControls.DetailHead(value.CoverUrl,value.Name,(value.Creator?.Nickname??"网易云音乐")+" · "+tracks.Count+"首","PLAYLIST"));
        ConfigureScrollHeader(value.CoverUrl,value.Name,Ui.S(180));
        if(!string.IsNullOrWhiteSpace(value.Description)){var description=Ui.Text(value.Description,13,"Muted");description.MaxLines=3;description.HorizontalTextAlignment=TextAlignment.Center;description.Margin=Ui.Inset(0,18,0,0);Header.Add(description);}
        var buttons=new HorizontalStackLayout{Spacing=Ui.S(12),HorizontalOptions=LayoutOptions.Center,Margin=Ui.Inset(0,22,0,0)};
        buttons.Add(Ui.Button("播放全部",()=>Play(tracks),"filled","play"));
        if(!AppServices.Api.IsLoggedIn||value.Creator?.Id!=AppServices.Api.CurrentUser!.Id)buttons.Add(Ui.Button(value.Subscribed?"取消收藏":"收藏歌单",()=>Run(async()=>{if(await Actions.RequireAccount(this)){await AppServices.Api.SetPlaylistSubscribedAsync(id,!value.Subscribed);await LoadAsync();}}),"tonal"));Header.Add(buttons);
        Header.Add(Ui.Section(tracks.Count+"首歌曲",()=>{SetSelection(!selecting);return Task.CompletedTask;},"多选"));
        normalHeader.AddRange(Header.Children.OfType<View>());
        selectionCount=Ui.Text("已选择0首",28);selectionBar.Add(selectionCount);
        var first=new HorizontalStackLayout{Spacing=Ui.S(8)};first.Add(Ui.Button("全选",()=>{updatingSelection=true;try{foreach(var r in Rows)r.IsSelected=true;}finally{updatingSelection=false;UpdateSelectionCount();}return Task.CompletedTask;},"text"));first.Add(Ui.Button("完成",()=>{SetSelection(false);return Task.CompletedTask;},"text"));selectionBar.Add(first);
        var actions=new FlexLayout{Direction=FlexDirection.Row,Wrap=FlexWrap.Wrap};
        actions.Add(Ui.Button("播放",()=>Run(async()=>{var songs=Selected();if(songs.Count==0)throw new ArgumentException("请先选择歌曲。");await AppServices.Playback.PlayAsync(songs);}),"tonal"));
        actions.Add(Ui.Button("收藏",()=>Run(async()=>{if(Selected().Count==0){ShowMessage("请先选择歌曲。");return;}if(await Actions.RequireAccount(this))await Actions.ChoosePlaylist(this,Selected());}),"tonal"));
        actions.Add(Ui.Button("下载",()=>Run(async()=>{var songs=Selected();if(songs.Count==0)throw new ArgumentException("请先选择歌曲。");foreach(var t in songs)await AppServices.Offline.DownloadAsync(t);ShowMessage("所选歌曲下载完成。");}),"tonal"));
        if(AppServices.Api.IsLoggedIn&&value.Creator?.Id==AppServices.Api.CurrentUser!.Id)actions.Add(Ui.Button(value.SpecialType==5?"取消喜欢":"移除",()=>Run(()=>Actions.Remove(this,value,Selected(),LoadAsync)),"outlined"));
        foreach(var button in actions.Children.OfType<Button>())button.Margin=Ui.Inset(4,0);
        selectionActions=actions;actions.IsVisible=false;Footer.Add(actions);
        Replace(tracks.Select((track,index)=>new MediaRow{Title=track.Name,Subtitle=track.ArtistText+" · "+track.Album.Name,Cover=AppServices.Offline.Cover(track),Value=track,Index=(index+1).ToString("D2")}));
        foreach(var row in Rows)row.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(MediaRow.IsSelected)&&!updatingSelection)UpdateSelectionCount();};
    }
    private void UpdateSelectionCount(){if(selectionCount!=null)selectionCount.Text="已选择"+Rows.Count(r=>r.IsSelected)+"首";}
    private List<Track> Selected()=>Rows.Where(r=>r.IsSelected).Select(r=>(Track)r.Value).ToList();
    private void SetSelection(bool enabled,MediaRow? first=null)
    {
        selecting=enabled;Title=enabled?"选择歌曲":"歌单";ShowMini(!enabled);PinFooter(enabled);foreach(var view in normalHeader)view.IsVisible=!enabled;if(selectionBar!=null)selectionBar.IsVisible=enabled;if(selectionActions!=null)selectionActions.IsVisible=enabled;
        UpdateScrollHeader(0);
        updatingSelection=true;try{foreach(var row in Rows){row.IsSelecting=enabled;if(!enabled)row.IsSelected=false;}if(first!=null)first.IsSelected=true;}finally{updatingSelection=false;UpdateSelectionCount();}
    }
    protected override Task OnOpen(MediaRow row)=>Play(Rows.Select(r=>(Track)r.Value),(Track)row.Value);
    protected override Task OnMenu(MediaRow row)=>Actions.TrackMenu(this,(Track)row.Value,playlist,LoadAsync,()=>SetSelection(true,row));
    protected override Task GoBack(){if(selecting){SetSelection(false);return Task.CompletedTask;}return base.GoBack();}
    protected override bool OnBackButtonPressed(){if(selecting){SetSelection(false);return true;}return base.OnBackButtonPressed();}
}
