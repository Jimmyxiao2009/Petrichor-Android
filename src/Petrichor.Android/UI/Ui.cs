using Petrichor.Core;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
namespace Petrichor.Mobile.UI;

public static class Ui
{
    public static double S(double value)=>value*.78;
    public static double F(double value)=>Math.Max(10.5,value*.85);
    public static Thickness Inset(double all)=>new(S(all));
    public static Thickness Inset(double horizontal,double vertical)=>new(S(horizontal),S(vertical));
    public static Thickness Inset(double left,double top,double right,double bottom)=>new(S(left),S(top),S(right),S(bottom));
    public static double MediaRowHeight=>Math.Max(64,24+40*(global::Android.App.Application.Context.Resources?.Configuration?.FontScale??1));
    public static string ErrorText(Exception error)
    {
        var message=error.Message;
        if(error is ArgumentException argument&&!string.IsNullOrEmpty(argument.ParamName))
        {
            // The runtime appends a localized parameter suffix; trimmed Android
            // builds can expose its resource key instead. Remove the runtime's
            // exact suffix without depending on its current language.
            var suffix=new ArgumentException("",argument.ParamName).Message;
            if(suffix.Length>0&&message.EndsWith(suffix,StringComparison.Ordinal))message=message[..^suffix.Length].TrimEnd();
        }
        return message;
    }
    public static readonly Dictionary<string,string> Glyphs = new() {
        ["home"]="\ue88a",["up"]="\ue316",["down"]="\ue313",["play"]="\ue037",["pause"]="\ue034",["next"]="\ue044",["previous"]="\ue045",["more"]="\ue5d4",
        ["back"]="\ue5c4",["search"]="\ue8b6",["explore"]="\ue87a",["library"]="\ue030",["podcasts"]="\uf048",
        ["person"]="\ue7fd",["settings"]="\ue8b8",["add"]="\ue145",["check"]="\ue5ca",["close"]="\ue5cd",["visible"]="\ue8f4",["hidden"]="\ue8f5",
        ["like"]="\ue87e",["liked"]="\ue87d",["download"]="\uf090",["history"]="\ue889",["today"]="\ue916",
        ["radio"]="\ue03e",["chart"]="\ue26b",["queue"]="\ue03d",["shuffle"]="\ue043",["repeat"]="\ue040",
        ["repeatone"]="\ue041",["lyrics"]="\uec0b",["comments"]="\ue0b9",["album"]="\ue019",["sleep"]="\uef44",
        ["folder"]="\ue2c7",["cloud"]="\ue2c1",["delete"]="\ue872",["logout"]="\ue9ba",["info"]="\ue88e",
        ["chevron"]="\ue5cc",["headphones"]="\ue310",["lock"]="\ue897",["edit"]="\ue3c9",["reply"]="\ue15e",
        ["thumb"]="\ue8dc",["wifi"]="\ue63e",["dark"]="\ue51c",["playlist"]="\ue03b",["share"]="\ue80d",["login"]="\uea77",["equalizer"]="\ue1b8"
    };
    public static Color Color(string key) => (Color)Application.Current!.Resources[key];
    public static Label Text(string text="",double size=14,string color="OnSurface")
    {
        var label = new Label {Text=text,FontSize=F(size),LineBreakMode=LineBreakMode.WordWrap,VerticalOptions=LayoutOptions.Center};
        label.SetDynamicResource(Label.TextColorProperty,color); return label;
    }
    public static Label Glyph(string name,double size=24,string color="OnSurface")
    {
        var label = Text(Glyphs.GetValueOrDefault(name,Glyphs["headphones"]),size,color);
        label.FontSize=S(size);label.FontFamily="Icons"; label.VerticalTextAlignment=TextAlignment.Center; label.HorizontalTextAlignment=TextAlignment.Center;
        return label;
    }
    public static FontImageSource IconSource(string name) => new() {FontFamily=name=="home"?"IconsFilled":"Icons",Glyph=Glyphs.GetValueOrDefault(name,Glyphs["headphones"]),Size=S(24)};
    public static Button Button(string text,Func<Task> action,string kind="filled",string? icon=null)
    {
        Button button=icon==null?new Button():new IconTextButton();
        button.Text=text;button.HeightRequest=Ui.S(48);button.MinimumHeightRequest=Ui.S(48);button.Padding=Ui.Inset(20,0);button.CornerRadius=(int)S(24);button.FontSize=Ui.F(14);
        if(icon!=null)
        {
            var source=IconSource(icon);source.SetBinding(FontImageSource.ColorProperty,new Binding(nameof(button.TextColor),source:button));button.ImageSource=source;
            button.ContentLayout=new Microsoft.Maui.Controls.Button.ButtonContentLayout(Microsoft.Maui.Controls.Button.ButtonContentLayout.ImagePosition.Left,S(8));
            // WidthRequest reaches Android's exact measure spec. A minimum
            // width only enlarges MAUI's frame and can leave native text clipped.
            void MeasureContent()
            {
                var metrics=global::Android.App.Application.Context.Resources!.DisplayMetrics!;
                using var paint=new global::Android.Graphics.Paint();
                paint.TextSize=global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Sp,(float)button.FontSize,metrics);
                button.WidthRequest=Math.Ceiling(paint.MeasureText(button.Text??"")/metrics.Density+button.Padding.HorizontalThickness+S(40));
            }
            MeasureContent();button.PropertyChanged+=(_,e)=>{if(e.PropertyName is nameof(button.Text) or nameof(button.FontSize) or nameof(button.Padding))MeasureContent();};
        }
        if(kind is "text" or "outlined")button.SetDynamicResource(Microsoft.Maui.Controls.Button.BackgroundColorProperty,"Transparent");
        else button.SetDynamicResource(Microsoft.Maui.Controls.Button.BackgroundColorProperty,kind=="filled"?"Primary":"PrimaryContainer");
        button.SetDynamicResource(Microsoft.Maui.Controls.Button.TextColorProperty,kind=="filled"?"OnPrimary":kind=="tonal"?"OnPrimaryContainer":"Primary");
        if(kind=="outlined"){button.BorderWidth=1;button.SetDynamicResource(Microsoft.Maui.Controls.Button.BorderColorProperty,"Outline");}
        button.HandlerChanged+=(_,_)=>{if(button.Handler?.PlatformView is Google.Android.Material.Button.MaterialButton native){native.InsetTop=0;native.InsetBottom=0;native.SetMinimumHeight(0);}};
        button.Clicked+=async(_,_)=>{if(!button.IsEnabled)return;button.IsEnabled=false;try{await action();}finally{button.IsEnabled=true;}};
        return button;
    }
    public static Button Icon(string name,Func<Task> action,string description)
    {
        var b=Button(Glyphs.GetValueOrDefault(name,Glyphs["more"]),action,"text");
        b.FontFamily="Icons";b.FontSize=Ui.F(24);b.WidthRequest=Ui.S(48);b.Padding=0;
        b.SetDynamicResource(Microsoft.Maui.Controls.Button.TextColorProperty,"OnSurface"); SemanticProperties.SetDescription(b,description);return b;
    }
    public static Border Card(View child,string background="SurfaceContainer",double radius=20,Thickness? padding=null)
    {
        var border=new Border {Content=child,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=S(radius)},Padding=padding??Ui.Inset(16)};
        border.SetDynamicResource(VisualElement.BackgroundColorProperty,background);return border;
    }
    public static BoxView Divider(){var b=new BoxView{HeightRequest=Ui.S(1),Opacity=.6};b.SetDynamicResource(BoxView.ColorProperty,"Outline");return b;}
    public static View Search(SearchField search)=>search;
    public static ImageSource Art(string? url,int size=400)
    {
        if(string.IsNullOrWhiteSpace(url))return "forest.png";
        if(Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https")
        {
            if(uri.Host.EndsWith("music.126.net",StringComparison.OrdinalIgnoreCase)&&!uri.Query.Contains("param="))url+=(url.Contains('?')?"&":"?")+"param="+size+"y"+size;
            return new UriImageSource{Uri=new Uri(url),CachingEnabled=true,CacheValidity=TimeSpan.FromDays(14)};
        }
        return ImageSource.FromFile(url);
    }
    public static Image Cover(string? url,double size=52)
    {
        size=S(size);var image=new Image{Aspect=Aspect.AspectFill,WidthRequest=size,HeightRequest=size,Source=Art(url,(int)Math.Max(200,size*2))};
        image.Clip=new RoundRectangleGeometry(new CornerRadius(S(10)),new Rect(0,0,size,size));return image;
    }
    public static Grid Row(string title,string subtitle,Func<Task> action,string icon="chevron",string? cover=null)
    {
        var grid=new Grid {ColumnDefinitions=[new(Ui.S(56)),new(GridLength.Star),new(Ui.S(48))],ColumnSpacing=Ui.S(12),Padding=Ui.Inset(0,10),MinimumHeightRequest=Ui.S(76)};
        var leading=cover!=null?(View)Cover(cover):Glyph(icon,26,"Primary");grid.Add(leading);
        var info=new VerticalStackLayout {Spacing=Ui.S(4),VerticalOptions=LayoutOptions.Center};
        info.Add(Text(title,15));info.Add(Text(subtitle,12,"Muted"));grid.Add(info,1);
        grid.Add(Icon("chevron",action,"打开"+title),2);
        var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await action();info.GestureRecognizers.Add(tap);leading.GestureRecognizers.Add(tap);return grid;
    }
    public static View Heading(string title,string subtitle="")
    {
        var stack=new VerticalStackLayout{Spacing=Ui.S(8),Margin=Ui.Inset(0,4,0,24)};
        var heading=Text(title,34);heading.CharacterSpacing=-1;stack.Add(heading);if(subtitle.Length>0)stack.Add(Text(subtitle,14,"Muted"));return stack;
    }
    public static View Section(string title,Func<Task>? action=null,string actionText="查看全部")
    {
        var grid=new Grid {ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],MinimumHeightRequest=Ui.S(48),Margin=Ui.Inset(0,26,0,14)};
        grid.Add(Text(title,22));
        if(action!=null){var more=Button(actionText+"  ›",action,"text");more.FontSize=Ui.F(13);more.Padding=0;grid.Add(more,1);}
        return grid;
    }
    public static View Empty(string title,string description,Func<Task>? retry=null)
    {
        var stack=new VerticalStackLayout {Spacing=Ui.S(16),Padding=Ui.Inset(24,48),HorizontalOptions=LayoutOptions.Fill};
        var bubble=Card(Glyph("library",36,"Primary"),"PrimaryContainer",28,0);bubble.WidthRequest=Ui.S(88);bubble.HeightRequest=Ui.S(88);bubble.HorizontalOptions=LayoutOptions.Center;stack.Add(bubble);var h=Text(title,22);h.HorizontalTextAlignment=TextAlignment.Center;stack.Add(h);
        var p=Text(description,14,"Muted");p.HorizontalTextAlignment=TextAlignment.Center;stack.Add(p);
        if(retry!=null)stack.Add(Button("重试",retry));return stack;
    }
    private static readonly SemaphoreSlim navigation=new(1,1);
    public static async Task Push(Page page){if(!await navigation.WaitAsync(0))return;try{await PageMotion.PushAsync(page);}finally{navigation.Release();}}
    public static async Task Pop(){if(!await navigation.WaitAsync(0))return;try{if(Shell.Current.Navigation.NavigationStack.Count>1)await PageMotion.PopAsync();}finally{navigation.Release();}}
    public static async Task Tab(string route){if(route=="search"){await Push(new SearchPage());return;}if(!await navigation.WaitAsync(0))return;try{await Shell.Current.GoToAsync("//main/"+route+"Tab/"+route);}finally{navigation.Release();}}
    public static string Duration(int milliseconds)=>TimeSpan.FromMilliseconds(milliseconds).ToString(milliseconds>=3600000?@"h\:mm\:ss":@"m\:ss");
}
