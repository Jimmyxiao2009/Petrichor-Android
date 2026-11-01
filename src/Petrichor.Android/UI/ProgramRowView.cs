using Petrichor.Core;
namespace Petrichor.Mobile.UI;
public sealed class ProgramRowView : ContentView
{
    public ProgramRowView(Func<MediaRow,Task> open,Func<MediaRow,Task> play)
    {
        var title=Ui.Text("",15);title.MaxLines=2;
        var subtitle=Ui.Text("",12,"Muted");
        var parent=Ui.Button("",async()=>{if(BindingContext is MediaRow r)await open(r);},"text");
        parent.FontSize=Ui.F(12);parent.Padding=0;parent.HorizontalOptions=LayoutOptions.Fill;
        parent.HandlerChanged+=(_,_)=>{if(parent.Handler?.PlatformView is global::Android.Widget.Button native)native.Gravity=global::Android.Views.GravityFlags.CenterVertical|global::Android.Views.GravityFlags.Left;};
        var cover=Ui.Cover("",52);
        var head=new Grid{ColumnDefinitions=[new(Ui.S(52)),new(GridLength.Star),new(Ui.S(48))],ColumnSpacing=Ui.S(12)};
        var info=new VerticalStackLayout{Spacing=Ui.S(6),VerticalOptions=LayoutOptions.Center};info.Add(title);info.Add(subtitle);
        head.Add(cover);head.Add(info,1);head.Add(Ui.Icon("play",async()=>{if(BindingContext is MediaRow r)await play(r);},"播放节目"),2);
        var stack=new VerticalStackLayout{Spacing=Ui.S(8)};stack.Add(head);stack.Add(parent);
        Content=Ui.Card(stack,radius:16,padding:Ui.Inset(16,12));Margin=Ui.Inset(24,6);
        BindingContextChanged+=(_,_)=>{if(BindingContext is MediaRow row&&row.Value is DjProgram p){title.Text=p.Name;subtitle.Text="节目 · "+Ui.Duration(p.DurationMs);parent.Text="所属播客 · "+(string.IsNullOrEmpty(p.RadioName)?"查看播客":p.RadioName)+"     查看 ›";cover.Source=Ui.Art(p.CoverUrl,200);}};
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
