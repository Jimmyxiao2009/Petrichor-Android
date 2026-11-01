using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class SettingsPage : BasePage
{
    public SettingsPage():base("设置"){}
    protected override bool RefreshOnAppear=>true;
    protected override Task LoadAsync()
    {
        var stack=Stack();stack.Add(Ui.Heading("按你的方式听。"));
        void Label(string text,double top=0){var label=Ui.Text(text,12,"Muted");label.Margin=new Thickness(0,top,0,8);stack.Add(label);}
        Label("外观");
        var dynamicSwitch=new Switch{IsToggled=Preferences.Get("dynamicColors",true),IsEnabled=OperatingSystem.IsAndroidVersionAtLeast(31)};
        SemanticProperties.SetDescription(dynamicSwitch,"跟随系统配色");
        dynamicSwitch.Toggled+=(_,e)=>{Preferences.Set("dynamicColors",e.Value);ThemeColors.Apply();};
        var group=new VerticalStackLayout{Spacing=0};
        group.Add(DesignControls.SettingRow("跟随系统配色",OperatingSystem.IsAndroidVersionAtLeast(31)?"使用壁纸生成的动态颜色\n适用于 Android 12 及以上":"Android 12 以下使用默认配色",dynamicSwitch));
        group.Add(Ui.Divider());
        string[] themes=["跟随系统","浅色","深色"];
        Task Theme()=>Run(async()=>{var result=await Actions.Sheet(this,"主题模式","选择应用外观",[("0","跟随系统"),("1","浅色"),("2","深色")]);if(result!=null){var index=int.Parse(result);Preferences.Set("theme",index);Application.Current!.UserAppTheme=(AppTheme)index;ThemeColors.Apply();await LoadAsync();}});
        group.Add(DesignControls.SettingRow("主题模式",themes[Math.Clamp(Preferences.Get("theme",0),0,2)],Ui.Icon("dark",Theme,"主题模式"),action:Theme));
        stack.Add(Ui.Card(group,padding:Ui.Inset(20,8)));
        Label("播放",26);
        var quality=Preferences.Get("quality",192000);
        Task Quality()=>Run(async()=>{var result=await Actions.Sheet(this,"在线播放音质","对下一次加载的歌曲生效，受账号与曲目权限限制。",[("128000","标准 · 128 kbps"),("192000","高品质 · 192 kbps"),("320000","极高 · 320 kbps"),("999000","无损 · FLAC"),("1999000","Hi-Res · FLAC")]);if(result!=null){Preferences.Set("quality",int.Parse(result));await LoadAsync();}});
        stack.Add(DesignControls.SettingRow("在线播放音质",quality==1999000?"Hi-Res · FLAC":quality==999000?"无损 · FLAC":quality==320000?"极高 · 320 kbps":quality==128000?"标准 · 128 kbps":"高品质 · 192 kbps",Ui.Icon("chevron",Quality,"选择音质"),"equalizer",Quality));
        Task Sleep()=>Run(()=>SleepTimer(this));
        var remaining=AppServices.Playback.Sleep.Remaining(DateTimeOffset.Now);
        stack.Add(DesignControls.SettingRow("睡眠定时",remaining.HasValue?"剩余 "+(int)Math.Ceiling(remaining.Value.TotalMinutes)+" 分钟":AppServices.Playback.Sleep.WaitingForTrackEnd?"听完当前歌曲":"关闭",Ui.Icon("chevron",Sleep,"睡眠定时"),"sleep",Sleep));
        var wifi=new Switch{IsToggled=Preferences.Get("wifiDownloads",true)};SemanticProperties.SetDescription(wifi,"仅在 Wi-Fi 下下载");wifi.Toggled+=(_,e)=>Preferences.Set("wifiDownloads",e.Value);
        stack.Add(DesignControls.SettingRow("仅在 Wi-Fi 下下载","使用移动网络时先询问",wifi,"download"));
        Task Diagnostics()=>Open(new DiagnosticsPage());
        stack.Add(DesignControls.SettingRow("播放诊断日志","查看、导出或清空本机播放记录",Ui.Icon("chevron",Diagnostics,"播放诊断日志"),"info",Diagnostics));
        Label("账号与存储",26);
        stack.Add(Ui.Button("本地与下载",()=>Open(new DownloadsPage()),"outlined"));
        var account=AppServices.Api.IsLoggedIn?Ui.Button("退出登录",()=>Run(async()=>{if(await DisplayAlertAsync("退出登录","退出后保留本机下载的音乐。","退出","取消")){await AppServices.Api.LogoutAsync();AppServices.NotifyAccount();await LoadAsync();}}),"outlined"):Ui.Button("登录网易云音乐",()=>Open(new LoginPage()),"outlined");account.Margin=Ui.Inset(0,12,0,0);stack.Add(account);
        var version=Ui.Text("Petrichor for Android · "+AppInfo.Current.VersionString+"\n.NET MAUI · Material Design 3",12,"Muted");version.HorizontalTextAlignment=TextAlignment.Center;version.Margin=Ui.Inset(0,26,0,0);stack.Add(version);var credit=Ui.Button("界面参考与开源许可 · Mooic",()=>Run(async()=>{using var stream=await FileSystem.OpenAppPackageFileAsync("Mooic-NOTICE.txt");using var reader=new StreamReader(stream);await Alert(await reader.ReadToEndAsync());}),"text");stack.Add(credit);
        Body.Content=Scroll(stack);return Task.CompletedTask;
    }
    public static Task SleepTimer(BasePage page)=>Actions.SleepTimerDialog(page);
}
public sealed class DownloadsPage : ListPage
{
    private int tab;
    private readonly Label folder=Ui.Text("",12,"Muted");
    protected override bool RefreshOnAppear=>true;
    public DownloadsPage():base("本地与下载")
    {
        string[] names=["已下载","下载任务","本地音乐"];
        var tabs=new TabStrip(names,choice=>{tab=choice;Render();return Task.CompletedTask;});Header.Add(tabs);
        folder.Text="下载位置："+DownloadFolder.Description;folder.Margin=Ui.Inset(0,16,0,4);Header.Add(folder);
        Header.Add(Ui.Button("选择下载文件夹",()=>Run(async()=>{if(await DownloadFolder.ChooseAsync())folder.Text="下载位置："+DownloadFolder.Description;}),"outlined","folder"));
        var info=Ui.Text("新下载同时保存到所选文件夹，应用内保留离线副本。已有文件不会移动。",12,"Muted");info.Margin=Ui.Inset(0,8);Header.Add(info);
        var import=Ui.Button("导入本地音乐",()=>Run(async()=>{await AppServices.Offline.ImportAsync();tab=2;tabs.SetSelected(2);Render();}),"outlined","add");import.Margin=Ui.Inset(0,20,0,0);Header.Add(import);
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
        if(value=="choose")await DownloadFolder.ChooseAsync();else if(value=="private")DownloadFolder.UsePrivate();folder.Text="下载位置："+DownloadFolder.Description;
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
        var result=await Actions.Sheet(this,track.Name,track.ArtistText,[("play","立即播放"),("remove","移除本机文件")]);
        if(result=="play")await OnOpen(row);
        if(result=="remove"&&await DisplayAlertAsync("移除文件","将删除应用保存的这份音频。","移除","取消")){await AppServices.Offline.DeleteAsync(track);Render();}
    }
}
