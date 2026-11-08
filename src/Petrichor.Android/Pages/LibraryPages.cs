using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class LibraryPage : ListPage
{
    private int offset;
    private bool guestRendered;
    private readonly Button more;
    protected override bool RefreshOnAppear=>false;
    protected override void AccountInvalidated(){guestRendered=false;Header.Clear();base.AccountInvalidated();}
    public LibraryPage():base("我的",true)
    {
        more=Ui.Button("加载更多歌单",()=>Run(()=>Fetch(false)),"outlined");more.HorizontalOptions=LayoutOptions.Center;Footer.Add(more);
        List.ItemSizingStrategy=ItemSizingStrategy.MeasureAllItems;List.ItemTemplate=new DataTemplate(()=>new LibraryItem(this));
    }
    protected override async Task LoadAsync()
    {
        if(!AppServices.Api.IsLoggedIn&&guestRendered)return;
        Header.Clear();
        if(!AppServices.Api.IsLoggedIn)
        {
            var stack=new VerticalStackLayout{Spacing=12,Padding=new Thickness(32,24),VerticalOptions=LayoutOptions.Center};
            var bubble=Ui.Card(Ui.Glyph("account",48,"OnPrimaryContainer"),"PrimaryContainer",48,0);bubble.WidthRequest=96;bubble.HeightRequest=96;bubble.HorizontalOptions=LayoutOptions.Center;bubble.Margin=new Thickness(0,0,0,8);stack.Add(bubble);
            var heading=Ui.Text("登录网易云音乐",TypeRole.HeadlineSmall);heading.HorizontalTextAlignment=TextAlignment.Center;stack.Add(heading);
            var hint=Ui.Text("同步你的歌单、收藏和每日推荐。",TypeRole.BodyMedium,"OnSurfaceVariant");hint.HorizontalTextAlignment=TextAlignment.Center;stack.Add(hint);
            var login=Ui.Button("登录",()=>Open(new LoginPage()),"filled","login");login.HorizontalOptions=LayoutOptions.Center;login.Margin=new Thickness(0,8,0,0);stack.Add(login);
            var local=new HorizontalStackLayout{Spacing=8,HorizontalOptions=LayoutOptions.Center,Margin=new Thickness(0,16,0,0)};
            local.Add(Ui.Button("本地与下载",()=>Open(new DownloadsPage()),"outlined","download"));local.Add(Ui.Button("最近播放",()=>Open(new CatalogPage(CatalogKind.Recent)),"outlined","history"));stack.Add(local);
            Body.Content=new Grid{Children={stack}};
            Rows.Clear();more.IsVisible=false;SetEmptyView(null);guestRendered=true;return;
        }
        Body.Content=ListContent;guestRendered=false;var user=AppServices.Api.CurrentUser!;
        var account=new Grid{ColumnDefinitions=[new(56),new(GridLength.Star)],ColumnSpacing=16,Padding=new Thickness(0,8,0,8)};
        account.Add(Ui.Avatar(user.AvatarUrl,56));var profile=new VerticalStackLayout{Spacing=2,VerticalOptions=LayoutOptions.Center};profile.Add(Ui.Line(user.Nickname,TypeRole.TitleLarge));profile.Add(Ui.Line("网易云音乐账号",TypeRole.BodyMedium,"OnSurfaceVariant"));account.Add(profile,1);Header.Add(account);
        Header.Add(DesignControls.Shortcuts(("我喜欢","like",()=>Open(new CatalogPage(CatalogKind.Liked))),("本地与下载","download",()=>Open(new DownloadsPage())),("最近播放","history",()=>Open(new CatalogPage(CatalogKind.Recent)))));
        Header.Add(Ui.Section("我的歌单",()=>Run(()=>Actions.NewPlaylist(this,refresh:LoadAsync)),actionIcon:"add"));
        SetEmptyView(Ui.Empty("还没有歌单","新建一个歌单，留住喜欢的声音。","playlist"));await Fetch(true);
    }
    private async Task Fetch(bool reset){var skip=reset?0:offset;var p=await AppServices.Api.GetMyPlaylistsAsync(100,skip,Token);Token.ThrowIfCancellationRequested();var rows=p.Select(Actions.Row).Append(new MediaRow{Title="我的收藏",Value=new CollectionsEntry()});if(reset)Replace(rows);else{if(Rows.LastOrDefault()?.Value is CollectionsEntry)Rows.RemoveAt(Rows.Count-1);Rows.AddRange(rows);}offset=skip+100;more.IsVisible=p.Count==100;}
    protected override Task OnOpen(MediaRow row)=>Actions.Open(this,row.Value);
    private sealed class CollectionsEntry;
    private sealed class LibraryItem : ContentView
    {
        public LibraryItem(LibraryPage page)
        {
            BindingContextChanged+=(_,_)=>{
                if(BindingContext is not MediaRow row)return;
                if(row.Value is CollectionsEntry)
                {
                    var stack=new VerticalStackLayout{Spacing=0,Padding=new Thickness(0,0,0,24)};var section=Ui.Section("我的收藏");section.Margin=new Thickness(Md.Margin,16,Md.Margin,4);stack.Add(section);
                    stack.Add(Ui.ListItem("订阅的播客","查看你订阅的播客与节目",()=>page.Open(new CatalogPage(CatalogKind.SubscribedRadios)),"podcasts"));
                    stack.Add(Ui.ListItem("收藏的歌手与专辑","按歌手或专辑浏览",()=>page.Open(new CatalogPage(CatalogKind.SavedArtists)),"album"));
                    Content=stack;
                }
                else{if(Content is not MediaRowView)Content=new MediaRowView(r=>page.Run(()=>page.OnOpen(r)),r=>page.Run(()=>page.OnOpen(r)));Content.BindingContext=row;}
            };
        }
    }
}
