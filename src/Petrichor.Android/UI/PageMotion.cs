using Android.Animation;
using Android.Provider;
using AndroidX.Fragment.App;
using Microsoft.Maui.Platform;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
using NativeView=Android.Views.View;
namespace Petrichor.Mobile.UI;

// Page motion follows the Material 3 transition patterns:
// - push/pop: shared axis X, run by Shell's fragment transaction so both pages move together;
// - full-screen pages (player, comments): rise over the current page, which stays drawn underneath;
// - navigation bar destinations: fade through.
// The navigation semaphore in Ui stays held until motion ends, including hardware Back.
internal static class PageMotion
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Page,TaskCompletionSource> entries=new();
    private static bool vertical;
    private static TaskCompletionSource BeginEntry(Page page){var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);entries.Remove(page);entries.Add(page,done);return done;}
    public static Task WaitForEntryAsync(Page page,CancellationToken token)=>entries.TryGetValue(page,out var done)?done.Task.WaitAsync(token):Task.CompletedTask;
    internal static bool Enabled => ValueAnimator.AreAnimatorsEnabled()&&Settings.Global.GetFloat(global::Android.App.Application.Context.ContentResolver,Settings.Global.AnimatorDurationScale,1)>0;
    private static bool Vertical(Page page)=>page is PlayerPage or CommentsPage or LyricsPage or FmPage or LoginPage;
    // Shell resolves its navigation task when the fragment animation ends; never let a missed
    // callback hold the navigation lock.
    private static Task Bounded(Task navigation)=>Task.WhenAny(navigation,Task.Delay(1200));

    public static async Task PushAsync(Page page)
    {
        var navigation=Shell.Current.Navigation;
        if(!Enabled){await navigation.PushAsync(page,false);return;}
        var ready=BeginEntry(page);vertical=Vertical(page);
        using var artwork=page is PlayerPage?ArtworkTransition.Capture(MiniPlayer.Shared?.ArtworkView,Md.ShapeSmall):null;
        try{
            var push=navigation.PushAsync(page,true);
            if(page is PlayerPage player&&artwork!=null)await artwork.AnimateTo(player.TransitionCover,player.TransitionRadius);
            await Bounded(push);
        }
        finally{ready.TrySetResult();}
    }
    public static async Task PopAsync()
    {
        var navigation=Shell.Current.Navigation;var page=navigation.NavigationStack.LastOrDefault();
        if(!Enabled||page==null){await navigation.PopAsync(false);return;}
        vertical=Vertical(page);
        var stack=navigation.NavigationStack;
        // The cover flies back only when the revealed page shows the navigation bar and mini player,
        // which Shell restores as soon as the previous page is displayed.
        var toMini=stack.Count>1&&stack[^2] is BasePage{ShowsChrome:true}&&AppServices.Playback.Queue.Current!=null&&MiniPlayer.Shared!=null;
        if(page is PlayerPage player&&toMini&&ArtworkTransition.Capture(player.TransitionCover,player.TransitionRadius) is{} artwork)
        {
            using(artwork){
                var pop=navigation.PopAsync(true);
                await artwork.AnimateTo(MiniPlayer.Shared!.ArtworkView,Md.ShapeSmall);
                await Bounded(pop);
            }
            return;
        }
        await Bounded(navigation.PopAsync(true));
    }
    // Called by Shell for every animated fragment transaction.
    public static void Configure(ShellNavigationSource source,FragmentTransaction transaction,Android.Views.ViewGroup? container)
    {
        var rise=vertical;vertical=false;
        switch(source)
        {
            case ShellNavigationSource.Push:
                transaction.SetCustomAnimations(rise?Resource.Animation.md_rise_enter:Resource.Animation.md_axis_enter,rise?Resource.Animation.md_hold:Resource.Animation.md_axis_exit);break;
            case ShellNavigationSource.Pop:
            case ShellNavigationSource.PopToRoot:
                transaction.SetCustomAnimations(rise?0:Resource.Animation.md_axis_pop_enter,rise?Resource.Animation.md_rise_exit:Resource.Animation.md_axis_pop_exit);break;
            default:
                transaction.SetCustomAnimations(Resource.Animation.md_fade_enter,Resource.Animation.md_fade_exit);break;
        }
        // Shell paints the page container black while fragments animate. The fades in these
        // transitions would expose it, so repaint it with the surface right after Shell does.
        if(container!=null){var surface=Ui.Color("Surface").ToPlatform();container.Post(()=>{if(container.Background!=null)container.SetBackgroundColor(surface);});}
    }
    // Reselecting the current destination returns to its root, then to the top of the page.
    public static void Reselect(ShellSection section)
    {
        if(section.Stack.Count>1){_=Ui.PopToRoot(section);return;}
        if(Shell.Current?.CurrentPage is BasePage page)page.ScrollToTop();
    }
    // Fade through: the incoming destination fades in while scaling up from 92%.
    public static void FadeThrough(Page? page)
    {
        if(page is not ContentPage{Content:{} view}||!Enabled)return;
        var ready=BeginEntry(page);
        void Run()
        {
            if(view.Handler?.PlatformView is not NativeView native){ready.TrySetResult();return;}
            native.Animate()?.Cancel();native.Alpha=0;native.ScaleX=native.ScaleY=.92f;
            native.Animate()!.Alpha(1).ScaleX(1).ScaleY(1).SetStartDelay(Md.Short2-10).SetDuration(Md.Medium1).SetInterpolator(Md.NativeEmphasizedDecelerate())
                .WithEndAction(new Java.Lang.Runnable(()=>{native.Alpha=1;native.ScaleX=native.ScaleY=1;ready.TrySetResult();})).Start();
        }
        if(view.Handler!=null)Run();else view.Dispatcher.Dispatch(Run);
        // A cancelled animation skips its end action; page loading must not wait on it.
        _=Task.Delay((int)Md.Long2+200).ContinueWith(_=>ready.TrySetResult(),TaskScheduler.Default);
    }
    // Native fade/rise for content that replaces a placeholder (lists after their skeleton).
    public static void Reveal(VisualElement view,double distance=12)
    {
        if(!Enabled||view.Handler?.PlatformView is not NativeView native)return;
        var density=native.Resources!.DisplayMetrics!.Density;
        native.Animate()?.Cancel();native.Alpha=0;native.TranslationY=(float)(distance*density);
        native.Animate()!.Alpha(1).TranslationY(0).SetDuration(Md.Medium2).SetInterpolator(Md.NativeEmphasizedDecelerate()).Start();
    }
    // Cross-fade between two panes that share one slot (e.g. login methods).
    public static void Swap(VisualElement outgoing,VisualElement incoming)
    {
        outgoing.IsVisible=false;incoming.IsVisible=true;
        if(!Enabled)return;
        incoming.Opacity=0;incoming.TranslationY=8;
        _=incoming.FadeToAsync(1,Md.Medium1,Md.StandardDecelerate);_=incoming.TranslateToAsync(0,0,Md.Medium2,Md.EmphasizedDecelerate);
    }
}
