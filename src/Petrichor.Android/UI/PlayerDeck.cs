using Android.Provider;
using Android.Views;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using View=Microsoft.Maui.Controls.View;
namespace Petrichor.Mobile.UI;

// Android arbitrates gestures before MAUI: preserve child scrolls, cover taps
// and horizontal seeking; capture vertical paging only at a scroll boundary.
public enum PlayerPane { Player,Functions,Queue }
internal sealed class PlayerDeck : ContentView
{
    private readonly View main,functions,queue;
    private double offset,start;
    private bool crossfading;
    public PlayerPane Pane{get;private set;}
    public bool IsFunctions=>Pane==PlayerPane.Functions;
    public event Action<PlayerPane>? StateChanged;
    public PlayerDeck(View main,View functions,View queue)
    {
        this.main=main;this.functions=functions;this.queue=queue;IsClippedToBounds=true;
        var frame=new Grid{SafeAreaEdges=SafeAreaEdges.None};frame.Add(main);frame.Add(queue);frame.Add(functions);Content=frame;
        functions.IsVisible=queue.IsVisible=false;
        SizeChanged+=(_,_)=>{this.AbortAnimation("PlayerPane");Finish(Pane);};
    }
    private double Position(PlayerPane pane)=>pane==PlayerPane.Functions?-Height:pane==PlayerPane.Queue?Height:0;
    private View ViewFor(PlayerPane pane)=>pane==PlayerPane.Functions?functions:pane==PlayerPane.Queue?queue:main;
    internal void BeginDrag(){this.AbortAnimation("PlayerPane");if(crossfading)Finish(Pane);start=offset;main.IsVisible=true;functions.IsVisible=queue.IsVisible=true;}
    internal void Drag(double distance)
    {
        var value=start+distance;
        double Rubber(double extra)=>extra*.18/(1+Math.Abs(extra)/Math.Max(1,Height*.2));
        Place(value < -Height?-Height+Rubber(value+Height):value>Height?Height+Rubber(value-Height):value);
    }
    internal Task EndDrag(double velocity,bool cancelled)
    {
        var distance=offset-start;var fling=Math.Abs(velocity)>500&&Math.Abs(distance)>24;var target=Pane;
        if(!cancelled&&(fling||Math.Abs(distance)>Math.Min(72,Height*.18))){
            var down=fling?velocity>0:distance>0;
            target=down?(Pane==PlayerPane.Functions?PlayerPane.Player:PlayerPane.Queue):(Pane==PlayerPane.Queue?PlayerPane.Player:PlayerPane.Functions);
        }
        return ShowPane(target);
    }
    private void Place(double value){offset=value;main.TranslationY=value;functions.TranslationY=value+Height;queue.TranslationY=value-Height;}
    private void Finish(PlayerPane pane){crossfading=false;Place(Position(pane));main.IsVisible=true;functions.IsVisible=pane==PlayerPane.Functions;queue.IsVisible=pane==PlayerPane.Queue;functions.Opacity=queue.Opacity=1;}
    public void SnapTo(PlayerPane pane){this.AbortAnimation("PlayerPane");Pane=pane;StateChanged?.Invoke(pane);Finish(pane);}
    public Task ShowFunctions(bool show)=>ShowPane(show?PlayerPane.Functions:PlayerPane.Player);
    public Task ShowPane(PlayerPane pane)
    {
        this.AbortAnimation("PlayerPane");if(crossfading)Finish(Pane);main.IsVisible=true;var previous=Pane;var target=Position(pane);var from=offset;
        if(Pane!=pane){Pane=pane;StateChanged?.Invoke(pane);}
        var animate=Height>0&&PageMotion.Enabled;
        if(!animate||Math.Abs(target-from)<1){Finish(pane);return Task.CompletedTask;}
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if(previous!=PlayerPane.Player&&pane!=PlayerPane.Player&&previous!=pane){
            // Auxiliary pages crossfade directly, without exposing the cover between them.
            crossfading=true;main.IsVisible=false;var old=ViewFor(previous);var next=ViewFor(pane);old.IsVisible=next.IsVisible=true;old.TranslationY=next.TranslationY=0;next.Opacity=0;
            new Animation(t=>{old.Opacity=1-t;next.Opacity=t;}).Commit(this,"PlayerPane",16,200,Md.Standard,(_,cancelled)=>{if(!cancelled)Finish(pane);done.TrySetResult();});
        }else{
            functions.IsVisible=queue.IsVisible=true;
            var duration=(uint)Math.Clamp(180+Math.Abs(target-from)/Math.Max(1,Height)*80,180,260);
            new Animation(t=>Place(from+(target-from)*t)).Commit(this,"PlayerPane",16,duration,Md.Standard,(_,cancelled)=>{if(!cancelled)Finish(pane);done.TrySetResult();});
        }
        return done.Task;
    }
}
internal sealed class PlayerDeckHandler : ContentViewHandler
{
    protected override ContentViewGroup CreatePlatformView()=>new DeckNative(Context,(PlayerDeck)VirtualView){CrossPlatformLayout=VirtualView};
    private sealed class DeckNative(global::Android.Content.Context context,PlayerDeck deck):ContentViewGroup(context)
    {
        private float downX,downY;
        private bool captured,blocked;
        private global::Android.Views.View? target;
        private VelocityTracker? velocity;
        public override bool DispatchTouchEvent(MotionEvent? e)
        {
            if(e==null)return false;
            if(e.ActionMasked==MotionEventActions.Down){captured=false;downX=e.RawX;downY=e.RawY;target=Hit(this,e.RawX,e.RawY);blocked=Ancestors(target).Any(v=>v is global::Android.Widget.AbsSeekBar or global::Android.Widget.EditText);velocity?.Recycle();velocity=VelocityTracker.Obtain();}
            if(e.ActionMasked==MotionEventActions.PointerDown){
                if(captured){_=deck.EndDrag(0,true);Parent?.RequestDisallowInterceptTouchEvent(false);}
                captured=false;blocked=true;
            }
            velocity?.AddMovement(e);
            if(e.ActionMasked==MotionEventActions.Move&&!captured&&!blocked&&e.PointerCount==1){
                var dx=e.RawX-downX;var dy=e.RawY-downY;var slop=ViewConfiguration.Get(Context!)!.ScaledTouchSlop;
                var scroll=Ancestors(target).Any(v=>v!=this&&v.CanScrollVertically(dy<0?1:-1));
                var towardPage=deck.Pane==PlayerPane.Player||deck.Pane==PlayerPane.Functions&&dy>0||deck.Pane==PlayerPane.Queue&&dy<0;
                if(towardPage&&!scroll&&Math.Abs(dy)>slop&&Math.Abs(dy)>Math.Abs(dx)*1.25f){captured=true;downY=e.RawY-Math.Sign(dy)*slop;deck.BeginDrag();Parent?.RequestDisallowInterceptTouchEvent(true);base.RequestDisallowInterceptTouchEvent(false);}
            }
            var handled=base.DispatchTouchEvent(e);
            if(e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel){velocity?.Recycle();velocity=null;target=null;captured=false;}
            return handled;
        }
        public override bool OnInterceptTouchEvent(MotionEvent? e)=>captured||base.OnInterceptTouchEvent(e);
        public override bool OnTouchEvent(MotionEvent? e)
        {
            if(!captured||e==null)return base.OnTouchEvent(e);
            var density=Resources!.DisplayMetrics!.Density;
            if(e.ActionMasked==MotionEventActions.Move)deck.Drag((e.RawY-downY)/density);
            if(e.ActionMasked is MotionEventActions.Up or MotionEventActions.Cancel){velocity?.ComputeCurrentVelocity(1000);_=deck.EndDrag((velocity?.YVelocity??0)/density,e.ActionMasked==MotionEventActions.Cancel);Parent?.RequestDisallowInterceptTouchEvent(false);}
            return true;
        }
        protected override void OnDetachedFromWindow(){velocity?.Recycle();velocity=null;target=null;captured=false;base.OnDetachedFromWindow();}
        private IEnumerable<global::Android.Views.View> Ancestors(global::Android.Views.View? view){while(view!=null){yield return view;if(view==this)break;view=view.Parent as global::Android.Views.View;}}
        private static global::Android.Views.View Hit(global::Android.Views.View view,float x,float y)
        {
            if(view is ViewGroup group)for(int i=group.ChildCount-1;i>=0;i--){var child=group.GetChildAt(i)!;if(child.Visibility!=ViewStates.Visible)continue;var point=new int[2];child.GetLocationOnScreen(point);if(x>=point[0]&&x<point[0]+child.Width&&y>=point[1]&&y<point[1]+child.Height)return Hit(child,x,y);}
            return view;
        }
    }
}
