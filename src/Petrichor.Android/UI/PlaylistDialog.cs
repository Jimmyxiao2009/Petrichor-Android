using Microsoft.Maui.Platform;
using Petrichor.Core;
using Petrichor.Mobile.Services;
using Entry=Petrichor.Mobile.UI.NativeEntry;
namespace Petrichor.Mobile.UI;

public static partial class Actions
{
    public static async Task NewPlaylist(BasePage page,IReadOnlyList<Track>? tracks=null,Func<Task>? refresh=null,bool closeChooser=false,Action? completed=null)
    {
        if(!await RequireAccount(page))return;
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口");
        using var dialog=new global::Android.App.Dialog(activity);
        dialog.RequestWindowFeature((int)global::Android.Views.WindowFeatures.NoTitle);
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stack=new VerticalStackLayout{Spacing=0};stack.Add(Ui.Glyph("playlist",28,"Primary"));
        var title=Ui.Text("为喜欢，留个位置。",22);title.Margin=Ui.Inset(0,16,0,12);stack.Add(title);stack.Add(Ui.Text("创建一个新的音乐歌单。",14,"Muted"));
        var name=new Entry{Placeholder="例如：雨天适合循环",FontSize=Ui.F(16),MaxLength=40};
        var field=DesignControls.Field("歌单名称",name,background:"SurfaceContainer");stack.Add(field);
        var privacy=new Switch();SemanticProperties.SetDescription(privacy,"设为私密歌单");stack.Add(DesignControls.SettingRow("设为私密歌单","只有你可以查看",privacy));
        var error=Ui.Text("",12,"Error");error.IsVisible=false;stack.Add(error);
        Playlist? created=null;bool saved=false;
        var buttons=new HorizontalStackLayout{Spacing=Ui.S(8),HorizontalOptions=LayoutOptions.End,Margin=Ui.Inset(0,20,0,0)};
        var cancel=Ui.Button("取消",()=>{dialog.Dismiss();return Task.CompletedTask;},"text");buttons.Add(cancel);
        buttons.Add(Ui.Button("创建",async()=>{
            cancel.IsEnabled=false;dialog.SetCancelable(false);error.IsVisible=false;
            try
            {
                created??=await AppServices.Api.CreatePlaylistAsync(name.Text??"",privacy.IsToggled);
                name.IsEnabled=false;privacy.IsEnabled=false;
                if(tracks?.Count>0)await AppServices.Api.AddTracksToPlaylistAsync(created.Id,tracks.Select(t=>t.Id));
                saved=true;dialog.Dismiss();
            }
            catch(Exception e){error.Text=(created==null?"":"歌单已创建，再次点击会重试添加歌曲。")+Ui.ErrorText(e);error.IsVisible=true;}
            finally{cancel.IsEnabled=true;dialog.SetCancelable(true);}
        }));stack.Add(buttons);
        var card=Ui.Card(stack,radius:28,padding:24);
        var native=card.ToPlatform(page.Handler!.MauiContext!);dialog.SetContentView(native);
        dialog.DismissEvent+=(_,_)=>completion.TrySetResult();dialog.Show();
        var window=dialog.Window!;window.SetSoftInputMode(global::Android.Views.SoftInput.StateAlwaysHidden|global::Android.Views.SoftInput.AdjustResize);window.SetBackgroundDrawable(new global::Android.Graphics.Drawables.ColorDrawable(global::Android.Graphics.Color.Transparent));
        window.AddFlags(global::Android.Views.WindowManagerFlags.DimBehind);window.SetDimAmount(.45f);
        window.SetLayout((int)((activity.Resources!.DisplayMetrics!.WidthPixels/activity.Resources.DisplayMetrics.Density-64)*activity.Resources.DisplayMetrics.Density),global::Android.Views.ViewGroup.LayoutParams.WrapContent);
        try{await completion.Task;}finally{card.Handler?.DisconnectHandler();}
        if(saved){completed?.Invoke();if(closeChooser)await Ui.Pop();else if(refresh!=null)await refresh();page.ShowMessage("歌单已创建。");}
    }

    public static async Task ChoosePlaylist(BasePage page,IReadOnlyList<Track> tracks)
    {
        if(!await RequireAccount(page))return;
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口");
        using var dialog=new Google.Android.Material.BottomSheet.BottomSheetDialog(activity);
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stack=new VerticalStackLayout{Spacing=0,Padding=Ui.Inset(24,8,24,28)};
        var handle=new BoxView{WidthRequest=Ui.S(32),HeightRequest=Ui.S(4),CornerRadius=2,Opacity=.5,HorizontalOptions=LayoutOptions.Center,Margin=Ui.Inset(0,4,0,16)};handle.SetDynamicResource(BoxView.ColorProperty,"Muted");stack.Add(handle);
        stack.Add(Ui.Section("收藏到歌单",()=>page.Run(()=>NewPlaylist(page,tracks,completed:()=>dialog.Dismiss())),"新建"));
        stack.Add(Ui.Text(tracks.Count==1?tracks[0].Name+" · "+tracks[0].ArtistText:"已选择 "+tracks.Count+" 首歌曲",12,"Muted"));
        var error=Ui.Text("",12,"Error");error.IsVisible=false;stack.Add(error);
        var lists=new VerticalStackLayout{Spacing=0};stack.Add(lists);int offset=0;bool busy=false;
        var more=Ui.Button("加载更多",()=>Task.CompletedTask,"text");stack.Add(more);
        async Task Fetch()
        {
            var values=await AppServices.Api.GetMyPlaylistsAsync(100,offset);offset+=100;
            foreach(var playlist in values.Where(p=>p.Creator?.Id==AppServices.Api.CurrentUser?.Id&&p.SpecialType!=5))
                lists.Add(Ui.Row(playlist.Name,playlist.TrackCount+"首 · "+(playlist.Creator?.Nickname??"我的歌单"),async()=>{
                    if(busy)return;busy=true;error.IsVisible=false;
                    try{await AppServices.Api.AddTracksToPlaylistAsync(playlist.Id,tracks.Select(t=>t.Id));dialog.Dismiss();page.ShowMessage("已收藏到「"+playlist.Name+"」。");}
                    catch(Exception e){error.Text=Ui.ErrorText(e);error.IsVisible=true;}finally{busy=false;}
                },cover:playlist.CoverUrl));
            more.IsVisible=values.Count==100;
            if(lists.Count==0)lists.Add(Ui.Text("还没有可添加歌曲的歌单，点击右上角新建。",14,"Muted"));
        }
        more.Clicked+=async(_,_)=>{try{await Fetch();}catch(Exception e){error.Text=Ui.ErrorText(e);error.IsVisible=true;}};
        stack.Add(Ui.Text("只显示你创建的、可添加歌曲的歌单。",12,"Muted"));
        await Fetch();
        var scroll=new ScrollView{Content=stack,HeightRequest=Math.Min(182+lists.Count*76+(more.IsVisible?48:0),activity.Resources!.DisplayMetrics!.HeightPixels/activity.Resources.DisplayMetrics.Density*.72)};
        scroll.SetDynamicResource(VisualElement.BackgroundColorProperty,"Surface");
        var native=scroll.ToPlatform(page.Handler!.MauiContext!);dialog.SetContentView(native);dialog.DismissEvent+=(_,_)=>completion.TrySetResult();dialog.Show();dialog.Behavior.State=Google.Android.Material.BottomSheet.BottomSheetBehavior.StateExpanded;
        try{await completion.Task;}finally{scroll.Handler?.DisconnectHandler();}
    }
}
