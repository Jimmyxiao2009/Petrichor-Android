using Microsoft.Maui.Platform;
using Microsoft.Maui.Controls.Shapes;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

public static partial class Actions
{
    public static async Task SleepTimerDialog(BasePage page)
    {
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口");
        using var dialog=new Google.Android.Material.BottomSheet.BottomSheetDialog(activity);
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stack=new VerticalStackLayout{Spacing=0,Padding=Ui.Inset(24,8,24,28)};
        var handle=new BoxView{WidthRequest=Ui.S(32),HeightRequest=Ui.S(4),CornerRadius=2,Opacity=.5,HorizontalOptions=LayoutOptions.Center,Margin=Ui.Inset(0,4,0,16)};handle.SetDynamicResource(BoxView.ColorProperty,"Muted");stack.Add(handle);
        stack.Add(Ui.Text("让音乐陪你入睡。",22));var hint=Ui.Text("到时间后暂停播放。",14,"Muted");hint.Margin=Ui.Inset(0,12,0,16);stack.Add(hint);
        string selected="30";var group="sleep-"+Guid.NewGuid().ToString("N");
        foreach(var choice in new[]{("off","关闭"),("15","15分钟"),("30","30分钟"),("60","60分钟"),("90","90分钟"),("track","听完当前歌曲")})
        {
            var radio=new RadioButton{Content=choice.Item2,GroupName=group,IsChecked=choice.Item1==selected,FontSize=Ui.F(15),MinimumHeightRequest=Ui.S(56)};radio.SetDynamicResource(RadioButton.TextColorProperty,"OnSurface");
            radio.HandlerChanged+=(_,_)=>{if(radio.Handler?.PlatformView is global::Android.Widget.CompoundButton native){native.Background=null;native.ButtonTintList=new global::Android.Content.Res.ColorStateList([[(int)global::Android.Resource.Attribute.StateChecked],[]],[Ui.Color("Primary").ToPlatform().ToArgb(),Ui.Color("Muted").ToPlatform().ToArgb()]);}};
            radio.CheckedChanged+=(_,e)=>{if(e.Value)selected=choice.Item1;};stack.Add(radio);
        }
        var finish=new Switch{IsToggled=true};SemanticProperties.SetDescription(finish,"到时间后听完当前歌曲");stack.Add(DesignControls.SettingRow("到时间后听完当前歌曲","",finish));
        stack.Add(Ui.Button("开始计时",()=>{
            var sleep=AppServices.Playback.Sleep;
            if(selected=="off")sleep.Cancel();else if(selected=="track")sleep.StopAfterCurrentTrack();else sleep.Start(TimeSpan.FromMinutes(int.Parse(selected)),finish.IsToggled,DateTimeOffset.Now);
            dialog.Dismiss();page.ShowMessage(selected=="off"?"已关闭睡眠定时。":selected=="track"?"将听完当前歌曲后暂停。":"将在 "+selected+" 分钟后暂停。");return Task.CompletedTask;
        }));
        var scroll=new ScrollView{Content=stack,HeightRequest=Math.Min(610,activity.Resources!.DisplayMetrics!.HeightPixels/activity.Resources.DisplayMetrics.Density*.86)};
        var card=Ui.Card(scroll,"Surface",28,0);card.StrokeShape=new RoundRectangle{CornerRadius=new CornerRadius(28,28,0,0)};
        dialog.SetContentView(card.ToPlatform(page.Handler!.MauiContext!));dialog.DismissEvent+=(_,_)=>completion.TrySetResult();dialog.Show();
        dialog.Behavior.State=Google.Android.Material.BottomSheet.BottomSheetBehavior.StateExpanded;
        try{await completion.Task;}finally{card.Handler?.DisconnectHandler();}
    }
}
