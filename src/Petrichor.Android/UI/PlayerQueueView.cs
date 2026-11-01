using Petrichor.Core;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

internal sealed class PlayerQueueView:ContentView
{
    private readonly RowCollection rows=[];
    private readonly CollectionView list;
    private readonly Label count=MooicLayout.Text("",14,"Muted");
    private Track[] rendered=[];
    private int index=-2;
    public PlayerQueueView(BasePage page)
    {
        var heading=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Padding=new Thickness(16,8,16,12)};heading.Add(MooicLayout.Text("播放队列",20));heading.Add(count,1);
        list=new CollectionView{SelectionMode=SelectionMode.None,ItemsSource=rows,ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,EmptyView=Ui.Empty("队列为空","选择歌曲后会在这里显示。")};
        list.ItemTemplate=new DataTemplate(()=>new MediaRowView(row=>page.Play(AppServices.Playback.Queue.Tracks.ToArray(),(Track)row.Value),row=>page.Run(()=>Actions.TrackMenu(page,(Track)row.Value),false)));
        list.HandlerChanged+=(_,_)=>{if(list.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView native){native.SetItemAnimator(null);native.SetItemViewCacheSize(10);}};
        var layout=new Grid{SafeAreaEdges=SafeAreaEdges.None,RowDefinitions=[new(GridLength.Auto),new(GridLength.Star)]};layout.Add(heading);layout.Add(list,0,1);Content=layout;
    }
    public void Refresh(bool scrollCurrent=false)
    {
        var q=AppServices.Playback.Queue;var tracks=q.Tracks;
        if(index!=q.Index||rendered.Length!=tracks.Count||!rendered.SequenceEqual(tracks)){
            rendered=tracks.ToArray();index=q.Index;count.Text=tracks.Count+" 首";
            rows.ReplaceWith(tracks.Select((t,i)=>new MediaRow{Title=t.Name,Subtitle=t.ArtistText,Cover=AppServices.Offline.Cover(t),Value=t,IsCurrent=i==q.Index}));
        }
        if(scrollCurrent&&q.Index>=0&&q.Index<rows.Count)Dispatcher.Dispatch(()=>list.ScrollTo(q.Index,position:ScrollToPosition.Center,animate:false));
    }
}
