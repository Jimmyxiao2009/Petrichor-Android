using Google.Android.Material.Button;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Petrichor.Mobile.UI;

public sealed class IconTextButton : Button {}

public sealed class IconButtonHandler : ButtonHandler
{
    protected override MaterialButton CreatePlatformView()=>new IconButton(Context)
    {
        IconGravity=MaterialButton.IconGravityTextStart,
        IconTintMode=Android.Graphics.PorterDuff.Mode.Add,
        IconTint=Colors.Transparent.ToDefaultColorStateList(),SoundEffectsEnabled=false
    };
    private sealed class IconButton(Android.Content.Context context):MauiMaterialButton(context)
    {
        protected override void OnDraw(Android.Graphics.Canvas canvas)
        {
            // A late compound icon can leave TextView's layout at the previous
            // text-only width when its MAUI parent retains the same frame.
            var available=Width-CompoundPaddingLeft-CompoundPaddingRight;
            if(Icon!=null&&Layout!=null&&available>0&&Math.Abs(Layout.Width-available)>1)
            {
                ForceLayout();
                Measure(MeasureSpec.MakeMeasureSpec(Width,Android.Views.MeasureSpecMode.Exactly),
                    MeasureSpec.MakeMeasureSpec(Height,Android.Views.MeasureSpecMode.Exactly));
            }
            base.OnDraw(canvas);
        }
    }
}
