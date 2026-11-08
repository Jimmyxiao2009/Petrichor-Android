namespace Petrichor.Mobile.UI;

// Supporting content uses neutral surfaces; primary color is reserved for actions/state.
internal static class PlayerFunctions
{
    public static View Tile(string title,string icon,Func<Task> action,string background="SurfaceContainerHigh")
    {
        var label=Ui.Line(title,TypeRole.LabelLarge);label.HorizontalTextAlignment=TextAlignment.Center;
        var content=new VerticalStackLayout{Spacing=8,Padding=new Thickness(12,16),HorizontalOptions=LayoutOptions.Fill,VerticalOptions=LayoutOptions.Center,InputTransparent=true};content.Add(Ui.Glyph(icon,24,"Primary"));content.Add(label);
        var card=Ui.Card(content,background,Md.ShapeLarge,0);card.MinimumHeightRequest=88;
        return Pressable.Attach(card,action,radius:Md.ShapeLarge,description:title);
    }
    public static Grid Row(string title,string icon,Func<Task> action,Label? supporting=null)
    {
        var row=Ui.ListItem(title,null,action,icon);
        if(supporting!=null)
        {
            Md.Style(supporting,TypeRole.BodyMedium);supporting.SetDynamicResource(Label.TextColorProperty,"OnSurfaceVariant");supporting.MaxLines=1;supporting.LineBreakMode=LineBreakMode.TailTruncation;
            if(row.Children[1] is VerticalStackLayout words)words.Add(supporting);
            row.MinimumHeightRequest=72;
        }
        return row;
    }
}
