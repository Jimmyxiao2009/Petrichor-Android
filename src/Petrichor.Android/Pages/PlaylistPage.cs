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
    private readonly Label selectionCount=Ui.Line("已选择 0 首",TypeRole.TitleLarge);
    private readonly Grid selectionBar;
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
        selectionBar=new Grid{ColumnDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=4,Padding=new Thickness(4,0,8,0),MinimumHeightRequest=56,IsVisible=false};
        selectionBar.SetDynamicResource(BackgroundColorProperty,"SurfaceContainer");
        selectionBar.Add(Ui.Icon("close",()=>{SetSelection(false);return Task.CompletedTask;},"退出多选",foreground:"OnSurface"));selectionBar.Add(selectionCount,1);
        selectionBar.Add(Ui.Button("全选",()=>{updatingSelection=true;try{foreach(var r in Rows)r.IsSelected=true;}finally{updatingSelection=false;UpdateSelectionCount();}return Task.CompletedTask;},"text","select"),2);
        ListContent.RowDefinitions.Insert(0,new RowDefinition(GridLength.Auto));
        Grid.SetRow(List,1);Grid.SetRow(Footer,2);ListContent.Add(selectionBar);
    }
    protected override async Task LoadAsync()
    {
        var value=await AppServices.Api.GetPlaylistAsync(id,Token);
        var tracks=await AppServices.Api.GetAllPlaylistTracksAsync(value,Token);Token.ThrowIfCancellationRequested();
        playlist=value;renderedAccount=AppServices.Api.CurrentUser?.Id??0;selecting=false;selectionBar.IsVisible=false;SuspendScrollHeader(false);PinFooter(false);ShowMini(true);Title="歌单";Header.Clear();Footer.Clear();normalHeader.Clear();
        Header.Add(DesignControls.DetailHead(value.CoverUrl,value.Name,(value.Creator?.Nickname??"网易云音乐")+" · "+tracks.Count+"首","PLAYLIST"));
        ConfigureScrollHeader(value.CoverUrl,value.Name,240);
        if(!string.IsNullOrWhiteSpace(value.Description)){var description=Ui.Text(value.Description,TypeRole.BodyMedium,"OnSurfaceVariant");description.MaxLines=3;description.LineBreakMode=LineBreakMode.TailTruncation;description.HorizontalTextAlignment=TextAlignment.Center;description.Margin=new Thickness(0,16,0,0);Header.Add(description);}
        var buttons=DesignControls.Actions(Ui.Button("播放全部",()=>Play(tracks),"filled","play"));
        if(!AppServices.Api.IsLoggedIn||value.Creator?.Id!=AppServices.Api.CurrentUser!.Id)buttons.Add(Ui.Button(value.Subscribed?"已收藏":"收藏",()=>Run(async()=>{if(await Actions.RequireAccount(this)){await AppServices.Api.SetPlaylistSubscribedAsync(id,!value.Subscribed);await LoadAsync();}}),"tonal",value.Subscribed?"check":"add"));
        Header.Add(buttons);
        Header.Add(Ui.Section(tracks.Count+" 首歌曲",()=>{SetSelection(!selecting);return Task.CompletedTask;},"多选"));
        normalHeader.AddRange(Header.Children.OfType<View>());
        var actions=new FlexLayout{Direction=Microsoft.Maui.Layouts.FlexDirection.Row,Wrap=Microsoft.Maui.Layouts.FlexWrap.Wrap,JustifyContent=Microsoft.Maui.Layouts.FlexJustify.Center};
        actions.Add(Ui.Button("播放",()=>Run(async()=>{var songs=Selected();if(songs.Count==0)throw new ArgumentException("请先选择歌曲。");await AppServices.Playback.PlayAsync(songs);}),"tonal","play"));
        actions.Add(Ui.Button("收藏",()=>Run(async()=>{if(Selected().Count==0){ShowMessage("请先选择歌曲。");return;}if(await Actions.RequireAccount(this))await Actions.ChoosePlaylist(this,Selected());}),"tonal","playlist"));
        actions.Add(Ui.Button("下载",()=>Run(async()=>{var songs=Selected();if(songs.Count==0)throw new ArgumentException("请先选择歌曲。");foreach(var t in songs)await AppServices.Offline.DownloadAsync(t);ShowMessage("所选歌曲下载完成。");}),"tonal","download"));
        if(AppServices.Api.IsLoggedIn&&value.Creator?.Id==AppServices.Api.CurrentUser!.Id)actions.Add(Ui.Button(value.SpecialType==5?"取消喜欢":"移除",()=>Run(()=>Actions.Remove(this,value,Selected(),LoadAsync)),"outlined","delete"));
        foreach(var button in actions.Children.OfType<Button>())button.Margin=new Thickness(4,0);
        selectionActions=actions;actions.IsVisible=false;Footer.Add(actions);
        Replace(tracks.Select((track,index)=>new MediaRow{Title=track.Name,Subtitle=track.ArtistText+" · "+track.Album.Name,Cover=AppServices.Offline.Cover(track),Value=track,Index=(index+1).ToString("D2")}));
        foreach(var row in Rows)row.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(MediaRow.IsSelected)&&!updatingSelection)UpdateSelectionCount();};
    }
    private void UpdateSelectionCount()=>selectionCount.Text="已选择 "+Rows.Count(r=>r.IsSelected)+" 首";
    private List<Track> Selected()=>Rows.Where(r=>r.IsSelected).Select(r=>(Track)r.Value).ToList();
    private void SetSelection(bool enabled,MediaRow? first=null)
    {
        selecting=enabled;ShowMini(!enabled);PinFooter(enabled);foreach(var view in normalHeader)view.IsVisible=!enabled;
        SuspendScrollHeader(enabled);
        if(selectionBar.IsVisible!=enabled){selectionBar.IsVisible=enabled;if(enabled)PageMotion.Reveal(selectionBar,-8);}
        if(selectionActions!=null){selectionActions.IsVisible=enabled;if(enabled)PageMotion.Reveal(selectionActions,12);}
        updatingSelection=true;try{foreach(var row in Rows){row.IsSelecting=enabled;if(!enabled)row.IsSelected=false;}if(first!=null)first.IsSelected=true;}finally{updatingSelection=false;UpdateSelectionCount();}
    }
    protected override Task OnOpen(MediaRow row)=>Play(Rows.Select(r=>(Track)r.Value),(Track)row.Value);
    protected override Task OnMenu(MediaRow row)=>Actions.TrackMenu(this,(Track)row.Value,playlist,LoadAsync,()=>SetSelection(true,row));
    protected override Task GoBack(){if(selecting){SetSelection(false);return Task.CompletedTask;}return base.GoBack();}
    protected override bool OnBackButtonPressed(){if(selecting){SetSelection(false);return true;}return base.OnBackButtonPressed();}
}
