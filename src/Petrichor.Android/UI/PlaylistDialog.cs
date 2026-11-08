using Google.Android.Material.Dialog;
using Microsoft.Maui.Platform;
using Petrichor.Core;
using Petrichor.Mobile.Services;
using Entry=Petrichor.Mobile.UI.NativeEntry;
namespace Petrichor.Mobile.UI;

public static partial class Actions
{
    // Material 3 dialog with a text field. The confirm button keeps the dialog open while saving.
    public static async Task NewPlaylist(BasePage page,IReadOnlyList<Track>? tracks=null,Func<Task>? refresh=null,bool closeChooser=false,Action? completed=null)
    {
        if(!await RequireAccount(page))return;
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口");
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stack=new VerticalStackLayout{Spacing=8,Padding=new Thickness(24,0,24,0)};
        var name=new Entry{Placeholder="例如：雨天适合循环",FontSize=16,MaxLength=40};
        stack.Add(DesignControls.Field("歌单名称",name,background:"SurfaceContainerHigh"));
        var privacy=new Switch();SemanticProperties.SetDescription(privacy,"设为私密歌单");
        var privacyRow=Ui.ListItem("设为私密歌单","只有你可以查看",()=>{privacy.IsToggled=!privacy.IsToggled;return Task.CompletedTask;},trailing:privacy,trailingIcon:null);privacyRow.Padding=new Thickness(0,8);stack.Add(privacyRow);
        var error=Ui.Text("",TypeRole.BodySmall,"Error");error.IsVisible=false;stack.Add(error);
        var content=stack.ToPlatform(page.Handler!.MauiContext!);
        var builder=new MaterialAlertDialogBuilder(activity);
        builder.SetTitle("新建歌单");builder.SetIcon(Dialogs.Icon(activity,"playlist"));builder.SetView(content);
        builder.SetPositiveButton("创建",(_,_)=>{});builder.SetNegativeButton("取消",(_,_)=>{});
        var dialog=builder.Create();
        Playlist? created=null;bool saved=false,busy=false;
        dialog.DismissEvent+=(_,_)=>completion.TrySetResult();
        dialog.Window?.SetSoftInputMode(global::Android.Views.SoftInput.StateVisible|global::Android.Views.SoftInput.AdjustResize);
        dialog.Show();
        var create=dialog.GetButton((int)global::Android.Content.DialogButtonType.Positive);var cancel=dialog.GetButton((int)global::Android.Content.DialogButtonType.Negative);
        if(create!=null)create.Click+=async(_,_)=>{
            if(busy)return;busy=true;create.Enabled=false;if(cancel!=null)cancel.Enabled=false;dialog.SetCancelable(false);error.IsVisible=false;
            try
            {
                created??=await AppServices.Api.CreatePlaylistAsync(name.Text??"",privacy.IsToggled);
                name.IsEnabled=false;privacy.IsEnabled=false;
                if(tracks?.Count>0)await AppServices.Api.AddTracksToPlaylistAsync(created.Id,tracks.Select(t=>t.Id));
                saved=true;dialog.Dismiss();
            }
            catch(Exception e){error.Text=(created==null?"":"歌单已创建，再次点击会重试添加歌曲。")+Ui.ErrorText(e);error.IsVisible=true;}
            finally{busy=false;create.Enabled=true;if(cancel!=null)cancel.Enabled=true;dialog.SetCancelable(true);}
        };
        name.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150),()=>name.Focus());
        try{await completion.Task;}finally{stack.Handler?.DisconnectHandler();dialog.Dispose();}
        if(saved){completed?.Invoke();if(closeChooser)await Ui.Pop();else if(refresh!=null)await refresh();page.ShowMessage("歌单已创建。");}
    }

    public static async Task ChoosePlaylist(BasePage page,IReadOnlyList<Track> tracks)
    {
        if(!await RequireAccount(page))return;
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口");
        using var dialog=new Google.Android.Material.BottomSheet.BottomSheetDialog(activity);
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stack=new VerticalStackLayout{Spacing=0,Padding=new Thickness(0,0,0,16)};
        var heading=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Padding=new Thickness(24,0,12,0)};
        heading.Add(Ui.Line("收藏到歌单",TypeRole.TitleLarge));heading.Add(Ui.Button("新建",()=>page.Run(()=>NewPlaylist(page,tracks,completed:()=>dialog.Dismiss())),"text","add"),1);stack.Add(heading);
        var detail=Ui.Line(tracks.Count==1?tracks[0].Name+" · "+tracks[0].ArtistText:"已选择 "+tracks.Count+" 首歌曲",TypeRole.BodyMedium,"OnSurfaceVariant");detail.Margin=new Thickness(24,0,24,8);stack.Add(detail);
        var error=Ui.Text("",TypeRole.BodySmall,"Error");error.Margin=new Thickness(24,0);error.IsVisible=false;stack.Add(error);
        var lists=new VerticalStackLayout{Spacing=0};stack.Add(lists);int offset=0;bool busy=false;
        var more=Ui.Button("加载更多",()=>Task.CompletedTask,"text");more.HorizontalOptions=LayoutOptions.Center;stack.Add(more);
        async Task Fetch()
        {
            var values=await AppServices.Api.GetMyPlaylistsAsync(100,offset);offset+=100;
            foreach(var playlist in values.Where(p=>p.Creator?.Id==AppServices.Api.CurrentUser?.Id&&p.SpecialType!=5))
            {
                var item=Ui.ListItem(playlist.Name,playlist.TrackCount+"首",async()=>{
                    if(busy)return;busy=true;error.IsVisible=false;
                    try{await AppServices.Api.AddTracksToPlaylistAsync(playlist.Id,tracks.Select(t=>t.Id));dialog.Dismiss();page.ShowMessage("已收藏到「"+playlist.Name+"」。");}
                    catch(Exception e){error.Text=Ui.ErrorText(e);error.IsVisible=true;}finally{busy=false;}
                },cover:playlist.CoverUrl,trailingIcon:null);
                item.Padding=new Thickness(24,8);lists.Add(item);
            }
            more.IsVisible=values.Count==100;
            if(lists.Count==0){var none=Ui.Text("还没有可添加歌曲的歌单，点击「新建」创建一个。",TypeRole.BodyMedium,"OnSurfaceVariant");none.Margin=new Thickness(24,16);lists.Add(none);}
        }
        more.Clicked+=async(_,_)=>{try{await Fetch();}catch(Exception e){error.Text=Ui.ErrorText(e);error.IsVisible=true;}};
        var note=Ui.Text("只显示你创建的、可添加歌曲的歌单。",TypeRole.BodySmall,"OnSurfaceVariant");note.Margin=new Thickness(24,8,24,0);stack.Add(note);
        await Fetch();
        var scroll=new ScrollView{Content=stack,HeightRequest=Math.Min(160+lists.Count*72+(more.IsVisible?48:0),activity.Resources!.DisplayMetrics!.HeightPixels/activity.Resources.DisplayMetrics.Density*.72)};
        var layout=new global::Android.Widget.LinearLayout(activity){Orientation=global::Android.Widget.Orientation.Vertical};
        layout.AddView(new Google.Android.Material.BottomSheet.BottomSheetDragHandleView(activity),new global::Android.Widget.LinearLayout.LayoutParams(-1,-2));
        layout.AddView(scroll.ToPlatform(page.Handler!.MauiContext!),new global::Android.Widget.LinearLayout.LayoutParams(-1,-2));
        dialog.SetContentView(layout);dialog.DismissEvent+=(_,_)=>completion.TrySetResult();dialog.Show();
        dialog.Behavior.SkipCollapsed=true;dialog.Behavior.State=Google.Android.Material.BottomSheet.BottomSheetBehavior.StateExpanded;
        try{await completion.Task;}finally{scroll.Handler?.DisconnectHandler();}
    }
}
