using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class SettingsPage : BasePage
{
    public SettingsPage():base("设置"){Build();}
    protected override bool RefreshOnAppear=>true;
    protected override Task LoadAsync(){Build();return Task.CompletedTask;}
    private static string QualityName(int quality)=>quality==1999000?"Hi-Res · FLAC":quality==999000?"无损 · FLAC":quality==320000?"极高 · 320 kbps":quality==128000?"标准 · 128 kbps":"高品质 · 192 kbps";
    private void Build()
    {
        var stack=new VerticalStackLayout{Spacing=0,Padding=new Thickness(Md.Margin,0,Md.Margin,32)};
        void Category(string text){var label=Ui.Category(text);label.Margin=new Thickness(Md.Margin,16,Md.Margin,8);stack.Add(label);}
        var user=AppServices.Api.CurrentUser;
        if(user!=null)
        {
            var profile=new Grid{ColumnDefinitions=[new(56),new(GridLength.Star)],ColumnSpacing=16,Padding=new Thickness(16),MinimumHeightRequest=88};
            profile.Add(Ui.Avatar(user.AvatarUrl,56));var words=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center};words.Add(Ui.Line(user.Nickname,TypeRole.TitleLarge));words.Add(Ui.Line("网易云音乐账号",TypeRole.BodyMedium,"OnSurfaceVariant"));profile.Add(words,1);
            var card=Ui.Card(profile,"SurfaceContainer",Md.ShapeLarge,0);card.Margin=new Thickness(0,8,0,0);stack.Add(card);
        }
        Category("外观");
        var dynamicSwitch=new Switch{IsToggled=Preferences.Get("dynamicColors",true),IsEnabled=ThemeColors.DynamicAvailable,VerticalOptions=LayoutOptions.Center};
        SemanticProperties.SetDescription(dynamicSwitch,"跟随系统配色");
        dynamicSwitch.Toggled+=(_,e)=>{Preferences.Set("dynamicColors",e.Value);ThemeColors.Apply();};
        string[] themes=["跟随系统","浅色","深色"];
        Task Theme()=>Run(async()=>{var current=Math.Clamp(Preferences.Get("theme",0),0,2);var result=await Actions.Sheet(this,"主题模式","选择应用外观",[("0","跟随系统"),("1","浅色"),("2","深色")],selected:current.ToString());if(result!=null){var index=int.Parse(result);Preferences.Set("theme",index);Application.Current!.UserAppTheme=(AppTheme)index;ThemeColors.Apply();Build();}});
        stack.Add(DesignControls.Group(
            DesignControls.SettingItem("跟随系统配色",ThemeColors.DynamicAvailable?"使用壁纸生成的动态颜色":"Android 12 以上可用，当前使用默认配色",dynamicSwitch,"palette",ThemeColors.DynamicAvailable?()=>{dynamicSwitch.IsToggled=!dynamicSwitch.IsToggled;return Task.CompletedTask;}:null),
            DesignControls.SettingItem("主题模式",themes[Math.Clamp(Preferences.Get("theme",0),0,2)],null,"dark",Theme)));
        Category("播放");
        Task Quality()=>Run(async()=>{var result=await Actions.Sheet(this,"在线播放音质","对下一次加载的歌曲生效，受账号与曲目权限限制。",[("128000","标准 · 128 kbps"),("192000","高品质 · 192 kbps"),("320000","极高 · 320 kbps"),("999000","无损 · FLAC"),("1999000","Hi-Res · FLAC")],selected:Preferences.Get("quality",192000).ToString());if(result!=null){Preferences.Set("quality",int.Parse(result));Build();}});
        Task Sleep()=>Run(()=>SleepTimer(this));
        var remaining=AppServices.Playback.Sleep.Remaining(DateTimeOffset.Now);
        var wifi=new Switch{IsToggled=Preferences.Get("wifiDownloads",true),VerticalOptions=LayoutOptions.Center};SemanticProperties.SetDescription(wifi,"仅在 Wi-Fi 下下载");wifi.Toggled+=(_,e)=>Preferences.Set("wifiDownloads",e.Value);
        stack.Add(DesignControls.Group(
            DesignControls.SettingItem("在线播放音质",QualityName(Preferences.Get("quality",192000)),null,"equalizer",Quality),
            DesignControls.SettingItem("睡眠定时",remaining.HasValue?"剩余 "+(int)Math.Ceiling(remaining.Value.TotalMinutes)+" 分钟":AppServices.Playback.Sleep.WaitingForTrackEnd?"听完当前歌曲":"关闭",null,"sleep",Sleep),
            DesignControls.SettingItem("仅在 Wi-Fi 下下载","使用移动网络时先询问",wifi,"wifi",()=>{wifi.IsToggled=!wifi.IsToggled;return Task.CompletedTask;})));
        Category("存储与账号");
        var items=new List<View>{DesignControls.SettingItem("本地与下载","下载位置、本地音乐导入",null,"download",()=>Open(new DownloadsPage())),DesignControls.SettingItem("播放诊断日志","查看、导出或清空本机播放记录",null,"info",()=>Open(new DiagnosticsPage()))};
        if(AppServices.Api.IsLoggedIn)
        {
            var logout=Ui.ListItem("退出登录","保留本机下载的音乐",()=>Run(async()=>{if(await Dialogs.Confirm("退出登录？","退出后保留本机下载的音乐。","退出",icon:"logout")){await AppServices.Api.LogoutAsync();AppServices.NotifyAccount();Build();}}),"logout",trailingIcon:null,iconColor:"Error");
            if(logout.Children[1] is VerticalStackLayout{Children:[Label first,..]})first.SetDynamicResource(Label.TextColorProperty,"Error");
            items.Add(logout);
        }
        else items.Add(DesignControls.SettingItem("登录网易云音乐","同步歌单、收藏和每日推荐",null,"login",()=>Open(new LoginPage())));
        stack.Add(DesignControls.Group(items.ToArray()));
        Category("关于");
        stack.Add(DesignControls.Group(
            DesignControls.SettingItem("Petrichor for Android",AppInfo.Current.VersionString+" · .NET MAUI · Material Design 3",null,"info"),
            DesignControls.SettingItem("界面参考与开源许可","Mooic · GPL-3.0",null,"notes",()=>Run(async()=>{using var stream=await FileSystem.OpenAppPackageFileAsync("Mooic-NOTICE.txt");using var reader=new StreamReader(stream);await Dialogs.Alert("开源许可",await reader.ReadToEndAsync());}))));
        var scroll=Scroll(stack);Body.Content=scroll;
    }
    public static Task SleepTimer(BasePage page)=>Actions.SleepTimerDialog(page);
}
public sealed class DownloadsPage : ListPage
{
    private int tab;
    private readonly Label folder=Ui.Text("",TypeRole.BodyMedium,"OnSurfaceVariant");
    protected override bool RefreshOnAppear=>true;
    protected override bool HasToolbarMenu=>true;
    public DownloadsPage():base("本地与下载")
    {
        string[] names=["已下载","下载任务","本地音乐"];
        var tabs=new TabStrip(names,choice=>{tab=choice;Render();return Task.CompletedTask;});tabs.Margin=new Thickness(-Md.Margin,0,-Md.Margin,8);Header.Add(tabs);
        folder.Text=DownloadFolder.Description;folder.MaxLines=2;folder.LineBreakMode=LineBreakMode.TailTruncation;
        var location=Ui.ListItem("下载位置",null,()=>Run(async()=>{if(await DownloadFolder.ChooseAsync())folder.Text=DownloadFolder.Description;}),"folder",trailing:Ui.Glyph("edit",20,"OnSurfaceVariant"));
        if(location.Children[1] is VerticalStackLayout words){words.Add(folder);}location.MinimumHeightRequest=72;location.Margin=new Thickness(-Md.Margin,0);Header.Add(location);
        var info=Ui.Text("新下载同时保存到所选文件夹，应用内保留离线副本。已有文件不会移动。",TypeRole.BodySmall,"OnSurfaceVariant");info.Margin=new Thickness(0,4,0,8);Header.Add(info);
        var import=Ui.Button("导入本地音乐",()=>Run(async()=>{await AppServices.Offline.ImportAsync();tab=2;tabs.SetSelected(2,true);Render();}),"tonal","add");import.HorizontalOptions=LayoutOptions.Start;Header.Add(import);
        Header.Add(Ui.Section("设备上的声音"));
    }
    protected override void OnAppearing(){base.OnAppearing();AppServices.Offline.Changed+=Render;}
    protected override void OnDisappearing(){AppServices.Offline.Changed-=Render;base.OnDisappearing();}
    protected override Task LoadAsync(){Render();return Task.CompletedTask;}
    private void Render()
    {
        if(tab==1)
        {
            var jobs=AppServices.Offline.Jobs.Values.ToArray();
            if(Rows.Count==jobs.Length&&Rows.Select(r=>((Track)r.Value).Id).SequenceEqual(jobs.Select(j=>j.Track.Id)))
                for(var i=0;i<jobs.Length;i++)Rows[i].Subtitle=jobs[i].Error??"正在下载 · "+(int)(jobs[i].Progress*100)+"%";
            else Replace(jobs.Select(j=>new MediaRow{Title=j.Track.Name,Subtitle=j.Error??"正在下载 · "+(int)(j.Progress*100)+"%",Cover=AppServices.Offline.Cover(j.Track),Value=j.Track}));
        }
        else Replace((tab==2?AppServices.Offline.Local:AppServices.Offline.Downloads).Select(Actions.Row));
    }
    protected override async Task OnToolbar()
    {
        var value=await Actions.Sheet(this,"下载位置",DownloadFolder.Description,[("choose","选择文件夹"),("private","使用应用专属空间")]);
        if(value=="choose")await DownloadFolder.ChooseAsync();else if(value=="private")DownloadFolder.UsePrivate();folder.Text=DownloadFolder.Description;
    }
    protected override Task OnOpen(MediaRow row)=>tab==1?OnMenu(row):Play(Rows.Select(r=>(Track)r.Value),(Track)row.Value);
    protected override async Task OnMenu(MediaRow row)
    {
        var track=(Track)row.Value;
        if(tab==1)
        {
            if(AppServices.Offline.Jobs.TryGetValue(track.Id,out var job)&&job.Error!=null)
            {
                var choice=await Actions.Sheet(this,track.Name,job.Error,[("retry","重试下载"),("clear","移除任务")]);
                if(choice!=null)AppServices.Offline.DismissFailed(track.Id);
                if(choice=="retry")await AppServices.Offline.DownloadAsync(track);
            }
            else ShowMessage("正在下载，完成后可以离线播放。");
            return;
        }
        var result=await Actions.Sheet(this,track.Name,track.ArtistText,[("play","立即播放"),("remove","移除本机文件")],AppServices.Offline.Cover(track));
        if(result=="play")await OnOpen(row);
        if(result=="remove"&&await Dialogs.Confirm("移除文件？","将删除应用保存的这份音频。","移除",destructive:true,icon:"delete")){await AppServices.Offline.DeleteAsync(track);Render();}
    }
}
