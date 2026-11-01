using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Widget;
using Microsoft.Maui.Platform;
using Paint=Android.Graphics.Paint;
namespace Petrichor.Mobile.UI;

internal sealed class MusicThumbDrawable(SeekBar owner):Drawable
{
    private readonly Paint paint=new(PaintFlags.AntiAlias);
    private float from=4,target=4;
    private long changedAt;
    private int alpha=255;
    private float Density=>owner.Resources!.DisplayMetrics!.Density;
    public override int IntrinsicWidth=>(int)Math.Round(4*Density);
    public override int IntrinsicHeight=>(int)Math.Round(36*Density);
    public override bool IsStateful=>true;
    public override int Opacity=>(int)Format.Translucent;
    public float WidthPixels {
        get{
            var desired=owner.Pressed||owner.HasFocus?2f:4f;
            var now=global::Android.OS.SystemClock.UptimeMillis();
            float Value(){var t=Math.Clamp((now-changedAt)/140f,0,1);return from+(target-from)*(1-(1-t)*(1-t)*(1-t));}
            if(desired!=target){from=Value();target=desired;changedAt=now;}
            return (global::Android.Provider.Settings.Global.GetFloat(owner.Context!.ContentResolver,global::Android.Provider.Settings.Global.AnimatorDurationScale,1)==0?target:Value())*Density;
        }
    }
    protected override bool OnStateChange(int[]? state){InvalidateSelf();return true;}
    public override void Draw(Canvas canvas)
    {
        var width=WidthPixels;var height=(owner.Pressed?36:32)*Density;var x=Bounds.ExactCenterX();var y=Bounds.ExactCenterY();
        paint.Color=Ui.Color("Primary").ToPlatform();paint.Alpha=owner.Enabled?alpha:(int)(alpha*.38);
        canvas.DrawRoundRect(x-width/2,y-height/2,x+width/2,y+height/2,width/2,width/2,paint);
        if(owner.IsShown&&Math.Abs(width/Density-target)>.01f)owner.PostInvalidateOnAnimation();
    }
    public override void SetAlpha(int value){alpha=value;InvalidateSelf();}
    public override void SetColorFilter(ColorFilter? filter){paint.SetColorFilter(filter);InvalidateSelf();}
    protected override void Dispose(bool disposing){if(disposing)paint.Dispose();base.Dispose(disposing);}
}
