using Android.App;
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
        if(!await page.DisplayAlertAsync(verb,tracks.Count==1?"确定"+verb+"「"+tracks[0].Name+"」？":"确定"+verb+"所选的 "+tracks.Count+" 首歌曲？","确定","取消"))return;
        Exception? failure=null;
        try{await AppServices.Api.RemoveTracksFromPlaylistAsync(playlist.Id,tracks.Select(t=>t.Id));}
        catch(Exception e){failure=e;}
        try{if(refresh!=null)await refresh();}
        catch(Exception e){failure??=e;}
        if(failure!=null)throw new InvalidOperationException("请以刷新的歌单内容为准。"+failure.Message,failure);
        page.ShowMessage("已"+verb+" "+tracks.Count+" 首歌曲。");
        } finally {removalGate.Release();}
    }
    public static Task<string?> Sheet(BasePage page,string title,string subtitle,IEnumerable<(string Key,string Text)> options,string? cover=null)
    {
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口");
        var completion=new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dialog=new BottomSheetDialog(activity);
        var density=activity.Resources!.DisplayMetrics!.Density;
        int D(double n)=>(int)(n*density);
        var layout=new LinearLayout(activity){Orientation=Orientation.Vertical};
        layout.SetPadding(D(12),D(12),D(12),D(24));
        var handle=new global::Android.Views.View(activity);var handleShape=new Android.Graphics.Drawables.GradientDrawable();var muted=Ui.Color("Muted");handleShape.SetColor(Android.Graphics.Color.Argb(100,(int)(muted.Red*255),(int)(muted.Green*255),(int)(muted.Blue*255)));handleShape.SetCornerRadius(D(2));handle.Background=handleShape;
        layout.AddView(handle,new LinearLayout.LayoutParams(D(32),D(4)){Gravity=GravityFlags.CenterHorizontal,BottomMargin=D(16)});
        var background=new Android.Graphics.Drawables.GradientDrawable();
        var surface=Ui.Color("SurfaceContainerLow");background.SetColor(Android.Graphics.Color.Argb((int)(surface.Alpha*255),(int)(surface.Red*255),(int)(surface.Green*255),(int)(surface.Blue*255)));
        background.SetCornerRadii([D(28),D(28),D(28),D(28),0,0,0,0]);layout.Background=background;
        var icons=Android.Graphics.Typeface.CreateFromAsset(activity.Assets,"MaterialIconsOutlined-Regular.otf");
        void Label(string text,float size,string color,int height,Action? tap=null,string? icon=null)
        {
            var view=new TextView(activity){Text=text,TextSize=size,Gravity=GravityFlags.CenterVertical};
            var c=Ui.Color(color);view.SetTextColor(Android.Graphics.Color.Argb(255,(int)(c.Red*255),(int)(c.Green*255),(int)(c.Blue*255)));
            var row=new LinearLayout(activity){Orientation=Orientation.Horizontal};
            if(icon!=null){var glyph=new TextView(activity){Text=Ui.Glyphs[icon],TextSize=24,Typeface=icons,Gravity=GravityFlags.CenterVertical};glyph.SetTextColor(Android.Graphics.Color.Argb(255,(int)(c.Red*255),(int)(c.Green*255),(int)(c.Blue*255)));row.AddView(glyph,new LinearLayout.LayoutParams(D(44),-1));}
            row.SetGravity(GravityFlags.CenterVertical);row.AddView(view,new LinearLayout.LayoutParams(-1,-1));layout.AddView(row,new LinearLayout.LayoutParams(-1,D(height)));
            if(tap!=null){
                var fill=new Android.Graphics.Drawables.GradientDrawable();fill.SetColor(Ui.Color("Surface").ToPlatform());fill.SetCornerRadius(D(12));
                var mask=new Android.Graphics.Drawables.GradientDrawable();mask.SetColor(Android.Graphics.Color.White);mask.SetCornerRadius(D(12));
                row.Background=new Android.Graphics.Drawables.RippleDrawable(Android.Content.Res.ColorStateList.ValueOf(Ui.Color("Primary").WithAlpha(.12f).ToPlatform()),fill,mask);row.SetPadding(D(12),0,D(12),0);
                if(row.LayoutParameters is LinearLayout.LayoutParams parameters)parameters.BottomMargin=D(3);
                row.Clickable=true;row.Focusable=true;row.Click+=(_,_)=>tap();
            }
        }
        Image? artwork=null;
        if(cover!=null){var heading=new LinearLayout(activity){Orientation=Orientation.Horizontal};heading.SetGravity(GravityFlags.CenterVertical);artwork=Ui.Cover(cover,52);heading.AddView(artwork.ToPlatform(page.Handler!.MauiContext!),new LinearLayout.LayoutParams(D(52),D(52)){RightMargin=D(14)});var text=new LinearLayout(activity){Orientation=Orientation.Vertical};var name=new TextView(activity){Text=title,TextSize=15};var by=new TextView(activity){Text=subtitle,TextSize=12};var ink=Ui.Color("OnSurface");name.SetTextColor(Android.Graphics.Color.Argb(255,(int)(ink.Red*255),(int)(ink.Green*255),(int)(ink.Blue*255)));by.SetTextColor(Android.Graphics.Color.Argb(255,(int)(muted.Red*255),(int)(muted.Green*255),(int)(muted.Blue*255)));text.AddView(name);text.AddView(by);heading.AddView(text);layout.AddView(heading,new LinearLayout.LayoutParams(-1,D(76)));}
        else{Label(title,20,"OnSurface",40);if(subtitle.Length>0)Label(subtitle,13,"Muted",32);}
        foreach(var item in options){string glyph=item.Key switch{"play"=>"play","next"=>"next","queue"=>"queue","add"=>"playlist","like"=>"like","download"=>"download","comments"=>"comments","artist"=>"person","album"=>"album","select"=>"check","remove" or "clear"=>"delete","sleep" or "15" or "30" or "60" or "90" or "track"=>"sleep",_=>"chevron"};Label(item.Text,15,item.Key=="remove"?"Error":"OnSurface",56,()=>{completion.TrySetResult(item.Key);dialog.Dismiss();},glyph);}
        var scroll=new Android.Widget.ScrollView(activity);scroll.AddView(layout);
        dialog.SetContentView(scroll);
        dialog.DismissEvent+=(_,_)=>completion.TrySetResult(null);
        dialog.Show();
        return Await();
        async Task<string?> Await(){try{return await completion.Task;}finally{artwork?.Handler?.DisconnectHandler();dialog.Dispose();}}
    }
}
