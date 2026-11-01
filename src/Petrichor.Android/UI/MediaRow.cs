using System.ComponentModel;
using System.Runtime.CompilerServices;
using View=Microsoft.Maui.Controls.View;
namespace Petrichor.Mobile.UI;

public sealed class MediaRow : INotifyPropertyChanged
{
    public required string Title {get;init;}
    private string subtitle="";
    public string Subtitle {get=>subtitle;set{if(subtitle==value)return;subtitle=value;Notify();}}
    public string Cover {get;init;}="";
    public required object Value {get;init;}
    public string? Index {get;init;}
    public bool IsCurrent {get;init;}
    private bool selecting,selected;
    public bool IsSelecting {get=>selecting;set{selecting=value;Notify();Notify(nameof(ShowCover));Notify(nameof(ShowIndex));}}
    public bool IsSelected {get=>selected;set{selected=value;Notify();}}
    public bool ShowCover=>!IsSelecting&&Index==null;
    public bool ShowIndex=>!IsSelecting&&Index!=null;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName]string? key=null)=>PropertyChanged?.Invoke(this,new(key));
}
public sealed class MediaRowView : ContentView
{
    private global::Android.Views.View? native;
    private DateTimeOffset ignoreClickUntil;
    private readonly Func<MediaRow,Task> open,menu;
    private readonly Label title=Ui.Text("",15),subtitle=Ui.Text("",12,"Muted");
    private readonly Image cover=Ui.Cover("",52);
    public MediaRowView(Func<MediaRow,Task> open,Func<MediaRow,Task> menu)
    {
        this.open=open;this.menu=menu;
        title.FontSize=14;title.FontAttributes=FontAttributes.Bold;subtitle.FontSize=12;cover.WidthRequest=48;cover.HeightRequest=48;cover.Clip=new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry(new CornerRadius(8),new Rect(0,0,48,48));
        var check=new CheckBox{WidthRequest=Ui.S(48)};
        check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(MediaRow.IsSelected),BindingMode.TwoWay));
        check.SetBinding(IsVisibleProperty,nameof(MediaRow.IsSelecting));
        cover.SetBinding(IsVisibleProperty,nameof(MediaRow.ShowCover));
        var index=Ui.Text("",14,"Muted");index.HorizontalTextAlignment=Microsoft.Maui.TextAlignment.Center;index.SetBinding(Label.TextProperty,nameof(MediaRow.Index));index.SetBinding(IsVisibleProperty,nameof(MediaRow.ShowIndex));
        var leading=new Grid();leading.Add(cover);leading.Add(check);leading.Add(index);
        var info=new VerticalStackLayout{Spacing=Ui.S(4),VerticalOptions=LayoutOptions.Center};
        subtitle.SetBinding(Label.TextProperty,nameof(MediaRow.Subtitle));title.LineBreakMode=LineBreakMode.TailTruncation;subtitle.LineBreakMode=LineBreakMode.TailTruncation;info.Add(title);info.Add(subtitle);
        var more=Ui.Icon("more",async()=>{if(BindingContext is MediaRow item)await menu(item);},"更多操作");
        var rowHeight=Ui.MediaRowHeight;HeightRequest=rowHeight;
        var row=new Grid{ColumnDefinitions=[new(48),new(GridLength.Star),new(48)],ColumnSpacing=12,Padding=new Thickness(12,8,0,8),HeightRequest=rowHeight};
        check.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(CheckBox.IsVisible))row.ColumnDefinitions[0].Width=check.IsVisible?48:BindingContext is MediaRow{Index:not null}?28:48;};
        row.Add(leading);row.Add(info,1);row.Add(more,2);Content=row;
        string? coverUrl=null;
        BindingContextChanged+=(_,_)=>{if(BindingContext is MediaRow item){title.Text=item.Title;subtitle.IsVisible=item.Subtitle.Length>0;if(item.ShowCover&&coverUrl!=item.Cover){coverUrl=item.Cover;cover.Source=Ui.Art(item.Cover,200);}row.ColumnDefinitions[0].Width=item.IsSelecting||item.Index==null?48:28;
            // Flat rows need no clipping/painting Border. Only the current queue
            // item uses a rounded highlight.
            if(item.IsCurrent&&Content is not Border){Content=null;Content=Ui.Card(row,"PrimaryContainer",16,0);}
            else if(!item.IsCurrent&&Content is Border old){old.Content=null;Content=row;}
            if(Content is Border surface)surface.Margin=Ui.Inset(12,4);
            row.HeightRequest=item.IsCurrent?rowHeight-8:rowHeight;row.Padding=item.IsCurrent?Ui.Inset(12,6,0,6):new Thickness(12,8,0,8);SemanticProperties.SetDescription(this,item.Title+"，"+item.Subtitle);}};
        HandlerChanged+=(_,_)=>Attach();
        HandlerChanging+=(_,_)=>Detach();
    }
    private void Attach()
    {
        native=Handler?.PlatformView as global::Android.Views.View;if(native==null)return;
        native.Clickable=true;native.LongClickable=true;native.Focusable=true;native.Click+=Clicked;native.LongClick+=Held;
    }
    private void Detach(){if(native!=null){native.Click-=Clicked;native.LongClick-=Held;native=null;}}
    private async void Clicked(object? sender,EventArgs e)
    {if(DateTimeOffset.UtcNow<ignoreClickUntil)return;if(BindingContext is MediaRow item){if(item.IsSelecting)item.IsSelected=!item.IsSelected;else await open(item);}}
    private async void Held(object? sender,global::Android.Views.View.LongClickEventArgs e)
    {e.Handled=true;ignoreClickUntil=DateTimeOffset.UtcNow.AddMilliseconds(800);if(BindingContext is MediaRow item)await menu(item);}
}
public class ListPage : BasePage
{
    protected readonly RowCollection Rows=[];
    protected readonly CollectionView List;
    protected readonly Grid ListContent;
    protected readonly VerticalStackLayout Header;
    protected readonly VerticalStackLayout Footer=new(){Padding=Ui.Inset(24,4,24,8),Spacing=Ui.S(12)};
    private bool footerPinned;
    private View? skeleton;
    private bool loading;
    private int lastVisible=-1;
    protected override bool RefreshOnAppear=>false;
    public ListPage(string title,bool root=false,bool showMini=true):base(title,root,showMini)
    {
        Header=Stack();Header.Padding=Ui.Inset(24,8,24,8);
        List=new CollectionView{SelectionMode=SelectionMode.None,ItemsSource=Rows,Header=Header,ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem};
        List.HandlerChanged+=(_,_)=>{if(List.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView native){native.SetItemAnimator(null);native.SetItemViewCacheSize(12);}};
        List.Scrolled+=(_,e)=>{lastVisible=e.LastVisibleItemIndex;UpdateFooter();};
        Rows.CollectionChanged+=(_,_)=>{UpdateFooter();UpdateEmpty();};
        List.SetDynamicResource(BackgroundColorProperty,"Surface");
        List.ItemTemplate=new DataTemplate(()=>new MediaRowView(row=>Run(()=>OnOpen(row)),row=>Run(()=>OnMenu(row),false)));
        SetEmptyView(Ui.Empty("暂无内容","内容会在这里显示。"));
        // Keep paging controls outside the recycled list: Android can reuse the
        // footer as its header after clearing and repopulating an empty source.
        ListContent=new Grid{RowDefinitions=[new(GridLength.Star),new(GridLength.Auto)]};
        ListContent.Add(List);ListContent.Add(Footer,0,1);Body.Content=ListContent;
    }
    private View? empty;
    protected void SetEmptyView(View? view)
    {
        if(empty!=null)Header.Remove(empty);empty=view;
        if(IsLoaded)UpdateEmpty();
    }
    private void UpdateEmpty()
    {
        if(empty==null)return;
        if(!Header.Contains(empty))Header.Add(empty);
        empty.IsVisible=Rows.Count==0&&!loading;
    }
    protected override void OnAppearing(){base.OnAppearing();UpdateEmpty();}
    protected override void AccountInvalidated(){Rows.Clear();}
    protected override void LoadingChanged(bool value)
    {
        loading=value&&Rows.Count==0;
        if(loading){skeleton??=LoadingSkeleton.Rows();if(!Header.Contains(skeleton))Header.Add(skeleton);}
        else if(skeleton!=null)Header.Remove(skeleton);
        UpdateEmpty();
    }
    protected virtual Task OnOpen(MediaRow row)=>Task.CompletedTask;
    protected virtual Task OnMenu(MediaRow row)=>OnOpen(row);
    protected void Replace(IEnumerable<MediaRow> items)=>Rows.ReplaceWith(items);
    protected void PinFooter(bool pinned){footerPinned=pinned;UpdateFooter();}
    private void UpdateFooter()=>Footer.IsVisible=footerPinned||Rows.Count<10||lastVisible>=Rows.Count-3;
    protected void ScrollTop(){if(IsRevalidation)return;Dispatcher.Dispatch(()=>{if(List.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView native)native.ScrollToPosition(0);});}
}


