using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Widget;
using Microsoft.Maui.Platform;
using Paint=Android.Graphics.Paint;
using Path=Android.Graphics.Path;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

// Retain the native SeekBar for accessibility, keyboard and drag behaviour.
// Only its rail is custom: neither rail is painted underneath the thumb.
internal sealed class WaveTrackDrawable(SeekBar owner) : Drawable
{
    private readonly Paint paint=new(PaintFlags.AntiAlias);
    private readonly Path wave=new();
    private float phase,amplitude;
    private long lastFrame;
    public override int Opacity=>(int)Format.Translucent;
    public override void SetAlpha(int alpha){}
    public override void SetColorFilter(ColorFilter? filter){}
    protected override bool OnLevelChange(int level){InvalidateSelf();return true;}
    public override void Draw(Canvas canvas)
    {
        var d=owner.Resources!.DisplayMetrics!.Density;
        var b=Bounds;var width=b.Width();var center=b.ExactCenterY();
        var fraction=Math.Clamp((owner.Progress-owner.Min)/(float)Math.Max(1,owner.Max-owner.Min),0,1);
        // AbsSeekBar draws the thumb in a coordinate space shifted by ThumbOffset.
        // Use its actual bounds, including native padding, rather than guessing its centre.
        var mirrored=owner.LayoutDirection==global::Android.Views.LayoutDirection.Rtl;
        var x=owner.Thumb is{} thumb?thumb.Bounds.ExactCenterX()-owner.ThumbOffset:b.Left+width*fraction;
        if(mirrored)x=b.Left+b.Right-x;
        x=Math.Clamp(x,b.Left,b.Right);
        var now=global::Android.OS.SystemClock.UptimeMillis();var delta=lastFrame==0?0:Math.Clamp((now-lastFrame)/1000f,0,.05f);lastFrame=now;
        var motionEnabled=global::Android.Animation.ValueAnimator.AreAnimatorsEnabled();
        var playing=PlaybackControllerIsPlaying();
        var targetAmplitude=playing&&!owner.Pressed?1f:0f;
        amplitude=motionEnabled?amplitude+(targetAmplitude-amplitude)*Math.Min(1,delta*10):targetAmplitude;
        if(playing&&motionEnabled&&!owner.Pressed)phase=(phase+delta*12*d)%(24*d);
        var halfThumb=owner.Thumb is MusicThumbDrawable handle?handle.WidthPixels/2:2*d;var stroke=4*d;
        // Six dp of visible space from each handle edge to the rounded rail cap.
        var gap=halfThumb+6*d+stroke/2;
        paint.StrokeWidth=stroke;paint.StrokeCap=Paint.Cap.Round;paint.SetStyle(Paint.Style.Stroke);
        canvas.Save();if(mirrored){canvas.Translate(b.Left+b.Right,0);canvas.Scale(-1,1);}
        paint.Color=Ui.Color("SecondaryContainer").ToPlatform();paint.Alpha=owner.Enabled?255:97;
        if(x+gap<b.Right)canvas.DrawLine(x+gap,center,b.Right,center,paint);
        paint.Color=Ui.Color("Primary").ToPlatform();paint.Alpha=owner.Enabled?255:97;
        var end=x-gap;
        if(end>b.Left){
            wave.Rewind();
            float Y(float px){var u=Math.Clamp((end-px)/(24*d),0,1);var envelope=u*u*(3-2*u);return center+(float)Math.Sin((px-b.Left+phase)/(24*d)*Math.PI*2)*4.5f*d*amplitude*envelope;}
            wave.MoveTo(b.Left,Y(b.Left));for(float px=b.Left+Math.Max(1,d);px<end;px+=Math.Max(1,d))wave.LineTo(px,Y(px));wave.LineTo(end,center);canvas.DrawPath(wave,paint);
        }
        if(b.Right-2*d-(x+halfThumb)>10*d){paint.SetStyle(Paint.Style.Fill);canvas.DrawCircle(b.Right-2*d,center,2*d,paint);}
        canvas.Restore();
        // Invalidate just this native control while playing; no MAUI layout per frame.
        if(AppServices.Playback.IsUiActive&&owner.IsShown&&owner.WindowVisibility==global::Android.Views.ViewStates.Visible&&motionEnabled&&(playing||Math.Abs(amplitude-targetAmplitude)>.005f))owner.PostInvalidateOnAnimation();
    }
    private static bool PlaybackControllerIsPlaying()=>AppServices.Playback.AnimateProgress;
    protected override void Dispose(bool disposing){if(disposing){paint.Dispose();wave.Dispose();}base.Dispose(disposing);}
}
