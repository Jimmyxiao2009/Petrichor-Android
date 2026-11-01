namespace Petrichor.Mobile.UI;

internal static class LoadingSkeleton
{
    private static BoxView Block(double width,double height)=>new(){WidthRequest=width,HeightRequest=height,CornerRadius=6,Color=Ui.Color("SurfaceHigh"),InputTransparent=true};
    public static View Rows()
    {
        var stack=new VerticalStackLayout{Spacing=20,Padding=Ui.Inset(24,20),InputTransparent=true};
        SemanticProperties.SetDescription(stack,"正在加载内容");
        for(int i=0;i<5;i++){var row=new HorizontalStackLayout{Spacing=14};row.Add(Block(42,42));var text=new VerticalStackLayout{Spacing=10,VerticalOptions=LayoutOptions.Center};text.Add(Block(i%2==0?174:132,12));text.Add(Block(100,9));row.Add(text);stack.Add(row);}
        return stack;
    }
    public static View Tiles()
    {
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Star),new(GridLength.Star)],ColumnSpacing=12,InputTransparent=true};
        for(int i=0;i<3;i++){var stack=new VerticalStackLayout{Spacing=10};var art=Block(-1,100);stack.Add(art);stack.Add(Block(-1,12));stack.Add(Block(54,9));grid.Add(stack,i);}
        SemanticProperties.SetDescription(grid,"正在加载推荐");return grid;
    }
}
