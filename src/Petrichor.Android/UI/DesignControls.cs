using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Platform;
using Entry=Petrichor.Mobile.UI.NativeEntry;
namespace Petrichor.Mobile.UI;

public sealed class SearchField : ContentView
{
    private readonly Entry input=new(){FontSize=Ui.F(16),ReturnType=ReturnType.Search,ClearButtonVisibility=ClearButtonVisibility.Never,Placeholder="搜索",HeightRequest=Ui.S(56),MinimumHeightRequest=0};
    public string? Text {get=>input.Text;set=>input.Text=value;}
    public string Placeholder {get=>input.Placeholder;set=>input.Placeholder=value;}
    public event EventHandler? SearchButtonPressed;
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public SearchField()
    {
        var grid=new Grid{ColumnDefinitions=[new(Ui.S(24)),new(GridLength.Star),new(Ui.S(32))],ColumnSpacing=Ui.S(12),Padding=Ui.Inset(16,0),HeightRequest=Ui.S(56)};
        grid.Add(Ui.Glyph("search",24,"Muted"));grid.Add(input,1);
        var clear=Ui.Icon("close",()=>{input.Text="";input.Focus();return Task.CompletedTask;},"清空搜索");clear.WidthRequest=Ui.S(32);clear.IsVisible=false;grid.Add(clear,2);
        Content=Ui.Card(grid,radius:28,padding:0);
        input.TextChanged+=(_,e)=>{clear.IsVisible=!string.IsNullOrEmpty(e.NewTextValue);TextChanged?.Invoke(this,e);};
        input.Completed+=(_,_)=>{
            if(input.Handler?.PlatformView is global::Android.Views.View native)
                (native.Context?.GetSystemService(global::Android.Content.Context.InputMethodService) as global::Android.Views.InputMethods.InputMethodManager)?.HideSoftInputFromWindow(native.WindowToken,global::Android.Views.InputMethods.HideSoftInputFlags.None);
            input.Unfocus();SearchButtonPressed?.Invoke(this,EventArgs.Empty);
        };
    }
}

public sealed class TabStrip : Grid
{
    private readonly List<Button> buttons=[];
    private readonly List<BoxView> indicators=[];
    public int Selected {get;private set;}
    public TabStrip(string[] names,Func<int,Task> select,int initial=0)
    {
        RowDefinitions=[new(48),new(1)];Selected=initial;
        for(int i=0;i<names.Length;i++)
        {
            ColumnDefinitions.Add(new(GridLength.Star));var index=i;
            var button=Ui.Button(names[i],async()=>{SetSelected(index);await select(index);},"text");button.Padding=0;button.FontSize=14;button.HeightRequest=48;buttons.Add(button);this.Add(button,i);
            var indicator=new BoxView{WidthRequest=Ui.S(48),HeightRequest=Ui.S(3),CornerRadius=2,HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.End};
            indicator.SetDynamicResource(BoxView.ColorProperty,"Primary");indicators.Add(indicator);this.Add(indicator,i,1);
        }
        var line=Ui.Divider();this.Add(line,0,1);Grid.SetColumnSpan((BindableObject)line,names.Length);
        foreach(var indicator in indicators){Children.Remove(indicator);Children.Add(indicator);}
        SetSelected(initial);
    }
    public void SetSelected(int index)
    {
        Selected=index;for(int i=0;i<buttons.Count;i++){buttons[i].SetDynamicResource(Button.TextColorProperty,i==index?"Primary":"OnSurface");indicators[i].IsVisible=i==index;}
    }
}

public static class DesignControls
{
    public static void StyleSlider(Slider slider)
    {
        void Apply()
        {
            if(slider.Handler?.PlatformView is not global::Android.Widget.SeekBar native)return;
            var density=native.Resources!.DisplayMetrics!.Density;int D(int value)=>(int)Math.Round(value*density);
            native.ProgressDrawable=new WaveTrackDrawable(native);
            native.SetThumb(new MusicThumbDrawable(native));native.ThumbOffset=D(2);native.SplitTrack=false;native.Background=null;native.SetPadding(D(12),0,D(12),0);native.SetMinimumHeight(D(48));
        }
        slider.HandlerChanged+=(_,_)=>Apply();slider.PropertyChanged+=(_,e)=>{if(e.PropertyName is nameof(Slider.MinimumTrackColor) or nameof(Slider.MaximumTrackColor) or nameof(Slider.ThumbColor))Apply();};
    }
    public static Button Chip(string title,Func<Task> action,bool selected=false)
    {
        var b=Ui.Button(title,action,"outlined");b.AutomationId=title;SemanticProperties.SetDescription(b,title);b.HeightRequest=Ui.S(36);b.MinimumHeightRequest=Ui.S(36);b.CornerRadius=8;b.FontSize=Ui.F(13);b.Padding=Ui.Inset(16,0);SelectChip(b,selected);return b;
    }
    public static void SelectChip(Button b,bool selected)
    {
        var title=b.AutomationId??b.Text??"";b.Text=selected?"✓  "+title:title;using var paint=new global::Android.Graphics.Paint();var metrics=global::Android.App.Application.Context.Resources!.DisplayMetrics!;paint.TextSize=global::Android.Util.TypedValue.ApplyDimension(global::Android.Util.ComplexUnitType.Sp,(float)b.FontSize,metrics);
        b.WidthRequest=Math.Ceiling(paint.MeasureText(b.Text)/metrics.Density+b.Padding.HorizontalThickness+12);
        b.BorderWidth=selected?0:1;b.SetDynamicResource(Button.BorderColorProperty,"Outline");
        b.ClearValue(Button.BackgroundColorProperty);b.SetDynamicResource(Button.BackgroundColorProperty,selected?"PrimaryContainer":"Transparent");
        b.SetDynamicResource(Button.TextColorProperty,selected?"OnPrimaryContainer":"OnSurface");
        b.ImageSource=null;
    }
    public static View Shortcuts(params (string Title,string Icon,Func<Task> Action)[] items)
    {
        var grid=new Grid{ColumnSpacing=Ui.S(10),Margin=Ui.Inset(0,20,0,0)};
        for(int i=0;i<items.Length;i++)
        {
            grid.ColumnDefinitions.Add(new(GridLength.Star));var item=items[i];
            var inner=new VerticalStackLayout{Spacing=Ui.S(10),HorizontalOptions=LayoutOptions.Center};
            var bubble=Ui.Card(Ui.Glyph(item.Icon,24,"Primary"),"PrimaryContainer",16,0);bubble.WidthRequest=Ui.S(44);bubble.HeightRequest=Ui.S(44);inner.Add(bubble);
            var text=Ui.Text(item.Title,13);text.HorizontalTextAlignment=TextAlignment.Center;inner.Add(text);
            var card=Ui.Card(inner,radius:20,padding:Ui.Inset(8,16));
            var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await item.Action();card.GestureRecognizers.Add(tap);SemanticProperties.SetDescription(card,item.Title);grid.Add(card,i);
        }
        return grid;
    }
    public static View Field(string label,View input,View? trailing=null,string background="Surface")
    {
        var body=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=Ui.S(8),Padding=Ui.Inset(14,0),MinimumHeightRequest=Ui.S(56)};body.Add(input);if(trailing!=null)body.Add(trailing,1);
        var frame=new Border{StrokeThickness=1,StrokeShape=new RoundRectangle{CornerRadius=6},Content=body};frame.SetDynamicResource(Border.StrokeProperty,"Muted");
        var caption=Ui.Text(label,12,"Muted");caption.Padding=Ui.Inset(4,0);caption.Margin=Ui.Inset(12,0,0,0);caption.HorizontalOptions=LayoutOptions.Start;caption.VerticalOptions=LayoutOptions.Start;caption.SetDynamicResource(VisualElement.BackgroundColorProperty,background);
        // Reserve space for the floating label instead of translating it outside
        // its measured bounds, where Android clips part of the glyphs.
        var grid=new Grid{Margin=Ui.Inset(0,14,0,0),RowDefinitions=[new(Ui.S(8)),new(Ui.S(56))]};grid.Add(frame,0,1);grid.Add(caption);Grid.SetRowSpan(caption,2);
        var entries=FindEntries(input).ToList();
        void FocusChanged(object? sender,FocusEventArgs e){var focused=entries.Any(entry=>entry.IsFocused);frame.StrokeThickness=focused?2:1;frame.SetDynamicResource(Border.StrokeProperty,focused?"Primary":"Muted");caption.SetDynamicResource(Label.TextColorProperty,focused?"Primary":"Muted");}
        foreach(var entry in entries){entry.HeightRequest=Ui.S(56);entry.MinimumHeightRequest=0;entry.VerticalTextAlignment=TextAlignment.Center;entry.Focused+=FocusChanged;entry.Unfocused+=FocusChanged;}
        return grid;
        static IEnumerable<Entry> FindEntries(View view){if(view is Entry entry)yield return entry;if(view is Layout layout)foreach(var child in layout.Children.OfType<View>())foreach(var found in FindEntries(child))yield return found;}
    }
    public static View Notice(string text,string icon="info")
    {
        var row=new Grid{ColumnDefinitions=[new(Ui.S(20)),new(GridLength.Star)],ColumnSpacing=Ui.S(10)};row.Add(Ui.Glyph(icon,20,"Primary"));row.Add(Ui.Text(text,12,"Muted"),1);return Ui.Card(row,radius:16);
    }
    public static View SettingRow(string title,string subtitle,View trailing,string? icon=null,Func<Task>? action=null)
    {
        var row=new Grid{ColumnDefinitions=[new(Ui.S(icon==null?0:24)),new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=Ui.S(icon==null?0:16),MinimumHeightRequest=Ui.S(80)};
        if(icon!=null)row.Add(Ui.Glyph(icon));var info=new VerticalStackLayout{Spacing=Ui.S(5),VerticalOptions=LayoutOptions.Center};info.Add(Ui.Text(title,15));if(subtitle.Length>0)info.Add(Ui.Text(subtitle,12,"Muted"));row.Add(info,1);row.Add(trailing,2);
        if(action!=null){var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await action();info.GestureRecognizers.Add(tap);}return row;
    }
    public static View DetailHead(string cover,string name,string meta,string kind)
    {
        var stack=new VerticalStackLayout{Spacing=Ui.S(8)};var art=Ui.Cover(cover,180);art.Clip=new RoundRectangleGeometry(new CornerRadius(24),new Rect(0,0,Ui.S(180),Ui.S(180)));art.Margin=Ui.Inset(0,8,0,12);art.HorizontalOptions=LayoutOptions.Center;stack.Add(art);
        var category=Ui.Text(kind,11,"Muted");category.CharacterSpacing=1.5;category.HorizontalTextAlignment=TextAlignment.Center;stack.Add(category);
        var title=Ui.Text(name,26);title.HorizontalTextAlignment=TextAlignment.Center;title.Margin=Ui.Inset(0,2,0,0);stack.Add(title);
        var detail=Ui.Text(meta,12,"Muted");detail.HorizontalTextAlignment=TextAlignment.Center;stack.Add(detail);return stack;
    }
}
