namespace Petrichor.Mobile.UI;

// Edge-to-edge Android 15 can keep the MAUI page at full window height even
// with adjustResize. Inset only the portion actually covered by the keyboard.
internal sealed class KeyboardAvoidance(ContentPage page,View body)
{
    private global::Android.Views.View? decor;
    private double bottom;
    public void Attach()
    {
        Detach();decor=MainActivity.Instance?.Window?.DecorView;
        if(decor?.ViewTreeObserver is{} observer)observer.GlobalLayout+=Layout;
    }
    public void Detach()
    {
        if(decor?.ViewTreeObserver is{IsAlive:true} observer)observer.GlobalLayout-=Layout;
        decor=null;bottom=0;body.Margin=new Thickness(0);
    }
    private void Layout(object? sender,EventArgs e)
    {
        if(decor==null||page.Content?.Handler?.PlatformView is not global::Android.Views.View root)return;
        using var visible=new global::Android.Graphics.Rect();decor.GetWindowVisibleDisplayFrame(visible);
        var location=new int[2];root.GetLocationOnScreen(location);
        var covered=Math.Max(0,location[1]+root.Height-visible.Bottom);
        var insets=AndroidX.Core.View.ViewCompat.GetRootWindowInsets(decor);
        var navigation=insets?.GetInsets(AndroidX.Core.View.WindowInsetsCompat.Type.SystemBars())?.Bottom??0;
        covered=Math.Max(0,covered-navigation);
        // Small differences come from system bar insets, not the IME.
        var density=decor.Resources!.DisplayMetrics!.Density;
        var next=covered>100*density?covered/density:0;
        if(Math.Abs(next-bottom)<1)return;
        bottom=next;body.Margin=new Thickness(0,0,0,next);
    }
}
