using Android.Widget;
using Android.Views;
using Google.Android.Material.BottomSheet;
using Petrichor.Core;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
using Microsoft.Maui.Platform;
namespace Petrichor.Mobile.UI;

public static partial class Actions
{
    private static bool showingMenu;
    private static readonly SemaphoreSlim removalGate = new(1,1);
    public static MediaRow Row(object value)=>value switch {
        Track t=>new(){Title=t.Name,Subtitle=t.ArtistText+(string.IsNullOrEmpty(t.Album?.Name)?"":" · "+t.Album.Name),Cover=AppServices.Offline.Cover(t),Value=t},
        Playlist p=>new(){Title=p.Name,Subtitle=p.TrackCount+"首 · "+(p.Creator?.Nickname??"网易云音乐"),Cover=p.CoverUrl,Value=p},
        DjRadio r=>new(){Title=r.Name,Subtitle=r.DjName+" · "+r.ProgramCount+"期",Cover=r.CoverUrl,Value=r},
        DjProgram p=>new(){Title=p.Name,Subtitle="所属播客 · "+p.RadioName+" · "+Ui.Duration(p.DurationMs),Cover=p.CoverUrl,Value=p},
        Artist a=>new(){Title=a.Name,Subtitle="歌手",Cover=a.CoverUrl,Value=a},
        Album a=>new(){Title=a.Name,Subtitle="专辑",Cover=a.CoverUrl,Value=a},
        _=>throw new ArgumentException("不支持的内容类型")
    };
    public static async Task<bool> RequireAccount(BasePage page)
    {if(AppServices.Api.IsLoggedIn)return true;await Ui.Push(new LoginPage());return false;}
    public static async Task Open(BasePage page,object value)=>await (value switch {
        Playlist p=>Ui.Push(new PlaylistPage(p.Id)),
        DjRadio r=>Ui.Push(new PodcastPage(r.Id)),
        DjProgram p=>OpenParent(page,p),
        Artist a=>Ui.Push(new ArtistPage(a.Id)),
        Album a=>Ui.Push(new AlbumPage(a.Id)),
        Track t=>page.Play([t],t),
        _=>Task.CompletedTask
    });
    public static async Task OpenParent(BasePage page,DjProgram program)
    {var p=program.RadioId>0?program:await AppServices.Api.GetProgramAsync(program.Id);if(p.RadioId<=0)throw new InvalidOperationException("这个节目暂时没有可打开的播客。");await Ui.Push(new PodcastPage(p.RadioId));}
    public static async Task PlayProgram(BasePage page,DjProgram program)
    {var p=program.Track.Id>0?program:await AppServices.Api.GetProgramAsync(program.Id);if(p.Track.Id<=0)throw new InvalidOperationException("这个节目暂时无法播放。");await page.Play([p.Track],p.Track);}
    public static async Task TrackMenu(BasePage page,Track track,Playlist? playlist=null,Func<Task>? refresh=null,Action? select=null)
    {
        if(showingMenu)return;showingMenu=true;
        string? result;
        try
        {
            var options=new List<(string Key,string Text)> {("play","立即播放"),("next","下一首播放"),("queue","加入播放队列")};
            if(track.Id>0){options.Add(("add","收藏到歌单"));options.Add(("like","喜欢 / 取消喜欢"));options.Add(("download","下载歌曲"));options.Add(("comments","查看评论"));}
            if(track.Artists.Any(a=>a.Id>0))options.Add(("artist","查看歌手"));
            if(track.Album?.Id>0)options.Add(("album","查看专辑"));
            if(select!=null)options.Add(("select","多选歌曲"));
            if(playlist!=null&&AppServices.Api.IsLoggedIn&&playlist.Creator?.Id==AppServices.Api.CurrentUser!.Id)options.Add(("remove",playlist.SpecialType==5?"取消喜欢":"从歌单移除"));
            options.Add(("sleep","睡眠定时"));result=await Sheet(page,track.Name,track.ArtistText,options,AppServices.Offline.Cover(track));
        }
        finally{showingMenu=false;}
        switch(result)
        {
            case "play":await page.Play([track],track);break;
            case "next":AppServices.Playback.Queue.InsertNext([track]);AppServices.Playback.Notify();page.ShowMessage("已加入下一首。");break;
            case "queue":AppServices.Playback.Queue.Append([track]);AppServices.Playback.Notify();page.ShowMessage("已加入播放队列。");break;
            case "add":if(await RequireAccount(page))await ChoosePlaylist(page,[track]);break;
            case "like":
                if(!await RequireAccount(page))break;
                var liked=(await AppServices.Api.GetLikedTrackIdsAsync()).Contains(track.Id);
                await AppServices.Api.SetLikedAsync(track.Id,!liked);page.ShowMessage(liked?"已取消喜欢。":"已喜欢。");break;
            case "download":await AppServices.Offline.DownloadAsync(track);page.ShowMessage("下载完成，可离线播放。");break;
            case "comments":await Ui.Push(new CommentsPage(track));break;
            case "artist":await Ui.Push(new ArtistPage(track.Artists.First(a=>a.Id>0).Id));break;
            case "album":await Ui.Push(new AlbumPage(track.Album.Id));break;
            case "select":select?.Invoke();break;
            case "sleep":await SettingsPage.SleepTimer(page);break;
            case "remove":if(playlist!=null)await Remove(page,playlist,[track],refresh);break;
        }
    }
    public static async Task Remove(BasePage page,Playlist playlist,IReadOnlyList<Track> tracks,Func<Task>? refresh)
    {
        if(!await removalGate.WaitAsync(0)){page.ShowMessage("正在移除歌曲，请稍候。");return;}
        try {
        if(tracks.Count==0){page.ShowMessage("请先选择歌曲。");return;}
        if(playlist.Creator?.Id!=AppServices.Api.CurrentUser?.Id)throw new InvalidOperationException("只能修改自己创建的歌单。");
        var verb=playlist.SpecialType==5?"取消喜欢":"从歌单移除";
        if(!await Dialogs.Confirm(verb+"？",tracks.Count==1?"将"+verb+"「"+tracks[0].Name+"」。":"将"+verb+"所选的 "+tracks.Count+" 首歌曲。",playlist.SpecialType==5?"取消喜欢":"移除",destructive:true,icon:"delete"))return;
        Exception? failure=null;
        try{await AppServices.Api.RemoveTracksFromPlaylistAsync(playlist.Id,tracks.Select(t=>t.Id));}
        catch(Exception e){failure=e;}
        try{if(refresh!=null)await refresh();}
        catch(Exception e){failure??=e;}
        if(failure!=null)throw new InvalidOperationException("请以刷新的歌单内容为准。"+failure.Message,failure);
        page.ShowMessage("已"+verb+" "+tracks.Count+" 首歌曲。");
        } finally {removalGate.Release();}
    }
    private static string IconFor(string key)=>key switch{"play"=>"play","next"=>"next","queue"=>"queue","add"=>"playlist","like"=>"like","download"=>"download","comments"=>"comments","artist"=>"person","album"=>"album","select"=>"select","remove" or "clear"=>"delete","sleep" or "15" or "30" or "60" or "90" or "track"=>"sleep","retry"=>"history","choose"=>"folder","private"=>"storage","follow"=>"lyrics","save"=>"like","0"=>"palette","1"=>"palette","2"=>"dark",_=>key.All(char.IsDigit)?"quality":"chevron"};
    // Material 3 modal bottom sheet with a list of actions. `selected` marks the current choice.
    public static Task<string?> Sheet(BasePage page,string title,string subtitle,IEnumerable<(string Key,string Text)> options,string? cover=null,string? selected=null)
    {
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口");
        var completion=new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog=new BottomSheetDialog(activity);
        var density=activity.Resources!.DisplayMetrics!.Density;
        int D(double n)=>(int)Math.Round(n*density);
        static int C(string key)=>Ui.Color(key).ToPlatform().ToArgb();
        var medium=OperatingSystem.IsAndroidVersionAtLeast(28)?Android.Graphics.Typeface.Create(Android.Graphics.Typeface.Default,500,false):Android.Graphics.Typeface.Create("sans-serif-medium",Android.Graphics.TypefaceStyle.Normal);
        var icons=Android.Graphics.Typeface.CreateFromAsset(activity.Assets,"MaterialIconsOutlined-Regular.otf");
        var layout=new LinearLayout(activity){Orientation=Orientation.Vertical};layout.SetPadding(0,0,0,D(12));
        layout.AddView(new BottomSheetDragHandleView(activity),new LinearLayout.LayoutParams(-1,-2));
        TextView Words(string text,float sp,string color,Android.Graphics.Typeface? face=null){var view=new TextView(activity){Text=text};view.SetTextSize(Android.Util.ComplexUnitType.Sp,sp);view.SetTextColor(new Android.Graphics.Color(C(color)));if(face!=null)view.Typeface=face;return view;}
        Image? artwork=null;
        if(cover!=null)
        {
            var heading=new LinearLayout(activity){Orientation=Orientation.Horizontal};heading.SetGravity(GravityFlags.CenterVertical);heading.SetPadding(D(24),0,D(24),D(16));
            artwork=Ui.Cover(cover,56);heading.AddView(artwork.ToPlatform(page.Handler!.MauiContext!),new LinearLayout.LayoutParams(D(56),D(56)){RightMargin=D(16)});
            var text=new LinearLayout(activity){Orientation=Orientation.Vertical};
            var name=Words(title,16,"OnSurface",medium);name.SetMaxLines(2);name.Ellipsize=Android.Text.TextUtils.TruncateAt.End;text.AddView(name);
            if(subtitle.Length>0){var by=Words(subtitle,14,"OnSurfaceVariant");by.SetMaxLines(1);by.Ellipsize=Android.Text.TextUtils.TruncateAt.End;text.AddView(by);}
            heading.AddView(text,new LinearLayout.LayoutParams(0,-2,1));layout.AddView(heading);
            var divider=new Android.Views.View(activity);divider.SetBackgroundColor(new Android.Graphics.Color(C("OutlineVariant")));layout.AddView(divider,new LinearLayout.LayoutParams(-1,D(1)){BottomMargin=D(8)});
        }
        else
        {
            var heading=Words(title,22,"OnSurface");heading.SetPadding(D(24),0,D(24),subtitle.Length>0?D(4):D(16));layout.AddView(heading);
            if(subtitle.Length>0){var detail=Words(subtitle,14,"OnSurfaceVariant");detail.SetPadding(D(24),0,D(24),D(16));layout.AddView(detail);}
        }
        foreach(var item in options)
        {
            var danger=item.Key=="remove"||item.Key=="clear";var chosen=item.Key==selected;
            var row=new LinearLayout(activity){Orientation=Orientation.Horizontal};row.SetGravity(GravityFlags.CenterVertical);row.SetPadding(D(24),0,D(24),0);
            var glyph=new TextView(activity){Text=Ui.Glyphs[IconFor(item.Key)],Typeface=icons,Gravity=GravityFlags.Center};glyph.SetTextSize(Android.Util.ComplexUnitType.Dip,24);glyph.SetTextColor(new Android.Graphics.Color(C(danger?"Error":chosen?"Primary":"OnSurfaceVariant")));
            row.AddView(glyph,new LinearLayout.LayoutParams(D(24),D(24)){RightMargin=D(16)});
            var label=Words(item.Text,16,danger?"Error":chosen?"Primary":"OnSurface",chosen?medium:null);label.SetMaxLines(1);label.Ellipsize=Android.Text.TextUtils.TruncateAt.End;
            row.AddView(label,new LinearLayout.LayoutParams(0,-2,1));
            if(chosen){var check=new TextView(activity){Text=Ui.Glyphs["check"],Typeface=icons};check.SetTextSize(Android.Util.ComplexUnitType.Dip,24);check.SetTextColor(new Android.Graphics.Color(C("Primary")));row.AddView(check,new LinearLayout.LayoutParams(D(24),D(24)));}
            row.Background=new Android.Graphics.Drawables.RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(Ui.Color("OnSurface").WithAlpha(Md.PressedState).ToPlatform()),null,new Android.Graphics.Drawables.ColorDrawable(Android.Graphics.Color.White));
            row.Clickable=true;row.Focusable=true;row.ContentDescription=item.Text;row.Click+=(_,_)=>{completion.TrySetResult(item.Key);dialog.Dismiss();};
            layout.AddView(row,new LinearLayout.LayoutParams(-1,D(56)));
        }
        var scroll=new Android.Widget.ScrollView(activity);scroll.AddView(layout);
        dialog.SetContentView(scroll);
        dialog.DismissEvent+=(_,_)=>completion.TrySetResult(null);
        dialog.Show();dialog.Behavior.SkipCollapsed=true;dialog.Behavior.State=BottomSheetBehavior.StateExpanded;
        return Await();
        async Task<string?> Await(){try{return await completion.Task;}finally{artwork?.Handler?.DisconnectHandler();dialog.Dispose();}}
    }
}
