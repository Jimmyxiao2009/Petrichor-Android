using Microsoft.Maui.Controls.Shapes;
namespace Petrichor.Mobile.UI;

public enum IconStyle{Standard,Filled,Tonal,Outlined}

// Material 3 icon button: a 40dp container inside a 48dp touch target, with a circular state layer.
// Toggle buttons switch between the unselected and selected color roles of their style.
public sealed class IconButton:Grid
{
    private readonly Border container;
    private readonly Label glyph;
    private readonly double containerSize;
    private string icon;
    private bool filled,selected;
    private IconStyle style;
    private string? background,foreground;
    public Border Container=>container;
    public Label GlyphLabel=>glyph;
    public IconButton(string icon,Func<Task> action,string description,IconStyle style=IconStyle.Standard,double iconSize=24,double containerSize=40,double target=48,bool filled=false,string? foreground=null)
    {
        this.icon=icon;this.style=style;this.filled=filled;this.foreground=foreground;this.containerSize=containerSize;
        var size=Math.Max(target,containerSize);WidthRequest=size;HeightRequest=size;HorizontalOptions=LayoutOptions.Center;VerticalOptions=LayoutOptions.Center;
        container=new Border{WidthRequest=containerSize,HeightRequest=containerSize,Padding=0,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=containerSize/2},HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center,InputTransparent=true};
        glyph=Ui.Glyph(icon,iconSize,"OnSurfaceVariant",filled);container.Content=glyph;Add(container);
        Pressable.Attach(this,action,null,0,description,(size-containerSize)/2,true,style==IconStyle.Filled?"OnPrimary":"OnSurface");
        PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(IsEnabled))Opacity=IsEnabled?1:Md.DisabledContent;};
        Apply();
    }
    public string Icon{get=>icon;set=>SetIcon(value,filled);}
    public void SetIcon(string name,bool filledGlyph,bool animate=false)
    {
        if(icon==name&&filled==filledGlyph)return;
        icon=name;filled=filledGlyph;
        void Swap(){glyph.Text=Ui.Glyphs.GetValueOrDefault(name,Ui.Glyphs["headphones"]);glyph.FontFamily=filledGlyph?"IconsFilled":"Icons";}
        if(!animate||!PageMotion.Enabled){Swap();return;}
        // Icon swaps (play/pause, like) shrink out and grow back in rather than jumping.
        glyph.AbortAnimation("IconSwap");
        new Animation(t=>{if(t<.5){glyph.Scale=1-.6*(t/.5);glyph.Opacity=1-t/.5;}else{if(glyph.Text!=Ui.Glyphs.GetValueOrDefault(name))Swap();var u=(t-.5)/.5;glyph.Scale=.4+.6*u;glyph.Opacity=u;}})
            .Commit(glyph,"IconSwap",16,Md.Short4,Easing.Linear,(_,_)=>{Swap();glyph.Scale=1;glyph.Opacity=1;});
    }
    private bool toggle;
    public bool Toggle{get=>toggle;set{toggle=value;Apply();}}
    public bool Selected{get=>selected;set{if(selected==value)return;selected=value;Apply();}}
    public IconStyle Variant{get=>style;set{if(style==value)return;style=value;Apply();}}
    public double IconSize{get=>glyph.FontSize;set=>glyph.FontSize=value;}
    public void SetColors(string? background,string? foreground){this.background=background;this.foreground=foreground;Apply();}
    public void SetCorner(double radius)=>((RoundRectangle)container.StrokeShape!).CornerRadius=radius;
    // Animates the container corner, e.g. the play button morphing from a circle to a rounded square.
    public void MorphCorner(double radius)
    {
        var shape=(RoundRectangle)container.StrokeShape!;var from=shape.CornerRadius.TopLeft;
        if(Math.Abs(from-radius)<.5)return;
        container.AbortAnimation("Morph");
        if(!PageMotion.Enabled){shape.CornerRadius=radius;return;}
        new Animation(t=>shape.CornerRadius=from+(radius-from)*t).Commit(container,"Morph",16,Md.Medium2,Md.EmphasizedDecelerate);
    }
    // Brief scale feedback for state toggles (like, shuffle, repeat).
    public Task Pop()
    {
        if(!PageMotion.Enabled)return Task.CompletedTask;
        var done=new TaskCompletionSource();container.AbortAnimation("Pop");
        new Animation(t=>container.Scale=t<.35?1-.18*(t/.35):.82+.18*Md.EmphasizedDecelerate.Ease((t-.35)/.65)).Commit(container,"Pop",16,Md.Medium1,Easing.Linear,(_,_)=>{container.Scale=1;done.TrySetResult();});
        return done.Task;
    }
    private void Apply()
    {
        string bg,fg;bool outline=false;
        switch(style)
        {
            case IconStyle.Filled:bg="Primary";fg="OnPrimary";break;
            case IconStyle.Tonal when Toggle&&!selected:bg="SurfaceContainerHighest";fg="OnSurfaceVariant";break;
            case IconStyle.Tonal:bg="SecondaryContainer";fg="OnSecondaryContainer";break;
            case IconStyle.Outlined:bg=selected?"InverseSurface":"Transparent";fg=selected?"InverseOnSurface":"OnSurfaceVariant";outline=!selected;break;
            default:bg="Transparent";fg=selected?"Primary":"OnSurfaceVariant";break;
        }
        if(background!=null)bg=background;
        if(foreground!=null&&!selected)fg=foreground;
        container.SetDynamicResource(BackgroundColorProperty,bg);glyph.SetDynamicResource(Label.TextColorProperty,fg);
        container.StrokeThickness=outline?1:0;if(outline)container.SetDynamicResource(Border.StrokeProperty,"Outline");
    }
}
