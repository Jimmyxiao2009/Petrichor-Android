using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class CommentsPage : ListPage
{
    private readonly Track track;
    private readonly MusicComment? parent;
    private readonly NativeEntry editor=new(){Placeholder="写下这一刻的感受…",IsMultiline=true,FontSize=Ui.F(15),HeightRequest=36,MinimumHeightRequest=0,MaxLength=1000};
    private readonly Picker sort=new(){Title="评论排序",ItemsSource=new[]{"推荐","最热","最新"},SelectedIndex=0};
    private readonly Button more;
    private string? cursor;
    private int pageNo=1;
    private long replyTo;
    private bool loading;
    public CommentsPage(Track track,MusicComment? parent=null):base(parent==null?"评论":"回复",showMini:false)
    {
        this.track=track;this.parent=parent;replyTo=parent?.Id??0;
        Header.Add(Ui.Row(track.Name,track.ArtistText,()=>Play([track],track),"play",AppServices.Offline.Cover(track)));
        if(parent!=null){Header.Add(Ui.Card(Ui.Text(parent.Nickname+"：\n"+parent.Content,14)));editor.Placeholder="回复 "+parent.Nickname+"…";}
        else {var chips=new HorizontalStackLayout{Spacing=Ui.S(8),Margin=Ui.Inset(0,20,0,20)};var choices=new List<Button>();string[] names=["推荐","最热","最新"];for(int i=0;i<names.Length;i++){int index=i;var chip=DesignControls.Chip(names[i],()=>Run(async()=>{sort.SelectedIndex=index;for(int j=0;j<choices.Count;j++)DesignControls.SelectChip(choices[j],j==index);await Fetch(true);}),i==0);choices.Add(chip);chips.Add(chip);}Header.Add(chips);}
        more=Ui.Button("加载更多评论",()=>Run(()=>Fetch(false)),"tonal");Footer.Add(more);
        List.ItemSizingStrategy=ItemSizingStrategy.MeasureAllItems;List.ItemTemplate=new DataTemplate(()=>new CommentView(this));
        Body.Content=null;
        var content=new Grid{RowDefinitions=[new(GridLength.Star),new(GridLength.Auto)]};content.Add(ListContent);
        var compose=new Grid{ColumnDefinitions=[new(GridLength.Star),new(68)],ColumnSpacing=Ui.S(8),Padding=Ui.Inset(16,8)};
        compose.SetDynamicResource(BackgroundColorProperty,"SurfaceContainer");var input=Ui.Card(editor,"Surface",24,Ui.Inset(16,0));input.StrokeThickness=1;input.SetDynamicResource(Border.StrokeProperty,"Outline");compose.Add(input);
        compose.Add(Ui.Button("发送",()=>Run(Send)),1);content.Add(compose,0,1);Body.Content=content;
    }
    protected override Task LoadAsync()=>Fetch(true);
    private async Task Fetch(bool reset)
    {
        if(loading)return;loading=true;sort.IsEnabled=false;
        try
        {
            CommentPage result;
            if(parent!=null)
            {
                long time=reset?-1:long.TryParse(cursor,out var n)?n:-1;
                result=await AppServices.Api.GetCommentRepliesAsync(CommentThread.ForTrack(track),parent.ParentCommentId>0?parent.ParentCommentId:parent.Id,time,20,Token);
            }
            else
            {
                CommentSort[] kinds=[CommentSort.Recommended,CommentSort.Hot,CommentSort.Latest];
                result=await AppServices.Api.GetCommentsAsync(CommentThread.ForTrack(track),kinds[Math.Max(0,sort.SelectedIndex)],reset?1:pageNo,20,reset?null:cursor,Token);
            }
            Token.ThrowIfCancellationRequested();if(reset)pageNo=1;var rows=result.Items.Select(c=>new MediaRow{Title=c.Nickname,Subtitle=c.Content,Value=c});if(reset)Replace(rows);else Rows.AddRange(rows);
            pageNo++;cursor=result.Cursor;more.IsVisible=result.HasMore&&result.Items.Count>0;
        }
        finally{loading=false;sort.IsEnabled=true;}
    }
    private async Task Send()
    {
        if(string.IsNullOrWhiteSpace(editor.Text))throw new ArgumentException("请先输入评论内容。");
        if(!await Actions.RequireAccount(this))return;
        await AppServices.Api.PostCommentAsync(CommentThread.ForTrack(track),editor.Text,replyTo,Token);
        editor.Text="";replyTo=parent?.Id??0;editor.Placeholder=parent==null?"写下这一刻的感受…":"回复 "+parent.Nickname+"…";
        await Fetch(true);ShowMessage("评论已发送。");
    }
    internal Task Like(MusicComment comment,Button button)=>Run(async()=>{
        if(!await Actions.RequireAccount(this))return;
        await AppServices.Api.SetCommentLikedAsync(CommentThread.ForTrack(track),comment.Id,!comment.Liked,Token);
        comment.Liked=!comment.Liked;comment.LikeCount+=comment.Liked?1:-1;button.Text=(comment.Liked?"已赞 ":"赞 ")+Math.Max(0,comment.LikeCount);
    });
    internal Task Reply(MusicComment comment){replyTo=comment.Id;editor.Placeholder="回复 "+comment.Nickname+"…";editor.Focus();return Task.CompletedTask;}
    internal Task Replies(MusicComment comment)=>Open(new CommentsPage(track,comment));
}
internal sealed class CommentView : ContentView
{
    private MusicComment? comment;
    private readonly Label nickname=Ui.Text("",12),date=Ui.Text("",12,"Muted"),words=Ui.Text("",14),quote=Ui.Text("",12,"Muted");
    private readonly Image avatar=Ui.Cover("",32);
    private readonly Button liked,replies;
    public CommentView(CommentsPage page)
    {
        var stack=new VerticalStackLayout{Spacing=Ui.S(8),Padding=Ui.Inset(20,12)};
        var meta=new Grid{ColumnDefinitions=[new(Ui.S(32)),new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=Ui.S(10)};meta.Add(avatar);
        var caption=new VerticalStackLayout{Spacing=Ui.S(4)};nickname.MaxLines=1;nickname.LineBreakMode=LineBreakMode.TailTruncation;caption.Add(nickname);caption.Add(date);meta.Add(caption,1);
        liked=Ui.Button("",()=>comment is{} c?page.Like(c,liked!):Task.CompletedTask,"text","thumb");liked.FontSize=Ui.F(12);liked.Padding=0;meta.Add(liked,2);stack.Add(meta);
        quote.Margin=Ui.Inset(42,0,0,0);quote.MaxLines=3;quote.LineBreakMode=LineBreakMode.TailTruncation;stack.Add(quote);
        words.Margin=Ui.Inset(42,0,0,0);stack.Add(words);
        var actions=new HorizontalStackLayout{Spacing=Ui.S(8),Margin=Ui.Inset(42,0,0,0)};
        var reply=Ui.Button("回复",()=>comment is{} c?page.Reply(c):Task.CompletedTask,"text");reply.HeightRequest=28;reply.MinimumHeightRequest=28;reply.FontSize=Ui.F(12);reply.Padding=0;actions.Add(reply);
        replies=Ui.Button("",()=>comment is{} c?page.Replies(c):Task.CompletedTask,"text");replies.HeightRequest=28;replies.MinimumHeightRequest=28;replies.FontSize=Ui.F(12);replies.Padding=0;actions.Add(replies);stack.Add(actions);stack.Add(Ui.Divider());Content=stack;
        BindingContextChanged+=(_,_)=>{
            if(BindingContext is not MediaRow{Value:MusicComment c})return;
            comment=c;nickname.Text=c.Nickname;date.Text=c.DateText+(string.IsNullOrEmpty(c.Location)?"":" · "+c.Location);
            avatar.Source=Ui.Art(c.User?.AvatarUrl,100);liked.Text=(c.Liked?"已赞 ":"")+c.LikeCount;
            quote.Text="回复 "+c.RepliedNickname+"："+c.RepliedContent;quote.IsVisible=!string.IsNullOrEmpty(c.RepliedNickname);
            words.Text=c.Content;replies.Text="查看"+c.ReplyCount+"条回复";replies.IsVisible=c.ReplyCount>0;
        };
    }
}
