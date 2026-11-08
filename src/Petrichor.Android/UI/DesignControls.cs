using Microsoft.Maui.Controls.Shapes;
using Entry=Petrichor.Mobile.UI.NativeEntry;
namespace Petrichor.Mobile.UI;

// Material 3 search bar: 56dp, full corner, surface-container-high.
public sealed class SearchField : ContentView
{
    private readonly Entry input=new(){FontSize=16,ReturnType=ReturnType.Search,ClearButtonVisibility=ClearButtonVisibility.Never,Placeholder="搜索",HeightRequest=56,MinimumHeightRequest=0};
    private readonly IconButton clear;
    public string? Text {get=>input.Text;set=>input.Text=value;}
    public string Placeholder {get=>input.Placeholder;set=>input.Placeholder=value;}
    public event EventHandler? SearchButtonPressed;
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public SearchField()
    {
        var grid=new Grid{ColumnDefinitions=[new(24),new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=16,Padding=new Thickness(16,0,4,0),HeightRequest=56};
        grid.Add(Ui.Glyph("search",24,"OnSurface"));grid.Add(input,1);
        clear=Ui.Icon("close",()=>{input.Text="";input.Focus();return Task.CompletedTask;},"清空搜索");clear.IsVisible=false;grid.Add(clear,2);
        Content=Ui.Card(grid,"SurfaceContainerHigh",28,0);
        input.TextChanged+=(_,e)=>{
            var show=!string.IsNullOrEmpty(e.NewTextValue);
            if(show!=clear.IsVisible){clear.IsVisible=show;if(show&&PageMotion.Enabled){clear.Opacity=0;clear.Scale=.6;_=clear.FadeToAsync(1,Md.Short3,Md.StandardDecelerate);_=clear.ScaleToAsync(1,Md.Short3,Md.EmphasizedDecelerate);}}
            TextChanged?.Invoke(this,e);
        };
        input.Completed+=(_,_)=>{
            if(input.Handler?.PlatformView is global::Android.Views.View native)
                (native.Context?.GetSystemService(global::Android.Content.Context.InputMethodService) as global::Android.Views.InputMethods.InputMethodManager)?.HideSoftInputFromWindow(native.WindowToken,global::Android.Views.InputMethods.HideSoftInputFlags.None);
            input.Unfocus();SearchButtonPressed?.Invoke(this,EventArgs.Empty);
        };
    }
    public new bool Focus()=>input.Focus();
}

// Material 3 primary tabs: Title Small labels, a 3dp indicator as wide as the label that slides between tabs.
public sealed class TabStrip : Grid
{
    private readonly List<Label> labels=[];
    private readonly BoxView indicator;
    private readonly Func<int,Task> select;
    public int Selected {get;private set;}
    public TabStrip(string[] names,Func<int,Task> select,int initial=0)
    {
        this.select=select;RowDefinitions=[new(48),new(1)];Selected=initial;
        var tabs=new Grid();
        for(int i=0;i<names.Length;i++)
        {
            tabs.ColumnDefinitions.Add(new(GridLength.Star));var index=i;
            var label=Ui.Line(names[i],TypeRole.TitleSmall,"OnSurfaceVariant");label.HorizontalOptions=LayoutOptions.Center;label.HorizontalTextAlignment=TextAlignment.Center;labels.Add(label);
            var cell=new Grid{InputTransparent=false};cell.Add(label);
            Pressable.Attach(cell,()=>Choose(index),description:names[i]);
            tabs.Add(cell,i);
            label.SizeChanged+=(_,_)=>{if(index==Selected)Place(false);};
        }
        Add(tabs);
        indicator=new BoxView{HeightRequest=3,WidthRequest=24,CornerRadius=new CornerRadius(3,3,0,0),HorizontalOptions=LayoutOptions.Start,VerticalOptions=LayoutOptions.End,InputTransparent=true};
        indicator.SetDynamicResource(BoxView.ColorProperty,"Primary");Add(indicator);
        var line=Ui.Divider();this.Add(line,0,1);
        SizeChanged+=(_,_)=>Place(false);
        SetSelected(initial);
    }
    private async Task Choose(int index)
    {
        if(index==Selected||!IsEnabled)return;
        SetSelected(index,true);await select(index);
    }
    public void SetSelected(int index,bool animate=false)
    {
        Selected=index;
        for(int i=0;i<labels.Count;i++)labels[i].SetDynamicResource(Label.TextColorProperty,i==index?"Primary":"OnSurfaceVariant");
        Place(animate);
    }
    private void Place(bool animate)
    {
        if(Width<=0||labels.Count==0)return;
        var column=Width/labels.Count;var label=labels[Selected];
        var width=Math.Max(24,label.Width>0?label.Width:column*.5);var x=column*Selected+(column-width)/2;
        this.AbortAnimation("Indicator");
        if(!animate||!PageMotion.Enabled){indicator.TranslationX=x;indicator.WidthRequest=width;return;}
        var fromX=indicator.TranslationX;var fromWidth=indicator.WidthRequest;
        new Animation(t=>{indicator.TranslationX=fromX+(x-fromX)*t;indicator.WidthRequest=fromWidth+(width-fromWidth)*t;}).Commit(this,"Indicator",16,Md.Medium1,Md.Standard);
    }
}

// Material 3 filter chip: 32dp, small corner, leading check mark when selected.
public sealed class FilterChip : Border
{
    private readonly Label check,label;
    private bool selected;
    public string Title {get;}
    public FilterChip(string title,Func<Task> action,bool selected=false,bool showCheck=true)
    {
        Title=title;HeightRequest=32;Padding=new Thickness(16,0);StrokeShape=new RoundRectangle{CornerRadius=Md.ShapeSmall};VerticalOptions=LayoutOptions.Center;
        check=Ui.Glyph("check",18,"OnSecondaryContainer");check.IsVisible=false;
        label=Ui.Line(title,TypeRole.LabelLarge,"OnSurfaceVariant");
        var row=new HorizontalStackLayout{Spacing=8,VerticalOptions=LayoutOptions.Center,InputTransparent=true};row.Add(check);row.Add(label);Content=row;
        ShowCheck=showCheck;
        Pressable.Attach(this,action,radius:Md.ShapeSmall,description:title);
        this.selected=!selected;IsSelected=selected;
    }
    public bool ShowCheck {get;set;}
    public string Text {get=>label.Text;set{label.Text=value;SemanticProperties.SetDescription(this,value);}}
    public bool IsSelected
    {
        get=>selected;
        set
        {
            if(selected==value)return;selected=value;
            StrokeThickness=value?0:1;this.SetDynamicResource(StrokeProperty,"OutlineVariant");
            this.SetDynamicResource(BackgroundColorProperty,value?"SecondaryContainer":"Transparent");
            label.SetDynamicResource(Label.TextColorProperty,value?"OnSecondaryContainer":"OnSurfaceVariant");
            var showCheck=value&&ShowCheck;Padding=showCheck?new Thickness(8,0,16,0):new Thickness(16,0);
            if(check.IsVisible!=showCheck){check.IsVisible=showCheck;if(showCheck&&PageMotion.Enabled&&Handler!=null){check.Scale=.3;_=check.ScaleToAsync(1,Md.Short4,Md.EmphasizedDecelerate);}}
        }
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
    public static FilterChip Chip(string title,Func<Task> action,bool selected=false)=>new(title,action,selected);
    public static HorizontalStackLayout ChipRow(params FilterChip[] chips)
    {
        var row=new HorizontalStackLayout{Spacing=8,Padding=new Thickness(0,8)};foreach(var chip in chips)row.Add(chip);return row;
    }
    // Horizontally scrolling chip row that bleeds to the screen edges while aligning with the content margin.
    public static View ChipScroller(View row,double margin=Md.Margin)
    {
        row.Margin=new Thickness(margin,0);
        return new ScrollView{Content=row,Orientation=ScrollOrientation.Horizontal,HorizontalScrollBarVisibility=ScrollBarVisibility.Never,Margin=new Thickness(-margin,0)};
    }
    public static View Shortcuts(params (string Title,string Icon,Func<Task> Action)[] items)
    {
        var grid=new Grid{ColumnSpacing=8,Margin=new Thickness(0,8,0,0)};
        for(int i=0;i<items.Length;i++)
        {
            grid.ColumnDefinitions.Add(new(GridLength.Star));var item=items[i];
            var inner=new VerticalStackLayout{Spacing=8,HorizontalOptions=LayoutOptions.Center,InputTransparent=true};
            var bubble=Ui.Card(Ui.Glyph(item.Icon,24,"OnSecondaryContainer"),"SecondaryContainer",24,0);bubble.WidthRequest=48;bubble.HeightRequest=48;bubble.HorizontalOptions=LayoutOptions.Center;inner.Add(bubble);
            var text=Ui.Line(item.Title,TypeRole.LabelLarge);text.HorizontalTextAlignment=TextAlignment.Center;inner.Add(text);
            var card=Ui.Card(inner,"SurfaceContainer",Md.ShapeLarge,new Thickness(8,16));
            Pressable.Attach(card,item.Action,radius:Md.ShapeLarge,description:item.Title);grid.Add(card,i);
        }
        return grid;
    }
    // Material 3 outlined text field. The label rests inside the field when it is empty and unfocused,
    // and floats into the outline notch otherwise. Space for the floated label is reserved above the
    // outline instead of translating it outside the measured bounds, where Android clips glyphs.
    public static View Field(string label,View input,View? trailing=null,string background="Surface",string? supporting=null)
    {
        const double notch=12;
        var body=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=4,Padding=new Thickness(16,0,trailing==null?16:4,0),MinimumHeightRequest=56};body.Add(input);if(trailing!=null)body.Add(trailing,1);
        var frame=new Border{StrokeThickness=1,StrokeShape=new RoundRectangle{CornerRadius=Md.ShapeExtraSmall},Content=body,Padding=0};frame.SetDynamicResource(Border.StrokeProperty,"Outline");
        var caption=Ui.Line(label,TypeRole.BodyLarge,"OnSurfaceVariant");caption.Padding=new Thickness(4,0);caption.Margin=new Thickness(12,0,0,0);caption.HorizontalOptions=LayoutOptions.Start;caption.VerticalOptions=LayoutOptions.Start;caption.AnchorX=0;caption.AnchorY=.5;caption.InputTransparent=true;
        caption.SetDynamicResource(VisualElement.BackgroundColorProperty,background);
        var grid=new Grid{RowDefinitions=[new(notch),new(56)]};grid.Add(frame,0,1);grid.Add(caption);Grid.SetRowSpan(caption,2);
        var entries=FindEntries(input).ToList();var hints=entries.Select(e=>e.Placeholder).ToList();
        bool? floated=null;
        void Update(bool animate)
        {
            var focused=entries.Any(entry=>entry.IsFocused);var filled=entries.Any(entry=>!string.IsNullOrEmpty(entry.Text));var up=focused||filled;
            frame.StrokeThickness=focused?2:1;frame.SetDynamicResource(Border.StrokeProperty,focused?"Primary":"Outline");
            caption.SetDynamicResource(Label.TextColorProperty,focused?"Primary":"OnSurfaceVariant");
            for(var i=0;i<entries.Count;i++)entries[i].Placeholder=up?hints[i]:"";
            if(caption.Height<=0)return;
            var y=up?notch-caption.Height/2:notch+28-caption.Height/2;var scale=up?.75:1;
            if(floated==up&&Math.Abs(caption.TranslationY-y)<.5)return;floated=up;
            caption.AbortAnimation("Float");
            if(!animate||!PageMotion.Enabled){caption.TranslationY=y;caption.Scale=scale;return;}
            var fromY=caption.TranslationY;var fromScale=caption.Scale;
            new Animation(t=>{caption.TranslationY=fromY+(y-fromY)*t;caption.Scale=fromScale+(scale-fromScale)*t;}).Commit(caption,"Float",16,Md.Short3,Md.Standard);
        }
        caption.SizeChanged+=(_,_)=>{floated=null;Update(false);};
        foreach(var entry in entries){entry.HeightRequest=56;entry.MinimumHeightRequest=0;entry.VerticalTextAlignment=TextAlignment.Center;entry.Focused+=(_,_)=>Update(true);entry.Unfocused+=(_,_)=>Update(true);entry.TextChanged+=(_,_)=>Update(true);}
        Update(false);
        if(supporting==null)return grid;
        var stack=new VerticalStackLayout{Spacing=4};stack.Add(grid);var help=Ui.Text(supporting,TypeRole.BodySmall,"OnSurfaceVariant");help.Margin=new Thickness(16,0);stack.Add(help);return stack;
        static IEnumerable<Entry> FindEntries(View view){if(view is Entry entry)yield return entry;if(view is Layout layout)foreach(var child in layout.Children.OfType<View>())foreach(var found in FindEntries(child))yield return found;}
    }
    public static View Notice(string text,string icon="info")
    {
        var row=new Grid{ColumnDefinitions=[new(20),new(GridLength.Star)],ColumnSpacing=12};row.Add(Ui.Glyph(icon,20,"OnSurfaceVariant"));row.Add(Ui.Text(text,TypeRole.BodySmall,"OnSurfaceVariant"),1);
        return Ui.Card(row,"SurfaceContainerHigh",Md.ShapeMedium);
    }
    // One row of a settings list.
    public static Grid SettingItem(string title,string supporting,View? trailing=null,string? icon=null,Func<Task>? action=null)
        =>Ui.ListItem(title,supporting,action,icon,trailing:trailing,trailingIcon:trailing==null&&action!=null?"chevron":null);
    // Material 3 segmented list: items share one rounded group, separated by 2dp gaps.
    public static View Group(params View[] items)=>GroupOn("SurfaceContainer",items);
    public static View GroupOn(string background,params View[] items)
    {
        var stack=new VerticalStackLayout{Spacing=2};
        for(var i=0;i<items.Length;i++)
        {
            double top=i==0?Md.ShapeLarge:Md.ShapeExtraSmall,bottom=i==items.Length-1?Md.ShapeLarge:Md.ShapeExtraSmall;
            var cell=new Border{Content=items[i],Padding=0,StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=new CornerRadius(top,top,bottom,bottom)}};
            cell.SetDynamicResource(VisualElement.BackgroundColorProperty,background);stack.Add(cell);
        }
        return stack;
    }
    public static View DetailHead(string cover,string name,string meta,string kind,bool round=false)
    {
        var stack=new VerticalStackLayout{Spacing=4,Padding=new Thickness(0,8,0,0)};
        var art=Ui.Cover(cover,200,round?100:Md.ShapeExtraLarge);art.HorizontalOptions=LayoutOptions.Center;art.Margin=new Thickness(0,0,0,16);stack.Add(art);
        var category=Ui.Line(kind,TypeRole.LabelMedium,"OnSurfaceVariant");category.CharacterSpacing=1;category.HorizontalTextAlignment=TextAlignment.Center;stack.Add(category);
        var title=Ui.Text(name,TypeRole.HeadlineSmall);title.MaxLines=2;title.LineBreakMode=LineBreakMode.TailTruncation;title.HorizontalTextAlignment=TextAlignment.Center;stack.Add(title);
        var detail=Ui.Text(meta,TypeRole.BodyMedium,"OnSurfaceVariant");detail.HorizontalTextAlignment=TextAlignment.Center;stack.Add(detail);return stack;
    }
    public static HorizontalStackLayout Actions(params View[] buttons)
    {
        var row=new HorizontalStackLayout{Spacing=8,HorizontalOptions=LayoutOptions.Center,Margin=new Thickness(0,16,0,0)};foreach(var b in buttons)row.Add(b);return row;
    }
}
