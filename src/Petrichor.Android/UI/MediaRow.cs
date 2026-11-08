using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls.Shapes;
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
// Material 3 two-line list item with a 56dp image, used by every track, playlist and album list.
public sealed class MediaRowView : ContentView
{
    private readonly Label title=Ui.Line("",TypeRole.BodyLarge),subtitle=Ui.Line("",TypeRole.BodyMedium,"OnSurfaceVariant");
    private readonly Image cover;
    private readonly Border highlight;
    public MediaRowView(Func<MediaRow,Task> open,Func<MediaRow,Task> menu,bool compact=false,double inset=Md.Margin)
    {
        var art=compact?48:56;cover=Ui.Cover(null,art);
        var check=new CheckBox{WidthRequest=48,HeightRequest=48};
        check.SetBinding(CheckBox.IsCheckedProperty,new Binding(nameof(MediaRow.IsSelected),BindingMode.TwoWay));
        check.SetBinding(IsVisibleProperty,nameof(MediaRow.IsSelecting));
        cover.SetBinding(IsVisibleProperty,nameof(MediaRow.ShowCover));
        var index=Ui.Line("",TypeRole.TitleSmall,"OnSurfaceVariant");index.WidthRequest=32;index.HorizontalTextAlignment=TextAlignment.Center;
        index.SetBinding(Label.TextProperty,nameof(MediaRow.Index));index.SetBinding(IsVisibleProperty,nameof(MediaRow.ShowIndex));
        var leading=new Grid{VerticalOptions=LayoutOptions.Center};leading.Add(cover);leading.Add(check);leading.Add(index);
        var info=new VerticalStackLayout{Spacing=0,VerticalOptions=LayoutOptions.Center,InputTransparent=true};
        subtitle.SetBinding(Label.TextProperty,nameof(MediaRow.Subtitle));info.Add(title);info.Add(subtitle);
        var more=Ui.Icon("more",async()=>{if(BindingContext is MediaRow item)await menu(item);},"更多操作");
        var rowHeight=compact?64:Ui.MediaRowHeight;HeightRequest=rowHeight;
        var row=new Grid{ColumnDefinitions=[new(GridLength.Auto),new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=16,Padding=new Thickness(inset,0,4,0)};
        row.Add(leading);row.Add(info,1);row.Add(more,2);
        // Selected-state container for the item that is playing now.
        highlight=new Border{StrokeThickness=0,StrokeShape=new RoundRectangle{CornerRadius=Md.ShapeLarge},Margin=new Thickness(8,2),IsVisible=false,InputTransparent=true};
        highlight.SetDynamicResource(BackgroundColorProperty,"SecondaryContainer");
        var surface=new Grid();surface.Add(highlight);surface.Add(row);Content=surface;
        Pressable.Attach(this,async()=>{if(BindingContext is MediaRow item){if(item.IsSelecting)item.IsSelected=!item.IsSelected;else await open(item);}},async()=>{if(BindingContext is MediaRow item&&!item.IsSelecting)await menu(item);});
        string? coverUrl=null;
        BindingContextChanged+=(_,_)=>{
            if(BindingContext is not MediaRow item)return;
            title.Text=item.Title;subtitle.IsVisible=item.Subtitle.Length>0;
            if(item.ShowCover&&coverUrl!=item.Cover){coverUrl=item.Cover;cover.Source=string.IsNullOrWhiteSpace(item.Cover)?null:Ui.Art(item.Cover,200);}
            highlight.IsVisible=item.IsCurrent;
            title.SetDynamicResource(Label.TextColorProperty,item.IsCurrent?"OnSecondaryContainer":"OnSurface");
            subtitle.SetDynamicResource(Label.TextColorProperty,item.IsCurrent?"OnSecondaryContainer":"OnSurfaceVariant");
            SemanticProperties.SetDescription(this,item.Title+"，"+item.Subtitle);
        };
    }
}
public class ListPage : BasePage
{
    protected readonly RowCollection Rows=[];
    protected readonly CollectionView List;
    protected readonly Grid ListContent;
    protected readonly VerticalStackLayout Header;
    protected readonly VerticalStackLayout Footer=new(){Padding=new Thickness(Md.Margin,4,Md.Margin,8),Spacing=8};
    private bool footerPinned;
    private View? skeleton;
    private bool loading,reveal;
    private int lastVisible=-1;
    protected override bool RefreshOnAppear=>false;
    public ListPage(string title,bool root=false,bool showMini=true):base(title,root,showMini)
    {
        Header=Stack();Header.Padding=new Thickness(Md.Margin,0,Md.Margin,8);
        List=new CollectionView{SelectionMode=SelectionMode.None,ItemsSource=Rows,Header=Header,ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem};
        List.HandlerChanged+=(_,_)=>{if(List.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView native){native.SetItemAnimator(null);native.SetItemViewCacheSize(12);}};
        List.Scrolled+=(_,e)=>{lastVisible=e.LastVisibleItemIndex;UpdateFooter();UpdateScrollHeader(e.VerticalOffset);};
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
    internal override void ScrollToTop(){if(List.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView native){if(PageMotion.Enabled)native.SmoothScrollToPosition(0);else native.ScrollToPosition(0);}}
    protected override void LoadingChanged(bool value)
    {
        loading=value&&Rows.Count==0;
        if(loading){reveal=true;skeleton??=LoadingSkeleton.Rows();if(!Header.Contains(skeleton))Header.Add(skeleton);}
        else if(skeleton!=null)Header.Remove(skeleton);
        UpdateEmpty();
    }
    protected virtual Task OnOpen(MediaRow row)=>Task.CompletedTask;
    protected virtual Task OnMenu(MediaRow row)=>OnOpen(row);
    protected void Replace(IEnumerable<MediaRow> items)
    {
        Rows.ReplaceWith(items);
        // Content that replaces the loading skeleton rises into place once; revalidation does not animate.
        if(reveal&&Rows.Count>0&&!IsRevalidation){reveal=false;Dispatcher.Dispatch(()=>PageMotion.Reveal(List));}
    }
    protected void PinFooter(bool pinned){footerPinned=pinned;UpdateFooter();}
    private void UpdateFooter()=>Footer.IsVisible=footerPinned||Rows.Count<10||lastVisible>=Rows.Count-3;
    protected void ScrollTop(){if(IsRevalidation)return;Dispatcher.Dispatch(()=>{if(List.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView native)native.ScrollToPosition(0);});}
}
