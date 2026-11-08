using Android.Animation;
using Microsoft.Maui.Controls.Shapes;
using AView=Android.Views.View;
namespace Petrichor.Mobile.UI;

// Placeholder layout that mirrors the real list rows and pulses gently while content loads.
internal static class LoadingSkeleton
{
    private static Border Block(double width,double height,double radius=Md.ShapeExtraSmall)
    {
        var block=new Border{WidthRequest=width,HeightRequest=height,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=radius},InputTransparent=true};
        block.SetDynamicResource(VisualElement.BackgroundColorProperty,"SurfaceContainerHighest");return block;
    }
    public static View Rows(double inset=0)
    {
        var stack=new VerticalStackLayout{Spacing=16,Padding=new Thickness(inset,16),InputTransparent=true};
        SemanticProperties.SetDescription(stack,"正在加载内容");
        for(int i=0;i<6;i++){var row=new HorizontalStackLayout{Spacing=16};row.Add(Block(56,56,Md.ShapeSmall));var text=new VerticalStackLayout{Spacing=10,VerticalOptions=LayoutOptions.Center};text.Add(Block(i%2==0?180:136,14));text.Add(Block(96,10));row.Add(text);stack.Add(row);}
        return Pulse(stack);
    }
    public static View Tiles()
    {
        var row=new HorizontalStackLayout{Spacing=8,Padding=new Thickness(Md.Margin,0),InputTransparent=true};
        for(int i=0;i<3;i++){var stack=new VerticalStackLayout{Spacing=8};stack.Add(Block(136,136,Md.ShapeLarge));stack.Add(Block(120,14));stack.Add(Block(64,10));row.Add(stack);}
        SemanticProperties.SetDescription(row,"正在加载推荐");return Pulse(row);
    }
    public static View Grid2x2()
    {
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star)],RowDefinitions=[new(88),new(88)],ColumnSpacing=8,RowSpacing=8,Padding=new Thickness(Md.Margin,0),InputTransparent=true};
        for(int i=0;i<4;i++){var block=Block(-1,88,Md.ShapeLarge);grid.Add(block,i%2,i/2);}
        return Pulse(grid);
    }
    // Native alpha animation; it stops when the placeholder leaves the window.
    private static T Pulse<T>(T view) where T:View
    {
        ObjectAnimator? animator=null;
        void Start(AView native)
        {
            if(!PageMotion.Enabled)return;
            animator?.Cancel();animator=ObjectAnimator.OfFloat(native,"alpha",1f,.45f)!;
            animator.SetDuration(900);animator.RepeatMode=ValueAnimatorRepeatMode.Reverse;animator.RepeatCount=ValueAnimator.Infinite;animator.SetInterpolator(Md.NativeStandard());animator.Start();
        }
        view.HandlerChanged+=(_,_)=>{
            if(view.Handler?.PlatformView is not AView native)return;
            native.ViewAttachedToWindow+=(_,_)=>Start(native);
            native.ViewDetachedFromWindow+=(_,_)=>{animator?.Cancel();animator=null;native.Alpha=1;};
            if(native.IsAttachedToWindow)Start(native);
        };
        return view;
    }
}
