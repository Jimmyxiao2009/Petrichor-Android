using Android.Content;
using Android.Content.Res;
using Google.Android.Material.Dialog;
using Microsoft.Maui.Platform;
namespace Petrichor.Mobile.UI;

// Material 3 basic dialogs (28dp corner, surface-container-high, headline small) built natively so their
// shape and colors come from the themed activity rather than MAUI's generic alert.
internal static class Dialogs
{
    public static Task<bool> Confirm(string title,string message,string accept,string cancel="取消",bool destructive=false,string? icon=null)
    {
        var activity=MainActivity.Instance;if(activity==null)return Task.FromResult(false);
        var done=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder=new MaterialAlertDialogBuilder(activity);builder.SetTitle(title);builder.SetMessage(message);
        builder.SetPositiveButton(accept,(_,_)=>done.TrySetResult(true));builder.SetNegativeButton(cancel,(_,_)=>done.TrySetResult(false));
        if(icon!=null)builder.SetIcon(Icon(activity,icon));
        var dialog=builder.Create();dialog.DismissEvent+=(_,_)=>done.TrySetResult(false);dialog.Show();
        if(destructive)dialog.GetButton((int)DialogButtonType.Positive)?.SetTextColor(Ui.Color("Error").ToPlatform());
        return done.Task;
    }
    public static Task Alert(string title,string message,string accept="知道了")
    {
        var activity=MainActivity.Instance;if(activity==null)return Task.CompletedTask;
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder=new MaterialAlertDialogBuilder(activity);builder.SetTitle(title);builder.SetMessage(message);builder.SetPositiveButton(accept,(_,_)=>done.TrySetResult());
        var dialog=builder.Create();dialog.DismissEvent+=(_,_)=>done.TrySetResult();dialog.Show();
        return done.Task;
    }
    public static global::Android.Graphics.Drawables.Drawable Icon(Context context,string name)
    {
        var icon=new GlyphDrawable(context,name,false);icon.SetTintList(ColorStateList.ValueOf(Ui.Color("Secondary").ToPlatform()));return icon;
    }
}
