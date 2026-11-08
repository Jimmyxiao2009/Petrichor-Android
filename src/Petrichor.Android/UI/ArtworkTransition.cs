using Android.Animation;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using View=Microsoft.Maui.Controls.View;
using NativeView=Android.Views.View;
namespace Petrichor.Mobile.UI;

// One drawable in the window overlay owns the artwork throughout navigation.
// Explicit invalidation avoids relying on a detached overlay child's animator.
internal sealed class ArtworkTransition:IDisposable
{
    private readonly Bitmap? bitmap;
    private readonly Drawable content;
    private readonly ArtworkDrawable artwork;
    private readonly ViewOverlay overlay;
    private readonly ViewGroup root;
    private readonly NativeView source;
    private readonly float sourceAlpha,radius;
    private NativeView? target;
    private float targetAlpha=1;
    private View? prepared;
    private ArtworkTransition(NativeView source,ViewGroup decor,double radiusDp)
    {
        root=decor;this.source=source;sourceAlpha=source.Alpha;
        var clone=FindImage(source)?.Drawable?.GetConstantState()?.NewDrawable(source.Resources)?.Mutate();
        if(clone!=null)content=clone;
        else{
            bitmap=Bitmap.CreateBitmap(source.Width,source.Height,Bitmap.Config.Argb8888!)!;
            using(var canvas=new Canvas(bitmap))source.Draw(canvas);
            content=new BitmapDrawable(source.Resources,bitmap);
        }
        radius=(float)(radiusDp*source.Resources!.DisplayMetrics!.Density);
        artwork=new ArtworkDrawable(content){Radius=radius};
        var location=new int[2];var origin=new int[2];source.GetLocationOnScreen(location);root.GetLocationOnScreen(origin);
        artwork.SetBounds(location[0]-origin[0],location[1]-origin[1],location[0]-origin[0]+source.Width,location[1]-origin[1]+source.Height);
        overlay=root.Overlay!;overlay.Add(artwork);source.Alpha=0;root.Invalidate();
    }
    private static ImageView? FindImage(NativeView view){
        if(view is ImageView image)return image;
        if(view is ViewGroup group)for(var i=0;i<group.ChildCount;i++)if(FindImage(group.GetChildAt(i)!) is{} child)return child;
        return null;
    }
    public static ArtworkTransition? Capture(View? view,double radius)
    {
        if(!PageMotion.Enabled)return null;
        if(view?.Handler?.PlatformView is not NativeView{Width:>0,Height:>0,IsShown:true} native||MainActivity.Instance?.Window?.DecorView is not ViewGroup decor)return null;
        try{return new(native,decor,radius);}catch(Java.Lang.RuntimeException){return null;}
    }
    public async Task PrepareAsync(View view)
    {
        if (prepared == view) return;
        void HideTarget() {
            if (target != null || view.Handler?.PlatformView is not NativeView native) return;
            target = native; targetAlpha = native.Alpha; native.Alpha = 0;
        }
        HideTarget();
        // Pre-draw runs after Android measurement/layout. Polling for three
        // identical frames added a visible pause before every open and close.
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = new BeforeDraw(() => {
            HideTarget();
            if (target is { Width: > 0, Height: > 0, IsAttachedToWindow: true } && !target.IsLayoutRequested && !root.IsLayoutRequested &&
                Math.Abs(target.Width - target.Height) <= 2 * target.Resources!.DisplayMetrics!.Density) done.TrySetResult();
        });
        var observer = root.ViewTreeObserver!;
        observer.AddOnPreDrawListener(listener);
        root.PostInvalidateOnAnimation();
        try { await Task.WhenAny(done.Task, Task.Delay(250)); }
        finally { if (observer.IsAlive) observer.RemoveOnPreDrawListener(listener); }
        if (done.Task.IsCompleted) prepared = view;
    }
    private sealed class BeforeDraw(Action ready) : Java.Lang.Object, ViewTreeObserver.IOnPreDrawListener
    {
        public bool OnPreDraw() { ready(); return true; }
    }
    public async Task AnimateTo(View view,double radiusDp)
    {
        await PrepareAsync(view);
        if(target is not {Width:>0,Height:>0} native)return;
        var point=new int[2];var origin=new int[2];root.GetLocationOnScreen(origin);
        var endRadius=(float)(radiusDp*native.Resources!.DisplayMetrics!.Density);
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timer=ValueAnimator.OfFloat(0,1)!;using var easing=Md.NativeEmphasizedDecelerate();timer.SetDuration(Md.Medium3);timer.SetInterpolator(easing);
        using var from=new Android.Graphics.Rect(artwork.Bounds);using var dirty=new Android.Graphics.Rect();
        void Place(float t){
            int Mix(int a,int b)=>(int)Math.Round(a+(b-a)*t);
            // The target rides a fragment that is still rising. Re-measure it every frame so the
            // artwork lands on the moving cover instead of the spot it started from.
            native.GetLocationOnScreen(point);
            var left=point[0]-origin[0];var top=point[1]-origin[1];
            var right=left+native.Width;var bottom=top+native.Height;
            // Repaint only the strip the artwork crosses this frame. Invalidating the decor view
            // redraws both animating fragments, the mini player and the navigation bar every
            // frame, which visibly stutters the transition.
            dirty.Set(Math.Min(artwork.Bounds.Left,left),Math.Min(artwork.Bounds.Top,top),Math.Max(artwork.Bounds.Right,right),Math.Max(artwork.Bounds.Bottom,bottom));
            artwork.Radius=radius+(endRadius-radius)*t;artwork.SetBounds(Mix(from.Left,left),Mix(from.Top,top),Mix(from.Right,right),Mix(from.Bottom,bottom));root.Invalidate(dirty);
        }
        timer.Update+=(_,_)=>Place(timer.AnimatedFraction);timer.AnimationEnd+=(_,_)=>done.TrySetResult();timer.AnimationCancel+=(_,_)=>done.TrySetResult();timer.Start();
        await Task.WhenAny(done.Task,Task.Delay(700));timer.Cancel();Place(1);native.Alpha=targetAlpha;target=null;
    }
    private sealed class ArtworkDrawable(Drawable content):Drawable
    {
        private readonly Android.Graphics.Path clip=new();
        public float Radius{get;set;}
        public override int Opacity=>(int)Format.Translucent;
        public override void Draw(Canvas canvas){
            var b=Bounds;clip.Reset();clip.AddRoundRect(b.Left,b.Top,b.Right,b.Bottom,Radius,Radius,Android.Graphics.Path.Direction.Cw!);
            var saved=canvas.Save();canvas.ClipPath(clip);content.SetBounds(b.Left,b.Top,b.Right,b.Bottom);content.Draw(canvas);canvas.RestoreToCount(saved);
        }
        public override void SetAlpha(int alpha){content.Alpha=alpha;InvalidateSelf();}
        public override void SetColorFilter(ColorFilter? filter){content.SetColorFilter(filter);InvalidateSelf();}
        protected override void Dispose(bool disposing){if(disposing)clip.Dispose();base.Dispose(disposing);}
    }
    public void Dispose(){if(target!=null)target.Alpha=targetAlpha;source.Alpha=sourceAlpha;overlay.Remove(artwork);root.Invalidate();artwork.Dispose();content.Dispose();bitmap?.Dispose();}
}
