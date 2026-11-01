using Android.Views;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

internal sealed class MusicSlider : Slider
{
    public bool GestureCancelled {get;internal set;}
    public bool WaitingForSeek {get;set;}
    public bool TouchActive {get;internal set;}
}

internal sealed class MusicSliderHandler : SliderHandler
{
    protected override SeekBar CreatePlatformView()=>new MusicSeekBar(Context,(MusicSlider)VirtualView){DuplicateParentStateEnabled=false,Max=(int)SliderExtensions.PlatformMaxValue};
    private sealed class MusicSeekBar(global::Android.Content.Context context,MusicSlider slider):SeekBar(context)
    {
        private float downX,downY;
        private bool horizontal,consume;
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if(e==null)return false;
            if(!Enabled)return base.OnTouchEvent(e);
            if(e.ActionMasked==MotionEventActions.Down){downX=e.GetX();downY=e.GetY();horizontal=false;consume=false;slider.GestureCancelled=false;slider.TouchActive=true;Parent?.RequestDisallowInterceptTouchEvent(true);}
            if(consume){if(e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel){slider.TouchActive=false;Parent?.RequestDisallowInterceptTouchEvent(false);}return true;}
            var dx=e.GetX()-downX;var dy=e.GetY()-downY;var slop=ViewConfiguration.Get(Context!)!.ScaledTouchSlop;
            if(e.ActionMasked==MotionEventActions.PointerDown||e.ActionMasked==MotionEventActions.Move&&!horizontal&&Math.Abs(dy)>slop&&Math.Abs(dy)>Math.Abs(dx)*1.25f){
                slider.GestureCancelled=true;consume=true;using var cancel=MotionEvent.Obtain(e)!;cancel.Action=MotionEventActions.Cancel;base.OnTouchEvent(cancel);Pressed=false;Invalidate();return true;
            }
            if(e.ActionMasked==MotionEventActions.Move&&Math.Abs(dx)>slop)horizontal=true;
            if(e.ActionMasked==MotionEventActions.Cancel)slider.GestureCancelled=true;
            var handled=base.OnTouchEvent(e);
            if(e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel){slider.TouchActive=false;Parent?.RequestDisallowInterceptTouchEvent(false);}
            return handled;
        }
        protected override void OnDraw(global::Android.Graphics.Canvas canvas)
        {
            // Keep playback movement native; do not measure MAUI layouts every frame.
            var playback=AppServices.Playback;
            if(!slider.TouchActive&&!slider.WaitingForSeek&&playback.AnimateProgress&&playback.DisplayDurationMs>0)
                Progress=(int)(Math.Clamp(playback.DisplayPositionMs/playback.DisplayDurationMs,0,1)*Max);
            base.OnDraw(canvas);
        }
        public override bool OnKeyDown(global::Android.Views.Keycode keyCode,KeyEvent? e)
        {
            var duration=AppServices.Playback.Duration.TotalMilliseconds;
            if(duration>0)KeyProgressIncrement=Math.Max(1,(int)(Max*1000d/duration));
            return base.OnKeyDown(keyCode,e);
        }
        protected override void OnDetachedFromWindow(){slider.GestureCancelled=true;slider.TouchActive=false;Parent?.RequestDisallowInterceptTouchEvent(false);base.OnDetachedFromWindow();}
    }
}
