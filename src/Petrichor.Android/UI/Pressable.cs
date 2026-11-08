using Android.Content.Res;
using Android.Graphics.Drawables;
using Microsoft.Maui.Platform;
using Petrichor.Mobile.Services;
using AView=Android.Views.View;
namespace Petrichor.Mobile.UI;

// Material state layer for any MAUI view: a native ripple foreground plus click and long-press handling.
// Tap gesture recognizers give no press feedback, which made cards and rows feel unresponsive.
internal static class Pressable
{
    public static T Attach<T>(T view,Func<Task>? tap,Func<Task>? hold=null,double radius=0,string? description=null,double inset=0,bool oval=false,string state="OnSurface") where T:View
    {
        _=new PressBinding(view,tap,hold,radius,inset,oval,state);
        if(description!=null)SemanticProperties.SetDescription(view,description);
        return view;
    }
    public static Drawable Ripple(global::Android.Content.Context context,double radius,double inset=0,bool oval=false,string state="OnSurface")
    {
        var density=context.Resources!.DisplayMetrics!.Density;
        var mask=new GradientDrawable();mask.SetColor(global::Android.Graphics.Color.White);
        if(oval)mask.SetShape(ShapeType.Oval);else mask.SetCornerRadius((float)(radius*density));
        Drawable shape=inset>0?new InsetDrawable(mask,(int)Math.Round(inset*density)):mask;
        return new RippleDrawable(ColorStateList.ValueOf(Ui.Color(state).WithAlpha(Md.PressedState).ToPlatform()),null,shape);
    }
    private sealed class PressBinding
    {
        private readonly View view;
        private readonly Func<Task>? tap,hold;
        private readonly double radius,inset;
        private readonly bool oval;
        private readonly string state;
        private AView? native;
        private bool busy;
        private long ignoreClickUntil;
        public PressBinding(View view,Func<Task>? tap,Func<Task>? hold,double radius,double inset,bool oval,string state)
        {
            this.view=view;this.tap=tap;this.hold=hold;this.radius=radius;this.inset=inset;this.oval=oval;this.state=state;
            view.HandlerChanging+=(_,_)=>Disconnect();view.HandlerChanged+=(_,_)=>Connect();Connect();
        }
        private void Connect()
        {
            Disconnect();if(view.Handler?.PlatformView is not AView platform)return;native=platform;
            platform.Clickable=true;platform.Focusable=true;platform.LongClickable=hold!=null;
            platform.Foreground=Ripple(platform.Context!,radius,inset,oval,state);
            platform.Click+=Clicked;if(hold!=null)platform.LongClick+=Held;
        }
        private void Disconnect()
        {
            if(native==null)return;
            native.Click-=Clicked;native.LongClick-=Held;native=null;
        }
        private async void Clicked(object? sender,EventArgs e)
        {
            if(busy||tap==null||Environment.TickCount64<ignoreClickUntil||!view.IsEnabled)return;
            busy=true;
            try{await tap();}
            catch(OperationCanceledException){}
            catch(Exception error){(Shell.Current?.CurrentPage as BasePage)?.ShowMessage(Ui.ErrorText(error));}
            finally{busy=false;}
        }
        private async void Held(object? sender,AView.LongClickEventArgs e)
        {
            e.Handled=true;if(hold==null||!view.IsEnabled)return;
            ignoreClickUntil=Environment.TickCount64+800;
            native?.PerformHapticFeedback(global::Android.Views.FeedbackConstants.LongPress);
            try{await hold();}
            catch(OperationCanceledException){}
            catch(Exception error){(Shell.Current?.CurrentPage as BasePage)?.ShowMessage(Ui.ErrorText(error));}
        }
    }
}

// A Material Symbols glyph as a tintable drawable. Navigation bar icons need state-aware tint
// (filled when selected) which MAUI's FontImageSource cannot express.
internal sealed class GlyphDrawable:Drawable
{
    private static readonly Dictionary<string,global::Android.Graphics.Typeface> faces=[];
    private readonly global::Android.Graphics.Paint paint=new(global::Android.Graphics.PaintFlags.AntiAlias){TextAlign=global::Android.Graphics.Paint.Align.Center};
    private readonly string glyph;
    private readonly int size;
    private ColorStateList? tint;
    private int alpha=255;
    public GlyphDrawable(global::Android.Content.Context context,string name,bool filled,double sizeDp=24)
    {
        glyph=Ui.Glyphs.GetValueOrDefault(name,Ui.Glyphs["headphones"]);
        var file=filled?"MaterialIcons-Regular.ttf":"MaterialIconsOutlined-Regular.otf";
        if(!faces.TryGetValue(file,out var face))faces[file]=face=global::Android.Graphics.Typeface.CreateFromAsset(context.Assets,file)!;
        paint.SetTypeface(face);size=(int)Math.Round(sizeDp*context.Resources!.DisplayMetrics!.Density);paint.TextSize=size;
    }
    public override int IntrinsicWidth=>size;
    public override int IntrinsicHeight=>size;
    public override int Opacity=>(int)global::Android.Graphics.Format.Translucent;
    public override bool IsStateful=>tint?.IsStateful==true;
    public override void SetTintList(ColorStateList? value){tint=value;UpdateColor(GetState());InvalidateSelf();}
    protected override bool OnStateChange(int[]? state)=>UpdateColor(state);
    private bool UpdateColor(int[]? state)
    {
        var color=tint==null?global::Android.Graphics.Color.Black.ToArgb():tint.GetColorForState(state,new global::Android.Graphics.Color(tint.DefaultColor));
        var value=global::Android.Graphics.Color.Argb(global::Android.Graphics.Color.GetAlphaComponent(color)*alpha/255,global::Android.Graphics.Color.GetRedComponent(color),global::Android.Graphics.Color.GetGreenComponent(color),global::Android.Graphics.Color.GetBlueComponent(color));
        if(paint.Color.ToArgb()==value.ToArgb())return false;
        paint.Color=value;return true;
    }
    public override void Draw(global::Android.Graphics.Canvas canvas)
    {
        var b=Bounds;var y=b.ExactCenterY()-(paint.Descent()+paint.Ascent())/2;
        canvas.DrawText(glyph,b.ExactCenterX(),y,paint);
    }
    public override void SetAlpha(int value){alpha=value;UpdateColor(GetState());InvalidateSelf();}
    public override void SetColorFilter(global::Android.Graphics.ColorFilter? filter){paint.SetColorFilter(filter);InvalidateSelf();}
    protected override void Dispose(bool disposing){if(disposing)paint.Dispose();base.Dispose(disposing);}
}
