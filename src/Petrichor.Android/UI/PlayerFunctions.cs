using Microsoft.Maui.Controls.Shapes;
namespace Petrichor.Mobile.UI;

// Supporting content uses neutral surfaces; primary color is reserved for actions/state.
internal static class PlayerFunctions
{
    public static View Tile(string title,string icon,Func<Task> action)
    {
        var glyph=Ui.Glyph(icon,24,"Primary");glyph.FontSize=24;
        var label=MooicLayout.Text(title,14);label.HorizontalTextAlignment=TextAlignment.Center;
        var content=new VerticalStackLayout{Spacing=10,Padding=new Thickness(12,16),HorizontalOptions=LayoutOptions.Fill,InputTransparent=true};content.Add(glyph);content.Add(label);
        return Surface(content,title,action,88,"SurfaceHigh",20);
    }
    public static View Row(string title,string icon,Func<Task> action,Label? supporting=null)
    {
        var content=new Grid{ColumnDefinitions=[new(24),new(GridLength.Star),new(24)],ColumnSpacing=16,Padding=new Thickness(16,10),InputTransparent=true};
        var glyph=Ui.Glyph(icon,24,"Primary");glyph.FontSize=24;content.Add(glyph);
        var words=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center};words.Add(MooicLayout.Text(title,14));
        if(supporting!=null){supporting.FontSize=12;supporting.MaxLines=1;supporting.LineBreakMode=LineBreakMode.TailTruncation;words.Add(supporting);}
        content.Add(words,1);var arrow=Ui.Glyph("chevron",20,"Muted");arrow.FontSize=20;content.Add(arrow,2);
        return Surface(content,title,action,supporting==null?56:68,"SurfaceHigh",12);
    }
    private static View Surface(View content,string label,Func<Task> action,double minimum,string color,double radius)
    {
        var hit=Ui.Button("",action,"text");hit.HeightRequest=-1;hit.MinimumHeightRequest=48;hit.CornerRadius=(int)radius;hit.HorizontalOptions=LayoutOptions.Fill;hit.VerticalOptions=LayoutOptions.Fill;SemanticProperties.SetDescription(hit,label);
        var grid=new Grid{MinimumHeightRequest=minimum};grid.Add(content);grid.Add(hit);
        var border=new Border{Content=grid,Padding=0,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=radius}};border.SetDynamicResource(VisualElement.BackgroundColorProperty,color);return border;
    }
}
