using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

// Playback composition adapted from Mooic 3.0.1 Player / Lyrics UI (GPL-3.0).
// Playback, stream permissions, queue, comments and storage remain Petrichor codebehind.
public sealed class PlayerPage : BasePage
{
    private readonly Image art=Ui.Cover("forest.png"),lyricArt=Ui.Cover("forest.png");
    private readonly Label title=MooicLayout.Text("",16,bold:true),artist=MooicLayout.Text("",14,"Muted"),hint=MooicLayout.Text("",12,"Muted"),quality=MooicLayout.Text("",11,"Muted");
    private readonly Label lyricTitle=MooicLayout.Text("",16),lyricArtist=MooicLayout.Text("",14,"Muted");
    private readonly MusicSlider seek=new(){Minimum=0,Maximum=1,HeightRequest=48};
    private readonly Label seekPreview=MooicLayout.Text("",12,"OnPrimary",true);
    private readonly Border previewBubble;
    private double? pendingSeek;
    private long seekDeadline,seekRevision,scrubTrackId;
    private readonly Button toggle,shuffle,repeat,like;
    private readonly PlayerDeck deck;
    private readonly Label functionQuality=MooicLayout.Text("",12,"Muted"),functionSleep=MooicLayout.Text("",12,"Muted"),functionLike=MooicLayout.Text("",12,"Muted");
    private readonly PlayerQueueView queueView;
    private readonly ContentView functionsHost=new(),queueHost=new();
    private bool functionsReady;
    private PlayerPane lyricReturnPane;
    private bool entryReady;
    private long lyricRevision;
    private bool isLiked;
    private long likeRevision;

    private readonly Label elapsed=MooicLayout.Text("0:00",12,"Muted",true),duration=MooicLayout.Text("0:00",12,"Muted",true);
    private readonly VerticalStackLayout lyricLines=new(){Spacing=24,Padding=new Thickness(16,24,16,100)};
    private readonly ScrollView lyricScroll;
    private readonly Border cover,smallCover;
    private readonly Grid main,lyricPane;
    private readonly List<Label> lyricLabels=[];
    private Lyrics? lyrics;
    private bool dragging,showLyrics,writingSeek,changingState;
    private int active=-2;
    private RepeatMode? renderedRepeat;
    private bool? renderedShuffle;
    private long lastId=long.MinValue;
    internal View TransitionCover=>showLyrics?smallCover:cover;
    public PlayerPage():base("正在播放",showMini:false)
    {
        main=new Grid{Padding=new Thickness(24,0,24,20),RowDefinitions=[new(GridLength.Auto),new(GridLength.Auto),new(GridLength.Auto),new(GridLength.Auto),new(GridLength.Auto),new(GridLength.Star)]};
        var hero=new Grid{Margin=new Thickness(0,24,0,24)};
        art.WidthRequest=-1;art.HeightRequest=-1;art.Clip=null;
        cover=Ui.Card(art,radius:12,padding:0);cover.HorizontalOptions=LayoutOptions.Center;cover.VerticalOptions=LayoutOptions.Center;
        hero.Add(cover);main.Add(hero);
        var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await ToggleLyrics();cover.GestureRecognizers.Add(tap);SemanticProperties.SetDescription(cover,"点击封面显示歌词");
        var song=new Grid{ColumnDefinitions=[new(GridLength.Star),new(48),new(48)],ColumnSpacing=8,MinimumHeightRequest=48};
        var caption=new VerticalStackLayout{Spacing=3,VerticalOptions=LayoutOptions.Center};title.MaxLines=1;title.LineBreakMode=LineBreakMode.TailTruncation;artist.MaxLines=1;artist.LineBreakMode=LineBreakMode.TailTruncation;caption.Add(title);caption.Add(artist);quality.IsVisible=false;caption.Add(quality);song.Add(caption);
        like=MooicLayout.Icon("like",()=>Run(ToggleLike),"喜欢或取消喜欢",area:48,background:"SurfaceHigh",foreground:"Primary");song.Add(like,1);
        song.Add(MooicLayout.Icon("more",()=>Run(OnToolbar,false),"播放选项",area:48,background:"SurfaceHigh",foreground:"OnSurface"),2);main.Add(song,0,1);
        hint.IsVisible=false;hint.Margin=new Thickness(0,6,0,0);main.Add(hint,0,2);
        seek.SetDynamicResource(Slider.MinimumTrackColorProperty,"Primary");seek.SetDynamicResource(Slider.ThumbColorProperty,"Primary");seek.SetDynamicResource(Slider.MaximumTrackColorProperty,"SurfaceHigh");DesignControls.StyleSlider(seek);
        SemanticProperties.SetDescription(seek,"播放进度");
        var seekArea=new Grid{RowDefinitions=[new(20),new(48)]};
        seekPreview.HorizontalTextAlignment=TextAlignment.Center;seekPreview.VerticalTextAlignment=TextAlignment.Center;
        previewBubble=new Border{Content=seekPreview,WidthRequest=56,HeightRequest=20,HorizontalOptions=LayoutOptions.Start,StrokeThickness=0,Padding=0,IsVisible=false,InputTransparent=true,StrokeShape=new Microsoft.Maui.Controls.Shapes.RoundRectangle{CornerRadius=10}};previewBubble.SetDynamicResource(BackgroundColorProperty,"Primary");seekArea.Add(previewBubble);seekArea.Add(seek,0,1);main.Add(seekArea,0,3);
        seek.ValueChanged+=(_,e)=>{if(writingSeek)return;if(dragging)PreviewSeek();else CommitSeek(e.NewValue);};
        seek.DragStarted+=(_,_)=>{dragging=true;scrubTrackId=AppServices.Playback.Queue.Current?.Id??0;pendingSeek=null;seek.WaitingForSeek=false;seekRevision++;previewBubble.IsVisible=true;PreviewSeek();};
        seek.DragCompleted+=(_,_)=>{dragging=false;previewBubble.IsVisible=false;if(seek.GestureCancelled||scrubTrackId!=AppServices.Playback.Queue.Current?.Id)Update();else CommitSeek(seek.Value);};
        seek.SizeChanged+=(_,_)=>{if(dragging)PreviewSeek();};
        var times=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)]};times.Add(elapsed);times.Add(duration,1);main.Add(times,0,4);
        var controls=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star),new(72),new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=8,VerticalOptions=LayoutOptions.Center,MinimumHeightRequest=88};
        shuffle=MooicLayout.Icon("shuffle",async()=>{var q=AppServices.Playback.Queue;q.Shuffle=!q.Shuffle;UpdateModes();await ModeFeedback(shuffle!);},"随机播放",area:48,background:"Transparent");controls.Add(shuffle);
        controls.Add(MooicLayout.Icon("previous",()=>Run(AppServices.Playback.PreviousAsync),"上一首",area:48,background:"SurfaceHigh"),1);
        toggle=MooicLayout.Icon("play",()=>Run(async()=>{if(AppServices.Playback.Queue.Current==null)await Ui.Tab("search");else await AppServices.Playback.ToggleAsync();}),"播放或暂停",size:36,area:72,background:"Primary",foreground:"OnPrimary");controls.Add(toggle,2);
        controls.Add(MooicLayout.Icon("next",()=>Run(()=>AppServices.Playback.NextAsync()),"下一首",area:48,background:"SurfaceHigh"),3);
        repeat=MooicLayout.Icon("repeat",async()=>{var q=AppServices.Playback.Queue;q.Repeat=(RepeatMode)(((int)q.Repeat+1)%3);UpdateModes();await ModeFeedback(repeat!);},"循环模式",area:48,background:"Transparent");controls.Add(repeat,4);main.Add(controls,0,5);
        lyricPane=new Grid{IsVisible=false,RowDefinitions=[new(GridLength.Auto),new(GridLength.Star)]};
        var lyricHeader=new Grid{ColumnDefinitions=[new(72),new(GridLength.Star),new(48)],Padding=new Thickness(12,12,12,12),ColumnSpacing=12};
        lyricArt.WidthRequest=72;lyricArt.HeightRequest=72;lyricArt.Clip=null;smallCover=Ui.Card(lyricArt,radius:12,padding:0);lyricHeader.Add(smallCover);
        var lyricCaption=new VerticalStackLayout{Spacing=4,VerticalOptions=LayoutOptions.Center};lyricTitle.MaxLines=1;lyricArtist.MaxLines=1;lyricCaption.Add(lyricTitle);lyricCaption.Add(lyricArtist);lyricHeader.Add(lyricCaption,1);
        lyricHeader.Add(MooicLayout.Icon("down",ToggleLyrics,"返回封面"),2);lyricPane.Add(lyricHeader);
        lyricScroll=new ScrollView{Content=lyricLines,VerticalScrollBarVisibility=ScrollBarVisibility.Never};lyricPane.Add(lyricScroll,0,1);
        var playerScroll=Scroll(main);
        queueView=new PlayerQueueView(this);
        deck=new PlayerDeck(playerScroll,functionsHost,queueHost);deck.StateChanged+=pane=>{PrepareFunctions();SetPlayerHeader(pane);if(pane==PlayerPane.Queue)queueView.Refresh(true);};
        void SizeCover(){if(deck.Height<=0)return;main.HeightRequest=Math.Max(580,deck.Height);var side=Math.Max(1,Math.Min(400,Math.Min(main.Width-48,deck.Height*.47)));if(Math.Abs(cover.WidthRequest-side)>.5){cover.WidthRequest=side;cover.HeightRequest=side;}}
        main.SizeChanged+=(_,_)=>SizeCover();deck.SizeChanged+=(_,_)=>SizeCover();
        var root=new Grid();root.Add(deck);root.Add(lyricPane);Body.Content=root;UpdateModes();Update();
    }
    private void UpdateModes()
    {
        var q=AppServices.Playback.Queue;if(renderedRepeat==q.Repeat&&renderedShuffle==q.Shuffle)return;renderedRepeat=q.Repeat;renderedShuffle=q.Shuffle;
        shuffle.SetDynamicResource(Button.BackgroundColorProperty,q.Shuffle?"SecondaryContainer":"Transparent");
        shuffle.SetDynamicResource(Button.TextColorProperty,q.Shuffle?"OnSecondaryContainer":"Muted");shuffle.Opacity=1;SemanticProperties.SetDescription(shuffle,q.Shuffle?"随机播放已开启":"随机播放已关闭");
        repeat.Text=Ui.Glyphs[q.Repeat==RepeatMode.One?"repeatone":"repeat"];repeat.Opacity=1;
        repeat.SetDynamicResource(Button.BackgroundColorProperty,q.Repeat==RepeatMode.Off?"Transparent":"SecondaryContainer");
        repeat.SetDynamicResource(Button.TextColorProperty,q.Repeat==RepeatMode.Off?"Muted":"OnSecondaryContainer");
        SemanticProperties.SetDescription(repeat,q.Repeat switch{RepeatMode.One=>"单曲循环",RepeatMode.All=>"列表循环",_=>"不循环"});
    }
    private static Task ModeFeedback(Button button)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if(!PageMotion.Enabled)return Task.CompletedTask;
        new Animation(t=>{button.Scale=1-.15*Math.Sin(Math.PI*t);button.Rotation=12*Math.Sin(Math.PI*2*t);}).Commit(button,"ModeFeedback",16,220,Easing.CubicOut,(_,_)=>{button.Scale=1;button.Rotation=0;done.TrySetResult();});return done.Task;
    }
    private async Task ToggleLyrics()
    {
        if(changingState)return;changingState=true;
        using var artwork=(!showLyrics&&deck.Pane==PlayerPane.Player||showLyrics&&lyricReturnPane==PlayerPane.Player)?ArtworkTransition.Capture(TransitionCover):null;
        try{
            if(!showLyrics)lyricReturnPane=deck.Pane;
            showLyrics=!showLyrics;deck.IsVisible=!showLyrics;main.IsVisible=!showLyrics;lyricPane.IsVisible=showLyrics;SetHeaderVisible(!showLyrics);
            var pane=showLyrics?(View)lyricPane:deck;pane.Opacity=.2;await Task.Delay(16);
            if(artwork!=null)await artwork.PrepareAsync(TransitionCover);
            await Task.WhenAll(PageMotion.Animate(pane,true,false),artwork?.AnimateTo(TransitionCover)??Task.CompletedTask);
            if(showLyrics&&lyrics==null&&AppServices.Playback.Queue.Current is{} track)_=LoadLyric(track);
            if(showLyrics&&active>=0&&active<lyricLabels.Count)await lyricScroll.ScrollToAsync(lyricLabels[active],ScrollToPosition.Center,false);
        }finally{main.Opacity=deck.Opacity=lyricPane.Opacity=1;changingState=false;}
    }
    protected override Task GoBack()=>showLyrics?ToggleLyrics():deck.Pane!=PlayerPane.Player?deck.ShowPane(PlayerPane.Player):base.GoBack();
    protected override void OnAppearing(){base.OnAppearing();AppServices.Playback.Changed+=Update;Update();_=AfterEntry();ThemeColors.Apply(MainActivity.Instance);}
    private void PrepareFunctions(){if(functionsReady)return;functionsReady=true;functionsHost.Content=BuildFunctions();queueHost.Content=queueView;}
    private async Task AfterEntry(){try{await PageMotion.WaitForEntryAsync(this,Token);Token.ThrowIfCancellationRequested();entryReady=true;PrepareFunctions();_=LoadLikeState();}catch(OperationCanceledException){}}
    protected override void OnDisappearing(){AppServices.Playback.Changed-=Update;entryReady=false;lyricRevision++;dragging=false;previewBubble.IsVisible=false;pendingSeek=null;seek.WaitingForSeek=false;seekRevision++;base.OnDisappearing();}
    private void PreviewSeek()
    {
        seekPreview.Text=Ui.Duration((int)seek.Value);elapsed.Text=seekPreview.Text;SemanticProperties.SetDescription(previewBubble,"预览进度 "+seekPreview.Text);
        var fraction=(seek.Value-seek.Minimum)/Math.Max(1,seek.Maximum-seek.Minimum);
        previewBubble.TranslationX=Math.Clamp(12+Math.Max(0,seek.Width-24)*fraction-28,0,Math.Max(0,seek.Width-56));
    }
    private void CommitSeek(double value)
    {
        var p=AppServices.Playback;if(p.Player==null){Update();return;}
        pendingSeek=Math.Clamp(value,0,p.Duration.TotalMilliseconds);seekDeadline=Environment.TickCount64+1000;seek.WaitingForSeek=true;var revision=++seekRevision;
        p.Seek(TimeSpan.FromMilliseconds(pendingSeek.Value));
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1100),()=>{if(revision!=seekRevision)return;pendingSeek=null;seek.WaitingForSeek=false;Update();});
    }
    private void Update()
    {
        var p=AppServices.Playback;var t=p.Queue.Current;toggle.Text=Ui.Glyphs[p.IsPlaying?"pause":"play"];if(t==null)return;
        title.Text=lyricTitle.Text=t.Name;artist.Text=lyricArtist.Text=t.ArtistText;if(deck.Pane==PlayerPane.Queue)queueView.Refresh();
        functionQuality.Text=p.QualityLabel;
        var remaining=p.Sleep.Remaining(DateTimeOffset.Now);functionSleep.Text=remaining.HasValue?"剩余 "+(int)Math.Ceiling(remaining.Value.TotalMinutes)+" 分钟":p.Sleep.WaitingForTrackEnd?"听完当前歌曲":"关闭";
        quality.Text=p.QualityLabel;quality.IsVisible=p.CurrentStream!=null;UpdateModes();
        hint.Text=p.IsLoading?"正在加载…":p.Error??(p.IsPreview?"当前账号只能试听此曲目。":p.QualityLimited?"当前资源未提供无损，实际播放 "+p.QualityLabel:"");hint.IsVisible=!string.IsNullOrEmpty(hint.Text);
        if(t.Id!=lastId){pendingSeek=null;seek.WaitingForSeek=false;seekRevision++;}
        if(pendingSeek.HasValue&&(Math.Abs(p.Position.TotalMilliseconds-pendingSeek.Value)<600||Environment.TickCount64>=seekDeadline)){pendingSeek=null;seek.WaitingForSeek=false;}
        seek.IsEnabled=p.Player!=null&&!p.IsLoading;
        if(!dragging){writingSeek=true;try{seek.Maximum=Math.Max(1,p.Duration.TotalMilliseconds);seek.Value=Math.Clamp(pendingSeek??p.Position.TotalMilliseconds,0,seek.Maximum);}finally{writingSeek=false;}}
        elapsed.Text=Ui.Duration((int)(dragging?seek.Value:pendingSeek??p.Position.TotalMilliseconds));duration.Text=Ui.Duration((int)p.Duration.TotalMilliseconds);
        if(t.Id!=lastId){lastId=t.Id;art.Source=lyricArt.Source=Ui.Art(AppServices.Offline.Cover(t),800);if(entryReady)_=LoadLikeState();lyrics=null;lyricLines.Clear();lyricLabels.Clear();active=-2;lyricLines.Add(MooicLayout.Text("正在加载歌词…",16,"Muted"));if(showLyrics)_=LoadLyric(t);return;}
        if(lyrics==null)return;var next=LrcParser.FindActive(lyrics.Lines,p.Position);if(next==active)return;
        if(active>=0&&active<lyricLabels.Count){lyricLabels[active].SetDynamicResource(Label.TextColorProperty,"Primary");lyricLabels[active].Opacity=.45;}
        active=next;if(active>=0&&active<lyricLabels.Count){lyricLabels[active].SetDynamicResource(Label.TextColorProperty,"Primary");lyricLabels[active].Opacity=1;if(showLyrics)_=lyricScroll.ScrollToAsync(lyricLabels[active],ScrollToPosition.Center,true);}
    }
    private View BuildFunctions()
    {
        var content=new VerticalStackLayout{SafeAreaEdges=SafeAreaEdges.None,Spacing=16,Padding=new Thickness(16,8,16,24),MaximumWidthRequest=640,HorizontalOptions=LayoutOptions.Fill};
        var tiles=new Grid{SafeAreaEdges=SafeAreaEdges.None,ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],RowDefinitions=[new(GridLength.Auto),new(GridLength.Auto)],ColumnSpacing=12,RowSpacing=12};
        tiles.Add(PlayerFunctions.Tile("收藏到歌单","playlist",()=>Run(()=>ExecutePlayerAction("add"))));
        tiles.Add(PlayerFunctions.Tile("下载歌曲","download",()=>Run(()=>ExecutePlayerAction("download"))),1);
        tiles.Add(PlayerFunctions.Tile("评论","comments",()=>Run(()=>ExecutePlayerAction("comments"))),0,1);
        tiles.Add(PlayerFunctions.Tile("分享歌曲","share",()=>Run(()=>ExecutePlayerAction("share"))),1,1);content.Add(tiles);
        var settings=new VerticalStackLayout{Spacing=4};
        settings.Add(PlayerFunctions.Row("正在播放","queue",()=>Run(()=>ExecutePlayerAction("queue"))));
        settings.Add(PlayerFunctions.Row("下一首播放","next",()=>Run(()=>ExecutePlayerAction("next"))));
        settings.Add(PlayerFunctions.Row("喜欢 / 取消喜欢","like",()=>Run(ToggleLike),functionLike));
        settings.Add(PlayerFunctions.Row("播放音质","equalizer",()=>Run(()=>ExecutePlayerAction("quality")),functionQuality));
        settings.Add(PlayerFunctions.Row("睡眠定时","sleep",()=>Run(()=>ExecutePlayerAction("sleep")),functionSleep));
        settings.Add(PlayerFunctions.Row("查看歌手","person",()=>Run(()=>ExecutePlayerAction("artist"))));
        settings.Add(PlayerFunctions.Row("查看专辑","album",()=>Run(()=>ExecutePlayerAction("album"))));
        settings.Add(PlayerFunctions.Row("歌词","lyrics",()=>Run(()=>ExecutePlayerAction("lyrics"))));content.Add(settings);
        return Scroll(content);
    }
    private void UpdateLike(){functionLike.Text=isLiked?"已喜欢":"未喜欢";like.Text=Ui.Glyphs[isLiked?"liked":"like"];like.SetDynamicResource(Button.BackgroundColorProperty,isLiked?"PrimaryContainer":"SurfaceHigh");SemanticProperties.SetDescription(like,isLiked?"取消喜欢":"喜欢或取消喜欢");}
    private async Task LoadLikeState(){
        var revision=++likeRevision;var track=AppServices.Playback.Queue.Current;isLiked=false;UpdateLike();
        if(track==null||track.Id<=0||!AppServices.Api.IsLoggedIn)return;
        try{var ids=await AppServices.Api.GetLikedTrackIdsAsync(Token);if(revision==likeRevision&&AppServices.Playback.Queue.Current?.Id==track.Id){isLiked=ids.Contains(track.Id);UpdateLike();}}catch(OperationCanceledException){}catch{}
    }
    private async Task ToggleLike(){
        var track=AppServices.Playback.Queue.Current;if(track==null)return;
        if(track.Id<=0){ShowMessage("本地音频暂不支持云端喜欢。");return;}
        if(!await Actions.RequireAccount(this))return;
        var liked=(await AppServices.Api.GetLikedTrackIdsAsync(Token)).Contains(track.Id);await AppServices.Api.SetLikedAsync(track.Id,!liked);if(AppServices.Playback.Queue.Current?.Id!=track.Id)return;likeRevision++;isLiked=!liked;UpdateLike();await ModeFeedback(like);
    }
    protected override Task OnToolbar()=>deck.ShowFunctions(true);
    private async Task ExecutePlayerAction(string choice)
    {
        var p=AppServices.Playback;var track=p.Queue.Current;if(track==null)return;
        switch(choice){
            case "add":if(await Actions.RequireAccount(this))await Actions.ChoosePlaylist(this,[track]);break;
            case "quality":var value=await Actions.Sheet(this,"播放音质","显示实际返回的音频格式。",[("128000","标准 · 128 kbps"),("192000","高品质 · 192 kbps"),("320000","极高 · 320 kbps"),("999000","无损 · FLAC"),("1999000","Hi-Res · FLAC")]);if(value!=null){Preferences.Set("quality",int.Parse(value));await p.PlayCurrentAsync();}break;
            case "download":if(track.Id>0){await AppServices.Offline.DownloadAsync(track);ShowMessage("下载完成。");}else ShowMessage("这首歌已保存在本机。");break;
            case "comments":if(track.Id>0)await Open(new CommentsPage(track));else ShowMessage("本地音频暂无在线评论。");break;
            case "lyrics":if(!showLyrics)await ToggleLyrics();break;
            case "queue":await deck.ShowPane(PlayerPane.Queue);break;
            case "next":p.Queue.InsertNext([track]);p.Notify();ShowMessage("已加入下一首。");break;
            case "share":if(track.Id>0)await Share.Default.RequestAsync(new ShareTextRequest{Title="分享歌曲",Text=track.Name+" · "+track.ArtistText,Uri="https://music.163.com/song?id="+track.Id});else ShowMessage("本地音频暂无云端分享链接。");break;
            case "artist":
                var artists=track.Artists.Where(a=>a.Id>0).ToList();
                if(artists.Count==1)await Open(new ArtistPage(artists[0].Id));
                else if(artists.Count>1){var selected=await Actions.Sheet(this,"查看歌手","",artists.Select(a=>(a.Id.ToString(),a.Name)));if(long.TryParse(selected,out var artistId))await Open(new ArtistPage(artistId));}
                else ShowMessage("这首音频暂无歌手页面。");break;
            case "album":if(track.Album?.Id>0)await Open(new AlbumPage(track.Album.Id));else ShowMessage("这首音频暂无专辑页面。");break;
            case "sleep":await SettingsPage.SleepTimer(this);break;
            case "track":await Actions.TrackMenu(this,track);break;
        }
    }
    private async Task LoadLyric(Track track)
    {
        var revision=++lyricRevision;
        try{
            var value=await AppServices.Offline.GetLyricsAsync(track);
            if(lastId!=track.Id||revision!=lyricRevision||!showLyrics||Token.IsCancellationRequested)return;
            lyricLines.Clear();lyricLabels.Clear();
            if(value.Lines.Count==0)lyricLines.Add(MooicLayout.Text("暂无歌词，享受音乐。",18,"Muted"));
            for(var i=0;i<value.Lines.Count;i++){
                if(revision!=lyricRevision||!showLyrics||Token.IsCancellationRequested)return;
                var line=value.Lines[i];var label=MooicLayout.Text(line.Text,23,"Primary",true);label.Opacity=.45;lyricLabels.Add(label);lyricLines.Add(label);var tap=new TapGestureRecognizer();tap.Tapped+=(_,_)=>AppServices.Playback.Seek(line.Position);label.GestureRecognizers.Add(tap);
                if(i%16==15)await Task.Delay(16);
            }
            lyrics=value;active=-2;Update();
        }catch{if(lastId==track.Id&&revision==lyricRevision){lyricLines.Clear();lyricLines.Add(MooicLayout.Text("歌词暂时不可用。",16,"Muted"));}}
    }

}
public sealed class LyricsPage : BasePage
{
    private readonly ScrollView scroll;
    private readonly VerticalStackLayout lines=new(){Spacing=Ui.S(24),Padding=Ui.Inset(24,32)};
    private readonly Switch follow=new(){IsToggled=true};
    private readonly List<Label> labels=[];
    private Lyrics? lyrics;
    private int active=-2;
    private long trackId=long.MinValue;
    public LyricsPage():base("歌词")
    {
        SemanticProperties.SetDescription(follow,"跟随歌词");
        scroll=new(){Content=lines,VerticalScrollBarVisibility=ScrollBarVisibility.Never};Body.Content=scroll;
    }
    protected override void OnAppearing(){base.OnAppearing();AppServices.Playback.Changed+=Update;Update();}
    protected override void OnDisappearing(){AppServices.Playback.Changed-=Update;base.OnDisappearing();}
    private void Update()
    {
        var p=AppServices.Playback;var track=p.Queue.Current;
        if(track==null){if(lines.Count==0)lines.Add(Ui.Empty("还没有正在播放的歌曲","选择一首歌，歌词会在这里同步显示。"));return;}
        if(track.Id!=trackId){trackId=track.Id;_ = Run(()=>LoadLyrics(track));return;}
        if(lyrics==null)return;
        var next=LrcParser.FindActive(lyrics.Lines,p.Position);if(next==active)return;
        if(active>=0&&active<labels.Count){labels[active].SetDynamicResource(Label.TextColorProperty,"Muted");labels[active].FontSize=Ui.F(24);}
        active=next;
        if(active>=0&&active<labels.Count){labels[active].SetDynamicResource(Label.TextColorProperty,"Primary");labels[active].FontSize=Ui.F(28);if(follow.IsToggled)_ = scroll.ScrollToAsync(labels[active],ScrollToPosition.Center,true);}
    }
    private async Task LoadLyrics(Track track)
    {
        var value=await AppServices.Offline.GetLyricsAsync(track);if(track.Id!=trackId)return;
        lyrics=value;labels.Clear();lines.Clear();active=-2;
        var caption=Ui.Text(track.Name+" · "+track.ArtistText,11,"Muted");caption.CharacterSpacing=1.2;caption.Margin=Ui.Inset(0,0,0,8);lines.Add(caption);
        if(value.Lines.Count==0){var cover=Ui.Cover(AppServices.Offline.Cover(track),300);cover.HorizontalOptions=LayoutOptions.Center;lines.Add(cover);lines.Add(Ui.Text("暂无歌词，享受音乐。",14,"Muted"));return;}
        var translations=value.TranslatedLines.GroupBy(l=>l.Position).ToDictionary(g=>g.Key,g=>g.First().Text);
        foreach(var line in value.Lines)
        {
            var label=Ui.Text(line.Text,24,"Muted");labels.Add(label);lines.Add(label);
            if(translations.TryGetValue(line.Position,out var text))lines.Add(Ui.Text(text,14,"Muted"));
            var tap=new TapGestureRecognizer();tap.Tapped+=(_,_)=>AppServices.Playback.Seek(line.Position);label.GestureRecognizers.Add(tap);
        }
        Update();
    }
    protected override async Task OnToolbar(){var value=await Actions.Sheet(this,"歌词设置","点击歌词可以跳转到对应位置。",[("follow",follow.IsToggled?"关闭歌词跟随":"跟随当前歌词")]);if(value=="follow")follow.IsToggled=!follow.IsToggled;}
}
public sealed class QueuePage : ListPage
{
    private readonly Label count=Ui.Text("",12,"OnPrimaryContainer");
    protected override bool RefreshOnAppear=>true;
    public QueuePage():base("播放队列")
    {
        var heading=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Margin=Ui.Inset(0,8,0,24)};heading.Add(Ui.Text("接下来播放",28));heading.Add(Ui.Card(count,"PrimaryContainer",16,Ui.Inset(12,4)),1);Header.Add(heading);
        var modes=new HorizontalStackLayout{Spacing=Ui.S(8),Margin=Ui.Inset(0,0,0,16)};
        var repeat=DesignControls.Chip("列表循环",()=>{AppServices.Playback.Queue.Repeat=(RepeatMode)(((int)AppServices.Playback.Queue.Repeat+1)%3);ShowMessage(AppServices.Playback.Queue.Repeat switch{RepeatMode.One=>"单曲循环",RepeatMode.All=>"列表循环",_=>"播放到末尾停止"});return Task.CompletedTask;},true);modes.Add(repeat);
        Button? shuffle=null;shuffle=DesignControls.Chip("随机播放",()=>{var q=AppServices.Playback.Queue;q.Shuffle=!q.Shuffle;DesignControls.SelectChip(shuffle!,q.Shuffle);return Task.CompletedTask;},AppServices.Playback.Queue.Shuffle);modes.Add(shuffle);Header.Add(modes);
    }
    protected override Task LoadAsync(){var queue=AppServices.Playback.Queue;count.Text=queue.Tracks.Count+"首";Replace(queue.Tracks.Select(t=>new MediaRow{Title=t.Name,Subtitle=(queue.Current?.Id==t.Id?"正在播放 · ":"")+t.ArtistText,Cover=AppServices.Offline.Cover(t),Value=t,IsCurrent=queue.Current?.Id==t.Id}));return Task.CompletedTask;}
    protected override async Task OnOpen(MediaRow row){await Play(AppServices.Playback.Queue.Tracks.ToArray(),(Track)row.Value);await LoadAsync();}
    protected override Task OnMenu(MediaRow row)=>Actions.TrackMenu(this,(Track)row.Value);
    protected override async Task OnToolbar(){var result=await Actions.Sheet(this,"播放队列","管理接下来播放的歌曲。",[("clear","清空队列")]);if(result=="clear"&&await DisplayAlertAsync("清空队列","将暂停播放并清空当前队列。","清空","取消")){AppServices.Playback.Pause();AppServices.Playback.Queue.Replace([]);AppServices.Playback.Notify();await LoadAsync();}}
}
public sealed class FmPage : BasePage
{
    private readonly Image art=Ui.Cover("forest.png",320);
    private readonly Label name=Ui.Text("登录后，发现更多喜欢。",22),artist=Ui.Text("为你推荐的下一段旋律",14,"Muted");
    private readonly Button play;
    private List<Track> tracks=[];
    private Track? current;
    public FmPage():base("私人 FM",showMini:false)
    {
        var stack=Stack();var category=Ui.Text("PERSONAL FM",11,"Muted");category.CharacterSpacing=1.5;category.Margin=Ui.Inset(0,4,0,8);stack.Add(category);
        art.WidthRequest=-1;art.Clip=null;art.SizeChanged+=(_,_)=>{if(art.Width>0&&Math.Abs(art.HeightRequest-art.Width)>.5)art.HeightRequest=art.Width;};var cover=Ui.Card(art,radius:28,padding:0);cover.Margin=Ui.Inset(0,0,0,30);stack.Add(cover);stack.Add(name);artist.Margin=Ui.Inset(0,8,0,0);stack.Add(artist);
        var controls=new Grid{ColumnDefinitions=[new(GridLength.Star),new(Ui.S(80)),new(GridLength.Star)],Margin=Ui.Inset(0,26)};
        controls.Add(Ui.Icon("next",()=>Run(Next),"下一首"));
        play=Ui.Icon("play",()=>Run(async()=>{if(current==null){if(await Actions.RequireAccount(this))await LoadAsync();return;}if(AppServices.Playback.Queue.Current?.Id==current.Id)await AppServices.Playback.ToggleAsync();else await AppServices.Playback.PlayAsync(tracks,current);}),"播放私人FM");play.WidthRequest=Ui.S(80);play.HeightRequest=Ui.S(72);play.CornerRadius=24;play.FontSize=Ui.F(38);play.SetDynamicResource(Button.BackgroundColorProperty,"PrimaryContainer");play.SetDynamicResource(Button.TextColorProperty,"OnPrimaryContainer");controls.Add(play,1);
        controls.Add(Ui.Icon("like",()=>Run(async()=>{if(current!=null&&await Actions.RequireAccount(this)){await AppServices.Api.SetLikedAsync(current.Id,true);ShowMessage("已喜欢这首歌曲。");}}),"喜欢"),2);stack.Add(controls);var hint=Ui.Text("喜欢或跳过，让推荐更懂你。",12,"Muted");hint.HorizontalTextAlignment=TextAlignment.Center;stack.Add(hint);Body.Content=Scroll(stack);
    }
    protected override async Task LoadAsync(){tracks=await AppServices.Api.GetPrivateFmAsync(Token);current=tracks.FirstOrDefault();Render();}
    private async Task Next(){if(current==null){await LoadAsync();return;}int index=tracks.FindIndex(t=>t.Id==current.Id)+1;if(index>=tracks.Count)await LoadAsync();else current=tracks[index];Render();if(current!=null)await AppServices.Playback.PlayAsync(tracks,current);}
    private void Render(){if(current!=null){name.Text=current.Name;artist.Text=current.ArtistText;art.Source=Ui.Art(AppServices.Offline.Cover(current),800);}play.Text=Ui.Glyphs[AppServices.Playback.IsPlaying&&AppServices.Playback.Queue.Current?.Id==current?.Id?"pause":"play"];}
    protected override void OnAppearing(){base.OnAppearing();AppServices.Playback.Changed+=Render;}
    protected override void OnDisappearing(){AppServices.Playback.Changed-=Render;base.OnDisappearing();}
}
