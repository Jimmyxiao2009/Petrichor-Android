using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class SearchPage : ListPage
{
    private readonly SearchField query=new(){Placeholder="歌曲、歌单、歌手、专辑、播客、节目"};
    private static readonly string[] names=["单曲","歌单","歌手","专辑","播客","节目"];
    private readonly TabStrip tabs;
    private readonly Button more;
    private readonly Label summary=Ui.Text("输入关键词，发现想听的声音。",13,"Muted");
    private readonly Label resultTitle=Ui.Text("单曲",22);
    private CancellationTokenSource? searchLife;
    private int selected,offset,generation;
    public SearchPage(string? initial=null,int initialKind=0):base("搜索")
    {
        selected=Math.Clamp(initialKind,0,5);
        query.Text=initial??"";
        query.SearchButtonPressed+=async(_,_)=>await Run(()=>Search(true));Header.Add(Ui.Search(query));
        tabs=new TabStrip(names,index=>Run(async()=>{selected=index;UpdateChips();await Search(true);}),selected);tabs.Margin=new Thickness(0,12,0,0);Header.Add(tabs);
        summary.IsVisible=false;UpdateChips();
        more=Ui.Button("加载更多",()=>Run(()=>Search(false)),"outlined");more.IsVisible=false;Footer.Add(more);
        SetEmptyView(null);
        List.ItemTemplate=new DataTemplate(()=>new SearchResultRow(
            r=>Run(()=>OnOpen(r)),r=>Run(()=>OnMenu(r),false),
            r=>Run(()=>Actions.PlayProgram(this,(DjProgram)r.Value))));
        List.ItemSizingStrategy=selected==5?ItemSizingStrategy.MeasureAllItems:ItemSizingStrategy.MeasureFirstItem;
    }
    protected override async Task LoadAsync(){if(!string.IsNullOrWhiteSpace(query.Text))await Search(true);}
    protected override void OnDisappearing(){searchLife?.Cancel();base.OnDisappearing();}
    private void UpdateChips(){tabs.SetSelected(selected);resultTitle.Text=selected==5?"节目中的「"+(query.Text?.Trim()??"")+"」":names[selected];}

    private async Task Search(bool reset)
    {
        var text=query.Text?.Trim()??"";if(text.Length==0){Rows.Clear();summary.Text="输入关键词，发现想听的声音。";more.IsVisible=false;return;}
        if(text.Length>100)throw new ArgumentException("搜索词最多100个字。");
        searchLife?.Cancel();searchLife?.Dispose();searchLife=CancellationTokenSource.CreateLinkedTokenSource(Token);
        var ct=searchLife.Token;var revision=++generation;var kind=selected;var skip=reset?0:offset;
        UpdateChips();summary.Text="正在搜索…";more.IsVisible=false;
        List.ItemSizingStrategy=kind==5?ItemSizingStrategy.MeasureAllItems:ItemSizingStrategy.MeasureFirstItem;
        if(reset)Rows.Clear();
        try
        {
            IEnumerable<object> values;bool hasMore;var api=AppServices.Api;
            switch(kind)
            {
                case 1:var p=await api.SearchPlaylistsAsync(text,30,skip,ct);values=p.Items;hasMore=p.HasMore;break;
                case 2:var a=await api.SearchArtistsAsync(text,30,skip,ct);values=a.Items;hasMore=a.HasMore;break;
                case 3:var b=await api.SearchAlbumsAsync(text,30,skip,ct);values=b.Items;hasMore=b.HasMore;break;
                case 4:var r=await api.SearchRadiosAsync(text,30,skip,ct);values=r.Items;hasMore=r.HasMore;break;
                case 5:var d=await api.SearchProgramsAsync(text,30,skip,ct);values=d.Items;hasMore=d.HasMore;break;
                default:var t=await api.SearchTracksAsync(text,30,skip,ct);values=t.Items;hasMore=t.HasMore;break;
            }
            ct.ThrowIfCancellationRequested();if(revision!=generation)return;
            var found=values.ToList();var rows=found.Select(Actions.Row);if(reset)Replace(rows);else Rows.AddRange(rows);offset=skip+30;
            more.IsVisible=hasMore&&found.Count>0;
            summary.Text=kind==5?"按节目名称查找，发现它所在的播客。":"已加载 "+Rows.Count+" 条结果";
            SetEmptyView(Ui.Empty("暂时没有找到","试试更简短的关键词，或切换其他分类。"));
            if(reset)ScrollTop();
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
    }
    protected override Task OnOpen(MediaRow row)=>row.Value is Track t?Play(Rows.Where(r=>r.Value is Track).Select(r=>(Track)r.Value),t):Actions.Open(this,row.Value);
    protected override Task OnMenu(MediaRow row)=>row.Value is Track t?Actions.TrackMenu(this,t):OnOpen(row);
}
