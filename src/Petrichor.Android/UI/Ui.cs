using Petrichor.Core;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;
using AView=Android.Views.View;
namespace Petrichor.Mobile.UI;

// The rounded placeholder and the artwork are one shape: a framework
// GradientDrawable paints the tonal fill, and ClipToOutline clips the image
// to that same background outline. Both corners and mask come from platform
// classes, so no square grey layer can sit behind the corners and no managed
// Java callback is involved while the trimmed Release build draws frames.
internal static class RoundedCover
{
    public static void Attach(View view,double radiusDp)
    {
        view.HandlerChanged+=(_,_)=>Apply(view,radiusDp);
    }
    private static void Apply(View view,double radiusDp)
    {
        if(view.Handler?.PlatformView is not AView native)return;
        try
        {
            var shape=new Android.Graphics.Drawables.GradientDrawable();
            shape.SetShape(Android.Graphics.Drawables.ShapeType.Rectangle);
            shape.SetCornerRadius((float)(radiusDp*native.Resources!.DisplayMetrics!.Density));
            shape.SetColor(Microsoft.Maui.Platform.ColorExtensions.ToPlatform(Ui.Color("SurfaceContainerHighest")).ToArgb());
            native.Background=shape;
            native.ClipToOutline=true;
        }
        catch(Exception){ }
    }
}

public static class Ui
{
    public static double MediaRowHeight=>Math.Max(72,32+40*(global::Android.App.Application.Context.Resources?.Configuration?.FontScale??1));
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
        ["thumb"]="\ue8dc",["wifi"]="\ue63e",["dark"]="\ue51c",["playlist"]="\ue03b",["share"]="\ue80d",["login"]="\uea77",["equalizer"]="\ue1b8",
        ["account"]="\ue853",["palette"]="\ue40a",["storage"]="\ue1db",["searchoff"]="\uea76",["offline"]="\ue648",["send"]="\ue163",
        ["forward"]="\ue5c8",["sort"]="\ue164",["music"]="\ue405",["error"]="\ue001",["select"]="\ue162",["quality"]="\ue024",
        ["subscriptions"]="\ue064",["notes"]="\ue26c",["playlistplay"]="\ue05f",["clear"]="\ue0b8"
    };
    public static Color Color(string key) => (Color)Application.Current!.Resources[key];
    public static Label Text(string text="",TypeRole role=TypeRole.BodyMedium,string color="OnSurface")=>Md.Text(text,role,color);
    public static Label Line(string text,TypeRole role,string color="OnSurface")
    {
        var label=Md.Text(text,role,color);label.MaxLines=1;label.LineBreakMode=LineBreakMode.TailTruncation;return label;
    }
    public static Label Glyph(string name,double size=24,string color="OnSurface",bool filled=false)
    {
        // Icons keep their dp size; they must not grow with the user's text scale.
        var label=new Label{Text=Glyphs.GetValueOrDefault(name,Glyphs["headphones"]),FontFamily=filled?"IconsFilled":"Icons",FontSize=size,FontAutoScalingEnabled=false,
            HorizontalTextAlignment=TextAlignment.Center,VerticalTextAlignment=TextAlignment.Center,HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center,InputTransparent=true};
        label.SetDynamicResource(Label.TextColorProperty,color);return label;
    }
    public static FontImageSource IconSource(string name,double size=18,bool filled=false)=>new(){FontFamily=filled?"IconsFilled":"Icons",Glyph=Glyphs.GetValueOrDefault(name,Glyphs["headphones"]),Size=size};

    // Material 3 common buttons: 40dp tall inside a 48dp touch target, full corner, Label Large text.
    public static Button Button(string text,Func<Task> action,string kind="filled",string? icon=null)
    {
        Button button=icon==null?new Button():new IconTextButton();
        button.Text=text;button.HeightRequest=48;button.MinimumHeightRequest=48;button.CornerRadius=20;button.FontSize=14;Md.SetMedium(button,true);
        button.Padding=kind=="text"?new Thickness(12,0):new Thickness(icon==null?24:16,0,24,0);
        var (background,foreground)=kind switch{"filled"=>("Primary","OnPrimary"),"tonal"=>("SecondaryContainer","OnSecondaryContainer"),"elevated"=>("SurfaceContainerLow","Primary"),"error"=>("Error","OnError"),_=>("Transparent","Primary")};
        button.SetDynamicResource(Microsoft.Maui.Controls.Button.BackgroundColorProperty,background);
        button.SetDynamicResource(Microsoft.Maui.Controls.Button.TextColorProperty,foreground);
        if(kind=="outlined"){button.BorderWidth=1;button.SetDynamicResource(Microsoft.Maui.Controls.Button.BorderColorProperty,"Outline");}
        if(icon!=null)
        {
            var source=IconSource(icon);source.SetBinding(FontImageSource.ColorProperty,new Binding(nameof(button.TextColor),source:button));button.ImageSource=source;
            button.ContentLayout=new Microsoft.Maui.Controls.Button.ButtonContentLayout(Microsoft.Maui.Controls.Button.ButtonContentLayout.ImagePosition.Left,8);
            // WidthRequest reaches Android's exact measure spec. A minimum
            // width only enlarges MAUI's frame and can leave native text clipped.
            void MeasureContent()
            {
                var metrics=global::Android.App.Application.Context.Resources!.DisplayMetrics!;
                using var paint=new global::Android.Graphics.Paint();
                if(OperatingSystem.IsAndroidVersionAtLeast(28))paint.SetTypeface(global::Android.Graphics.Typeface.Create(global::Android.Graphics.Typeface.Default,500,false));
                paint.TextSize=global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Sp,(float)button.FontSize,metrics);
                button.WidthRequest=Math.Ceiling(paint.MeasureText(button.Text??"")/metrics.Density+button.Padding.HorizontalThickness+18+8+4);
            }
            MeasureContent();button.PropertyChanged+=(_,e)=>{if(e.PropertyName is nameof(button.Text) or nameof(button.FontSize) or nameof(button.Padding))MeasureContent();};
        }
        Md.SetTouchInset(button,4);
        button.HandlerChanged+=(_,_)=>{if(button.Handler?.PlatformView is Google.Android.Material.Button.MaterialButton native){native.SetMinimumHeight(0);native.SetMinWidth(0);}};
        button.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(button.IsEnabled))button.Opacity=button.IsEnabled?1:Md.DisabledContent;};
        // A busy flag, not IsEnabled: actions may disable the button themselves (e.g. an SMS cooldown).
        var busy=false;
        button.Clicked+=async(_,_)=>{if(busy||!button.IsEnabled)return;busy=true;try{await action();}finally{busy=false;}};
        return button;
    }
    public static IconButton Icon(string name,Func<Task> action,string description,IconStyle style=IconStyle.Standard,string? foreground=null)=>new(name,action,description,style,foreground:foreground);
    public static Border Card(View child,string background="SurfaceContainerHighest",double radius=Md.ShapeMedium,Thickness? padding=null)
    {
        var border=new Border {Content=child,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=radius},Padding=padding??new Thickness(16)};
        border.SetDynamicResource(VisualElement.BackgroundColorProperty,background);return border;
    }
    public static Border OutlinedCard(View child,double radius=Md.ShapeMedium,Thickness? padding=null)
    {
        var border=Card(child,"Surface",radius,padding);border.StrokeThickness=1;border.SetDynamicResource(Border.StrokeProperty,"OutlineVariant");return border;
    }
    public static BoxView Divider(){var b=new BoxView{HeightRequest=1,InputTransparent=true};b.SetDynamicResource(BoxView.ColorProperty,"OutlineVariant");return b;}
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
    // Fixed-size artwork. The native rounded background doubles as the tonal
    // placeholder; it is painted by Android rather than MAUI so the fill and the
    // clip share one rounded outline.
    public static Image Cover(string? url,double size=56,double radius=Md.ShapeSmall)
    {
        var image=new Image{Aspect=Aspect.AspectFill,WidthRequest=size,HeightRequest=size,Source=string.IsNullOrWhiteSpace(url)?null:Art(url,(int)Math.Max(200,size*3)),InputTransparent=true};
        RoundedCover.Attach(image,radius);return image;
    }
    public static Image Avatar(string? url,double size=40)=>Cover(url,size,size/2);
    // Square artwork that fills its column; the Border clips to the shape at any width.
    public static Border Artwork(Image image,double radius=Md.ShapeLarge)
    {
        image.Aspect=Aspect.AspectFill;image.WidthRequest=-1;image.HeightRequest=-1;image.Clip=null;image.InputTransparent=true;
        var frame=new Border{Content=image,StrokeThickness=0,Padding=0,StrokeShape=new RoundRectangle{CornerRadius=radius},InputTransparent=true};
        frame.SetDynamicResource(VisualElement.BackgroundColorProperty,"SurfaceContainerHighest");
        frame.SizeChanged+=(_,_)=>{if(frame.Width>0&&Math.Abs(frame.HeightRequest-frame.Width)>.5)frame.HeightRequest=frame.Width;};
        return frame;
    }
    // Material 3 list item: 56dp one-line / 72dp two-line, leading icon, image or avatar, trailing element.
    public static Grid ListItem(string title,string? supporting,Func<Task>? action,string? icon=null,string? cover=null,View? trailing=null,string? trailingIcon="chevron",bool avatar=false,string iconColor="OnSurfaceVariant")
    {
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=16,Padding=new Thickness(16,8,trailing!=null?8:16,8),MinimumHeightRequest=string.IsNullOrEmpty(supporting)?56:72};
        if(cover!=null)grid.Add(avatar?Avatar(cover):Cover(cover));
        else if(icon!=null)grid.Add(Glyph(icon,24,iconColor));
        else grid.ColumnSpacing=0;
        var words=new VerticalStackLayout{Spacing=0,VerticalOptions=LayoutOptions.Center,InputTransparent=true};words.Add(Line(title,TypeRole.BodyLarge));
        if(!string.IsNullOrEmpty(supporting)){var detail=Text(supporting,TypeRole.BodyMedium,"OnSurfaceVariant");detail.MaxLines=2;detail.LineBreakMode=LineBreakMode.TailTruncation;words.Add(detail);}
        grid.Add(words,1);
        if(trailing!=null)grid.Add(trailing,2);else if(trailingIcon!=null&&action!=null)grid.Add(Glyph(trailingIcon,24,"OnSurfaceVariant"),2);
        if(action!=null)Pressable.Attach(grid,action,description:title);
        return grid;
    }
    public static View Heading(string title,string subtitle="")
    {
        var stack=new VerticalStackLayout{Spacing=8,Margin=new Thickness(0,8,0,24)};
        stack.Add(Text(title,TypeRole.HeadlineSmall));if(subtitle.Length>0)stack.Add(Text(subtitle,TypeRole.BodyMedium,"OnSurfaceVariant"));return stack;
    }
    // Content section header (Title Large) with an optional trailing action.
    public static View Section(string title,Func<Task>? action=null,string? actionText=null,string actionIcon="chevron")
    {
        var grid=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],MinimumHeightRequest=48,Margin=new Thickness(0,16,0,4)};
        grid.Add(Line(title,TypeRole.TitleLarge));
        if(action!=null)
        {
            View trailing=actionText!=null?Button(actionText,action,"text"):Icon(actionIcon,action,"查看"+title);
            trailing.Margin=new Thickness(0,0,-12,0);grid.Add(trailing,1);
        }
        return grid;
    }
    // Settings-style category label.
    public static Label Category(string text)
    {
        var label=Text(text,TypeRole.LabelLarge,"Primary");label.Margin=new Thickness(16,24,16,8);return label;
    }
    public static View Empty(string title,string description,string icon)=>Empty(title,description,null,icon);
    public static View Empty(string title,string description,Func<Task>? retry=null,string icon="library")
    {
        var stack=new VerticalStackLayout {Spacing=12,Padding=new Thickness(32,48),HorizontalOptions=LayoutOptions.Fill};
        var bubble=Card(Glyph(icon,36,"OnSecondaryContainer"),"SecondaryContainer",36,0);bubble.WidthRequest=72;bubble.HeightRequest=72;bubble.HorizontalOptions=LayoutOptions.Center;bubble.Margin=new Thickness(0,0,0,8);stack.Add(bubble);
        var h=Text(title,TypeRole.TitleLarge);h.HorizontalTextAlignment=TextAlignment.Center;stack.Add(h);
        var p=Text(description,TypeRole.BodyMedium,"OnSurfaceVariant");p.HorizontalTextAlignment=TextAlignment.Center;stack.Add(p);
        if(retry!=null){var button=Button("重试",retry,"tonal","history");button.HorizontalOptions=LayoutOptions.Center;button.Margin=new Thickness(0,8,0,0);stack.Add(button);}
        return stack;
    }
    private static readonly SemaphoreSlim navigation=new(1,1);
    public static async Task Push(Page page){if(!await navigation.WaitAsync(0))return;try{await PageMotion.PushAsync(page);}finally{navigation.Release();}}
    public static async Task Pop(){if(!await navigation.WaitAsync(0))return;try{if(Shell.Current.Navigation.NavigationStack.Count>1)await PageMotion.PopAsync();}finally{navigation.Release();}}
    public static async Task PopToRoot(ShellSection section){if(!await navigation.WaitAsync(0))return;try{await section.Navigation.PopToRootAsync(PageMotion.Enabled);}finally{navigation.Release();}}
    public static async Task Tab(string route){if(route=="search"){await Push(new SearchPage());return;}if(!await navigation.WaitAsync(0))return;try{await Shell.Current.GoToAsync("//main/"+route+"Tab/"+route);}finally{navigation.Release();}}
    public static string Duration(int milliseconds)=>TimeSpan.FromMilliseconds(milliseconds).ToString(milliseconds>=3600000?@"h\:mm\:ss":@"m\:ss");
}
