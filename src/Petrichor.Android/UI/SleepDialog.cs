using Microsoft.Maui.Platform;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

public static partial class Actions
{
    public static async Task SleepTimerDialog(BasePage page)
    {
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口");
        using var dialog=new Google.Android.Material.BottomSheet.BottomSheetDialog(activity);
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sleep=AppServices.Playback.Sleep;var remaining=sleep.Remaining(DateTimeOffset.Now);
        var stack=new VerticalStackLayout{Spacing=0,Padding=new Thickness(0,0,0,16)};
        var title=Ui.Text("睡眠定时",TypeRole.TitleLarge);title.Margin=new Thickness(24,0,24,4);stack.Add(title);
        var hint=Ui.Text(remaining.HasValue?"当前：剩余 "+(int)Math.Ceiling(remaining.Value.TotalMinutes)+" 分钟后暂停":sleep.WaitingForTrackEnd?"当前：听完这首歌后暂停":"到时间后暂停播放。",TypeRole.BodyMedium,"OnSurfaceVariant");hint.Margin=new Thickness(24,0,24,8);stack.Add(hint);
        string selected=remaining.HasValue||sleep.WaitingForTrackEnd?"":"30";var group="sleep-"+Guid.NewGuid().ToString("N");
        foreach(var choice in new[]{("off","关闭"),("15","15 分钟"),("30","30 分钟"),("60","60 分钟"),("90","90 分钟"),("track","听完当前歌曲")})
        {
            var radio=new RadioButton{GroupName=group,IsChecked=choice.Item1==selected,VerticalOptions=LayoutOptions.Center,InputTransparent=true};
            radio.HandlerChanged+=(_,_)=>{if(radio.Handler?.PlatformView is global::Android.Widget.CompoundButton native){native.Background=null;native.ButtonTintList=new global::Android.Content.Res.ColorStateList([[global::Android.Resource.Attribute.StateChecked],[]],[Ui.Color("Primary").ToPlatform().ToArgb(),Ui.Color("OnSurfaceVariant").ToPlatform().ToArgb()]);}};
            radio.CheckedChanged+=(_,e)=>{if(e.Value)selected=choice.Item1;};
            var row=Ui.ListItem(choice.Item2,null,()=>{radio.IsChecked=true;return Task.CompletedTask;},trailing:radio,trailingIcon:null);row.Padding=new Thickness(24,0,12,0);stack.Add(row);
        }
        var finish=new Switch{IsToggled=true};SemanticProperties.SetDescription(finish,"到时间后听完当前歌曲");
        var finishRow=Ui.ListItem("到时间后听完当前歌曲","避免在歌曲中途停止",()=>{finish.IsToggled=!finish.IsToggled;return Task.CompletedTask;},trailing:finish,trailingIcon:null);finishRow.Padding=new Thickness(24,8,16,8);
        stack.Add(Ui.Divider());stack.Add(finishRow);
        var start=Ui.Button("开始计时",()=>{
            if(selected=="off")sleep.Cancel();else if(selected=="track")sleep.StopAfterCurrentTrack();else if(selected.Length>0)sleep.Start(TimeSpan.FromMinutes(int.Parse(selected)),finish.IsToggled,DateTimeOffset.Now);
            dialog.Dismiss();if(selected.Length>0)page.ShowMessage(selected=="off"?"已关闭睡眠定时。":selected=="track"?"将听完当前歌曲后暂停。":"将在 "+selected+" 分钟后暂停。");return Task.CompletedTask;
        });
        start.Margin=new Thickness(24,8,24,0);stack.Add(start);
        var scroll=new ScrollView{Content=stack,HeightRequest=Math.Min(620,activity.Resources!.DisplayMetrics!.HeightPixels/activity.Resources.DisplayMetrics.Density*.86)};
        var native=scroll.ToPlatform(page.Handler!.MauiContext!);
        var layout=new global::Android.Widget.LinearLayout(activity){Orientation=global::Android.Widget.Orientation.Vertical};
        layout.AddView(new Google.Android.Material.BottomSheet.BottomSheetDragHandleView(activity),new global::Android.Widget.LinearLayout.LayoutParams(-1,-2));
        layout.AddView(native,new global::Android.Widget.LinearLayout.LayoutParams(-1,-2));
        dialog.SetContentView(layout);dialog.DismissEvent+=(_,_)=>completion.TrySetResult();dialog.Show();
        dialog.Behavior.SkipCollapsed=true;dialog.Behavior.State=Google.Android.Material.BottomSheet.BottomSheetBehavior.StateExpanded;
        try{await completion.Task;}finally{scroll.Handler?.DisconnectHandler();}
    }
}
