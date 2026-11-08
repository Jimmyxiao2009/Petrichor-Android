using Petrichor.Core;
namespace Petrichor.Mobile.UI;
// Program search result: an outlined card with its own play action and a link to the parent podcast.
public sealed class ProgramRowView : ContentView
{
    public ProgramRowView(Func<MediaRow,Task> open,Func<MediaRow,Task> play)
    {
        var title=Ui.Text("",TypeRole.TitleSmall);title.MaxLines=2;title.LineBreakMode=LineBreakMode.TailTruncation;
        var subtitle=Ui.Line("",TypeRole.BodySmall,"OnSurfaceVariant");
        var cover=Ui.Cover(null,56);
        var head=new Grid{ColumnDefinitions=[new(56),new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=16};
        var info=new VerticalStackLayout{Spacing=4,VerticalOptions=LayoutOptions.Center,InputTransparent=true};info.Add(title);info.Add(subtitle);
        head.Add(cover);head.Add(info,1);head.Add(new IconButton("play",async()=>{if(BindingContext is MediaRow r)await play(r);},"播放节目",IconStyle.Tonal,filled:true),2);
        var parent=Ui.Line("",TypeRole.LabelLarge,"Primary");
        var link=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],Padding=new Thickness(16,0,8,0),MinimumHeightRequest=48};link.Add(parent);link.Add(Ui.Glyph("chevron",20,"Primary"),1);
        Pressable.Attach(link,async()=>{if(BindingContext is MediaRow r)await open(r);},radius:Md.ShapeMedium,description:"查看所属播客");
        var stack=new VerticalStackLayout{Spacing=4};var top=new ContentView{Content=head,Padding=new Thickness(16,12,8,4)};stack.Add(top);stack.Add(link);
        Content=Ui.OutlinedCard(stack,Md.ShapeMedium,0);Margin=new Thickness(Md.Margin,4);
        BindingContextChanged+=(_,_)=>{if(BindingContext is MediaRow row&&row.Value is DjProgram p){title.Text=p.Name;subtitle.Text="节目 · "+Ui.Duration(p.DurationMs);parent.Text="所属播客 · "+(string.IsNullOrEmpty(p.RadioName)?"查看播客":p.RadioName);cover.Source=Ui.Art(p.CoverUrl,200);}};
    }
}
// One stable native item template avoids reusing Android header/footer view types when switching result categories.
public sealed class SearchResultRow : ContentView
{
    public SearchResultRow(Func<MediaRow,Task> open,Func<MediaRow,Task> menu,Func<MediaRow,Task> play)
    {
        BindingContextChanged+=(_,_)=>{
            if(BindingContext is not MediaRow row)return;
            bool program=row.Value is DjProgram;
            if(Content==null || program!=(Content is ProgramRowView)) {
                Content?.Handler?.DisconnectHandler();
                Content=program?new ProgramRowView(open,play):new MediaRowView(open,menu);
            }
            Content.BindingContext=row;
        };
    }
}
