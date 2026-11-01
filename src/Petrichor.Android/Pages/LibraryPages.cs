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
    public LibraryPage():base("我的",true){more=Ui.Button("加载更多歌单",()=>Run(()=>Fetch(false)),"outlined");Footer.Add(more);List.ItemSizingStrategy=ItemSizingStrategy.MeasureAllItems;List.ItemTemplate=new DataTemplate(()=>new LibraryItem(this));}
    protected override async Task LoadAsync()
    {
        if(!AppServices.Api.IsLoggedIn&&guestRendered)return;
        Header.Clear();
        if(!AppServices.Api.IsLoggedIn)
        {
            var center=new Grid{RowDefinitions=[new(GridLength.Star),new(GridLength.Auto)]};
            var prompt=new VerticalStackLayout{Spacing=16,HorizontalOptions=LayoutOptions.Center,VerticalOptions=LayoutOptions.Center};
            var glyph=Ui.Glyph("login",48,"Primary");glyph.FontSize=48;prompt.Add(glyph);prompt.Add(MooicLayout.Text("点击登录",16));
            var card=Ui.Card(prompt,"SurfaceContainer",28,new Thickness(32));card.HorizontalOptions=LayoutOptions.Center;card.VerticalOptions=LayoutOptions.Center;card.WidthRequest=148;card.HeightRequest=156;
            var tap=new TapGestureRecognizer();tap.Tapped+=async(_,_)=>await Open(new LoginPage());card.GestureRecognizers.Add(tap);SemanticProperties.SetDescription(card,"登录网易云音乐");center.Add(card);
            var local=new HorizontalStackLayout{Spacing=12,HorizontalOptions=LayoutOptions.Center,Padding=new Thickness(12,12,12,16)};
            local.Add(Ui.Button("本地与下载",()=>Open(new DownloadsPage()),"text","download"));local.Add(Ui.Button("最近播放",()=>Open(new CatalogPage(CatalogKind.Recent)),"text","history"));center.Add(local,0,1);
            Body.Content=center;
            Rows.Clear();more.IsVisible=false;SetEmptyView(null);guestRendered=true;return;
        }
        Body.Content=ListContent;guestRendered=false;var user=AppServices.Api.CurrentUser!;
        var account=new Grid{ColumnDefinitions=[new(Ui.S(56)),new(GridLength.Star),new(Ui.S(48))],ColumnSpacing=Ui.S(16),Padding=Ui.Inset(0,0,0,12)};
        account.Add(Ui.Cover(user.AvatarUrl,56));var profile=new VerticalStackLayout{Spacing=Ui.S(5),VerticalOptions=LayoutOptions.Center};profile.Add(Ui.Text(user.Nickname,22));account.Add(profile,1);account.Add(Ui.Icon("settings",()=>Open(new SettingsPage()),"设置"),2);Header.Add(account);
        Header.Add(DesignControls.Shortcuts(("我喜欢","like",()=>Open(new CatalogPage(CatalogKind.Liked))),("本地与下载","download",()=>Open(new DownloadsPage())),("最近播放","history",()=>Open(new CatalogPage(CatalogKind.Recent)))));
        Header.Add(MooicLayout.Section("我的歌单",()=>Run(()=>Actions.NewPlaylist(this,refresh:LoadAsync))));
        SetEmptyView(Ui.Empty("还没有歌单","新建一个歌单，留住喜欢的声音。"));await Fetch(true);
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
                if(row.Value is CollectionsEntry){var stack=new VerticalStackLayout{Spacing=0,Padding=Ui.Inset(24,0,24,24)};stack.Add(Ui.Section("我的收藏"));var links=new HorizontalStackLayout{Spacing=Ui.S(8)};links.Add(Ui.Button("订阅播客",()=>page.Open(new CatalogPage(CatalogKind.SubscribedRadios)),"tonal","podcasts"));links.Add(Ui.Button("歌手与专辑",()=>page.Open(new CatalogPage(CatalogKind.SavedArtists)),"tonal","album"));stack.Add(links);Content=stack;}
                else{if(Content is not MediaRowView)Content=new MediaRowView(r=>page.Run(()=>page.OnOpen(r)),r=>page.Run(()=>page.OnOpen(r)));Content.BindingContext=row;}
            };
        }
    }
}
public sealed class LibraryAccountPage : BasePage
{
    protected override bool RefreshOnAppear=>true;
    public LibraryAccountPage():base("我的账号"){}
    protected override Task LoadAsync()
    {
        var stack=Stack();var user=AppServices.Api.CurrentUser;stack.Add(Ui.Heading(user?.Nickname??"欢迎来到 Petrichor",user==null?"登录后同步歌单与收藏。":"网易云音乐账号"));
        if(user!=null){var avatar=Ui.Cover(user.AvatarUrl,96);stack.Add(avatar);}
        stack.Add(Ui.Button(user==null?"登录网易云音乐":"打开我的音乐",()=>user==null?Open(new LoginPage()):Ui.Tab("library")));
        stack.Add(Ui.Button("设置",()=>Open(new SettingsPage()),"tonal"));Body.Content=Scroll(stack);return Task.CompletedTask;
    }
}
