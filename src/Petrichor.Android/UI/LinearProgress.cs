using Google.Android.Material.ProgressIndicator;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
namespace Petrichor.Mobile.UI;

// Indeterminate Material 3 linear progress indicator. It fades in and out with the native show/hide
// animation and keeps its height, so loading never shifts the page below the top app bar.
public sealed class LinearProgress : View
{
    public static readonly BindableProperty IsRunningProperty=BindableProperty.Create(nameof(IsRunning),typeof(bool),typeof(LinearProgress),false);
    public bool IsRunning{get=>(bool)GetValue(IsRunningProperty);set=>SetValue(IsRunningProperty,value);}
    public LinearProgress(){HeightRequest=4;InputTransparent=true;}
}

public sealed class LinearProgressHandler : ViewHandler<LinearProgress,LinearProgressIndicator>
{
    private static readonly PropertyMapper<LinearProgress,LinearProgressHandler> mapper=new(ViewMapper){[nameof(LinearProgress.IsRunning)]=(h,_)=>h.Update()};
    public LinearProgressHandler():base(mapper){}
    protected override LinearProgressIndicator CreatePlatformView()
    {
        var bar=new LinearProgressIndicator(Context){Indeterminate=true,Visibility=global::Android.Views.ViewStates.Invisible};
        bar.SetVisibilityAfterHide((int)global::Android.Views.ViewStates.Invisible);
        bar.ShowAnimationBehavior=BaseProgressIndicator.ShowNone;bar.HideAnimationBehavior=BaseProgressIndicator.HideNone;
        return bar;
    }
    private void Update()
    {
        var bar=PlatformView;
        bar.SetIndicatorColor(Ui.Color("Primary").ToPlatform().ToArgb());bar.TrackColor=Ui.Color("SecondaryContainer").ToPlatform().ToArgb();
        if(VirtualView.IsRunning){if(bar.Visibility!=global::Android.Views.ViewStates.Visible){bar.Alpha=0;bar.Visibility=global::Android.Views.ViewStates.Visible;bar.Animate()!.Alpha(1).SetDuration(Md.Short3).Start();}}
        else if(bar.Visibility==global::Android.Views.ViewStates.Visible)bar.Animate()!.Alpha(0).SetDuration(Md.Medium1).WithEndAction(new Java.Lang.Runnable(()=>{if(VirtualView?.IsRunning!=true)bar.Visibility=global::Android.Views.ViewStates.Invisible;})).Start();
    }
}
