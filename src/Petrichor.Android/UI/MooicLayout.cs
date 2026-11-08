// MAUI adaptation of Mooic 3.0.1 UI proportions and composition.
// Source: kid-depress/Mooic, tag 3.0.1, GPL-3.0. See Resources/Raw/Mooic-LICENSE.txt.
// Actions and data remain Petrichor's AppServices / Core implementation.
using Petrichor.Core;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

internal static class MooicLayout
{
    // Horizontal carousels scroll edge to edge, but their first item lines up with the page margin.
    private static void Inset(CollectionView view)
    {
        view.HandlerChanged+=(_,_)=>{
            if(view.Handler?.PlatformView is not AndroidX.RecyclerView.Widget.RecyclerView native)return;
            var margin=(int)Math.Round(Md.Margin*native.Resources!.DisplayMetrics!.Density);
            native.SetPadding(margin,0,margin,0);native.SetClipToPadding(false);native.OverScrollMode=Android.Views.OverScrollMode.Never;
        };
    }
    public static View Songs(IReadOnlyList<Track> tracks,BasePage page)
    {
        var width=Math.Min(340,DeviceDisplay.Current.MainDisplayInfo.Width/DeviceDisplay.Current.MainDisplayInfo.Density-64);
        var list=new CollectionView{HeightRequest=4*64,SelectionMode=SelectionMode.None,ItemsSource=tracks.Select(t=>new MediaRow{Title=t.Name,Subtitle=t.ArtistText,Cover=AppServices.Offline.Cover(t),Value=t}).ToArray(),ItemsLayout=new GridItemsLayout(4,ItemsLayoutOrientation.Horizontal){HorizontalItemSpacing=8},ItemSizingStrategy=ItemSizingStrategy.MeasureFirstItem,HorizontalScrollBarVisibility=ScrollBarVisibility.Never};
        list.ItemTemplate=new DataTemplate(()=>new MediaRowView(row=>page.Play(tracks,(Track)row.Value),row=>page.Run(()=>Actions.TrackMenu(page,(Track)row.Value),false),true,0){WidthRequest=width});
        Inset(list);return list;
    }
    public static View Playlists(IReadOnlyList<Playlist> lists,BasePage page)
    {
        var view=new CollectionView{HeightRequest=196,SelectionMode=SelectionMode.None,ItemsSource=lists,ItemsLayout=new LinearItemsLayout(ItemsLayoutOrientation.Horizontal){ItemSpacing=12},HorizontalScrollBarVisibility=ScrollBarVisibility.Never};
        view.ItemTemplate=new DataTemplate(()=>new PlaylistCard(page));Inset(view);return view;
    }
    private sealed class PlaylistCard:ContentView
    {
        private Playlist? playlist;
        public PlaylistCard(BasePage page)
        {
            WidthRequest=136;HeightRequest=196;
            var cover=Ui.Cover(null,136,Md.ShapeLarge);
            var title=Ui.Text("",TypeRole.TitleSmall);title.MaxLines=2;title.LineBreakMode=LineBreakMode.TailTruncation;
            var body=new VerticalStackLayout{Spacing=8,InputTransparent=true};body.Add(cover);body.Add(title);Content=body;
            Pressable.Attach(this,async()=>{if(playlist!=null)await page.Open(new PlaylistPage(playlist.Id));},radius:Md.ShapeLarge);
            BindingContextChanged+=(_,_)=>{if(BindingContext is Playlist value){playlist=value;cover.Source=Ui.Art(value.CoverUrl,300);title.Text=value.Name;SemanticProperties.SetDescription(this,value.Name);}};
        }
    }
}
