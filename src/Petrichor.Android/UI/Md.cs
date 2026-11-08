using Android.Graphics;
using Microsoft.Maui.Handlers;
using AInterpolator=Android.Views.Animations.PathInterpolator;
namespace Petrichor.Mobile.UI;

public enum TypeRole{DisplaySmall,HeadlineLarge,HeadlineMedium,HeadlineSmall,TitleLarge,TitleMedium,TitleSmall,BodyLarge,BodyMedium,BodySmall,LabelLarge,LabelMedium,LabelSmall}

// Material 3 design tokens. All sizes are dp; text sizes are sp and follow the system font scale.
public static class Md
{
    public const double Margin=16;
    public const double ShapeExtraSmall=4,ShapeSmall=8,ShapeMedium=12,ShapeLarge=16,ShapeLargeIncreased=20,ShapeExtraLarge=28;
    public const uint Short2=100,Short3=150,Short4=200,Medium1=250,Medium2=300,Medium3=350,Medium4=400,Long2=500;
    public const float PressedState=.12f,DisabledContent=.38f,DisabledContainer=.12f;

    public static readonly Easing Standard=Bezier(.2,0,0,1);
    public static readonly Easing StandardDecelerate=Bezier(0,0,0,1);
    public static readonly Easing StandardAccelerate=Bezier(.3,0,1,1);
    public static readonly Easing EmphasizedDecelerate=Bezier(.05,.7,.1,1);
    public static readonly Easing EmphasizedAccelerate=Bezier(.3,0,.8,.15);
    public static AInterpolator NativeStandard()=>new(.2f,0,0,1);
    public static AInterpolator NativeEmphasizedDecelerate()=>new(.05f,.7f,.1f,1);
    public static AInterpolator NativeEmphasizedAccelerate()=>new(.3f,0,.8f,.15f);

    // Solves the cubic Bézier for x, then evaluates y, matching CSS/Android PathInterpolator curves.
    public static Easing Bezier(double x1,double y1,double x2,double y2)=>new(t=>{
        if(t<=0)return 0;if(t>=1)return 1;
        double X(double u)=>3*(1-u)*(1-u)*u*x1+3*(1-u)*u*u*x2+u*u*u;
        double low=0,high=1;for(var i=0;i<18;i++){var u=(low+high)/2;if(X(u)<t)low=u;else high=u;}
        var v=(low+high)/2;return 3*(1-v)*(1-v)*v*y1+3*(1-v)*v*v*y2+v*v*v;
    });

    private static readonly (double Size,bool Medium)[] scale=[(36,false),(32,false),(28,false),(24,false),(22,false),(16,true),(14,true),(16,false),(14,false),(12,false),(14,true),(12,true),(11,true)];
    public static double Size(TypeRole role)=>scale[(int)role].Size;

    public static readonly BindableProperty MediumProperty=BindableProperty.CreateAttached("Medium",typeof(bool),typeof(Md),false,propertyChanged:(b,_,_)=>(b as IView)?.Handler?.UpdateValue(WeightKey));
    public static bool GetMedium(BindableObject view)=>(bool)view.GetValue(MediumProperty);
    public static void SetMedium(BindableObject view,bool value)=>view.SetValue(MediumProperty,value);

    public static Label Text(string text,TypeRole role,string color="OnSurface")
    {
        var label=new Label{Text=text,LineBreakMode=LineBreakMode.WordWrap,VerticalOptions=LayoutOptions.Center};
        Style(label,role);label.SetDynamicResource(Label.TextColorProperty,color);return label;
    }
    public static T Style<T>(T label,TypeRole role) where T:Label
    {
        label.FontSize=Size(role);label.FontAttributes=FontAttributes.None;SetMedium(label,scale[(int)role].Medium);return label;
    }

    private const string WeightKey="MdWeight";
    private static Typeface? medium;
    // Roboto Medium (500). MAUI only maps FontAttributes.Bold, so the weight is applied after its font mapping.
    private static Typeface MediumFace=>medium??=OperatingSystem.IsAndroidVersionAtLeast(28)?Typeface.Create(Typeface.Default,500,false)!:Typeface.Create("sans-serif-medium",TypefaceStyle.Normal)!;
    internal static void RegisterMappers()
    {
        static void Label(ILabelHandler handler,ILabel view){if(view is BindableObject b&&GetMedium(b))handler.PlatformView.Typeface=MediumFace;}
        static void Button(IButtonHandler handler,IButton view){if(view is BindableObject b&&GetMedium(b)&&handler.PlatformView is Android.Widget.TextView text)text.Typeface=MediumFace;}
        LabelHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font),Label);
        LabelHandler.Mapper.AppendToMapping(WeightKey,Label);
        ButtonHandler.Mapper.AppendToMapping(nameof(ITextStyle.Font),Button);
        ButtonHandler.Mapper.AppendToMapping(WeightKey,Button);
        ButtonHandler.Mapper.AppendToMapping(nameof(IView.Background),Inset);
        ButtonHandler.Mapper.AppendToMapping(TouchInsetKey,Inset);
    }
    // Buttons keep a 48dp touch target around a 40dp container. MAUI paints buttons with its own
    // ripple drawable (container layer + mask), so the inset is applied to those layers.
    private const string TouchInsetKey="MdTouchInset";
    public static readonly BindableProperty TouchInsetProperty=BindableProperty.CreateAttached("TouchInset",typeof(double),typeof(Md),0d,propertyChanged:(b,_,_)=>(b as IView)?.Handler?.UpdateValue(TouchInsetKey));
    public static void SetTouchInset(BindableObject view,double value)=>view.SetValue(TouchInsetProperty,value);
    private static void Inset(IButtonHandler handler,IButton view)
    {
        if(view is not BindableObject b)return;
        var inset=(double)b.GetValue(TouchInsetProperty);if(inset<=0)return;
        if(handler.PlatformView.Background is not Android.Graphics.Drawables.RippleDrawable ripple||ripple.NumberOfLayers<2)return;
        var px=(int)Math.Round(inset*handler.PlatformView.Resources!.DisplayMetrics!.Density);
        for(var i=0;i<ripple.NumberOfLayers;i++)ripple.SetLayerInset(i,0,px,0,px);
    }
}
