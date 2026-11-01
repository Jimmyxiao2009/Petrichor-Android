using Android.Animation;
using Android.Provider;
using Android.Views.Animations;
using Petrichor.Mobile.Pages;
using NativeView=Android.Views.View;
namespace Petrichor.Mobile.UI;

// The navigation semaphore remains held until motion ends, including hardware Back.
internal static class PageMotion
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page,TaskCompletionSource> entries=new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlayerPage,View> originCovers=new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,ValueAnimator> animations=new();
    private static TaskCompletionSource BeginEntry(Page page){var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);entries.Remove(page);entries.Add(page,done);return done;}
    public static Task WaitForEntryAsync(Page page,CancellationToken token)=>entries.TryGetValue(page,out var done)?done.Task.WaitAsync(token):Task.CompletedTask;
    internal static bool Enabled => ValueAnimator.AreAnimatorsEnabled()&&Settings.Global.GetFloat(global::Android.App.Application.Context.ContentResolver,Settings.Global.AnimatorDurationScale,1)>0;
    private static bool Vertical(Page page)=>page is PlayerPage or CommentsPage or LyricsPage;
    private static void Reset(VisualElement view){view.Opacity=1;view.TranslationX=0;view.TranslationY=0;if(view.Handler?.PlatformView is NativeView native){native.Alpha=1;native.TranslationX=native.TranslationY=0;}}
    public static async Task PushAsync(Page page)
    {
        if(page is PlayerPage originPage&&MiniPlayer.ActiveCover is{} origin){originCovers.Remove(originPage);originCovers.Add(originPage,origin);}
        if(page is not ContentPage {Content:{} view}||!Enabled){await Shell.Current.Navigation.PushAsync(page,false);return;}
        using var artwork=page is PlayerPage?ArtworkTransition.Capture(MiniPlayer.ActiveCover):null;
        view.Opacity=0;
        var ready=BeginEntry(page);
        if(page is PlayerPage)view.TranslationY=artwork==null?48:0;
        else if(Vertical(page))view.TranslationY=56;else view.TranslationX=48;
        try{
            await Shell.Current.Navigation.PushAsync(page,false);
            if(page is PlayerPage target&&artwork!=null)await artwork.PrepareAsync(target.TransitionCover);
            await Task.WhenAll(Animate(view,true,Vertical(page)),page is PlayerPage player&&artwork!=null?artwork.AnimateTo(player.TransitionCover):Task.CompletedTask);
        }
        finally{Reset(view);ready.TrySetResult();}
    }
    public static async Task PopAsync()
    {
        var page=Shell.Current.Navigation.NavigationStack.LastOrDefault();
        if(page is PlayerPage player&&Enabled){
            var mini=(Shell.Current.Navigation.NavigationStack.TakeLast(2).FirstOrDefault() as BasePage)?.MiniCover;
            if(mini==null&&originCovers.TryGetValue(player,out var origin))mini=origin;
            if(mini!=null){using var artwork=ArtworkTransition.Capture(player.TransitionCover);
                if(artwork!=null){await Shell.Current.Navigation.PopAsync(false);await artwork.AnimateTo(mini);return;}}
        }
        if(page is ContentPage {Content:{} view}&&Enabled){
            try{await Animate(view,false,Vertical(page));await Shell.Current.Navigation.PopAsync(false);}
            finally{Reset(view);}
        }else await Shell.Current.Navigation.PopAsync(false);
    }
    public static async void ShowTab(Page? page,int direction=1)
    {
        if(page is not ContentPage {Content:{} view}||!Enabled)return;
        var ready=BeginEntry(page);
        view.TranslationX=40*direction;view.TranslationY=0;view.Opacity=.15;
        try{await Animate(view,true,false,240);}
        finally{if(!animations.TryGetValue(view,out _))Reset(view);ready.TrySetResult();}
    }
    // Animate native render properties, avoiding MAUI property propagation on
    // every frame. Cached tab pages use the same path as newly constructed pages.
    internal static async Task Animate(VisualElement view,bool entering,bool vertical,uint duration=280)
    {
        if(!Enabled){Reset(view);return;}
        if(animations.TryGetValue(view,out var previous))previous.Cancel();
        if(view.Handler?.PlatformView is not NativeView)await Task.Yield();
        if(view.Handler?.PlatformView is not NativeView native){Reset(view);return;}
        var start=entering?(vertical?view.TranslationY:view.TranslationX):0;
        var alpha=view.Opacity;
        var density=native.Resources!.DisplayMetrics!.Density;
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var animator=ValueAnimator.OfFloat(0,1)!;
        using var easing=entering?new PathInterpolator(.2f,0,0,1):new PathInterpolator(.4f,0,1,1);
        animations.Remove(view);animations.Add(view,animator);
        animator.SetDuration(entering?duration:180);animator.SetInterpolator(easing);
        void Place(float t){
            native.Alpha=(float)(entering?alpha+(1-alpha)*t:1-t);
            var offset=(float)(entering?start*(1-t):36*t)*density;
            if(vertical)native.TranslationY=offset;else native.TranslationX=offset;
        }
        animator.Update+=(_,_)=>Place(animator.AnimatedFraction);
        animator.AnimationEnd+=(_,_)=>done.TrySetResult();
        animator.AnimationCancel+=(_,_)=>done.TrySetResult();
        Place(0);animator.Start();
        try{await done.Task;}
        finally{
            if(animations.TryGetValue(view,out var current)&&ReferenceEquals(current,animator)){
                animations.Remove(view);
                view.Opacity=native.Alpha;
                if(vertical)view.TranslationY=native.TranslationY/density;else view.TranslationX=native.TranslationX/density;
            }
        }
    }
}