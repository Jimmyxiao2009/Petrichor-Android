using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.Pages;

public sealed class CommentsPage : ListPage
{
    private readonly Track track;
    private readonly MusicComment? parent;
    private readonly NativeEntry editor=new(){Placeholder="写下这一刻的感受…",IsMultiline=true,FontSize=16,HeightRequest=40,MinimumHeightRequest=0,MaxLength=1000};
    private readonly Button more;
    private readonly IconButton send;
    private int sortIndex;
    private string? cursor;
    private int pageNo=1;
    private long replyTo;
    private bool loading;
    internal override string BottomKey=>"SurfaceContainer";
    public CommentsPage(Track track,MusicComment? parent=null):base(parent==null?"评论":"回复",showMini:false)
    {
        this.track=track;this.parent=parent;replyTo=parent?.Id??0;
        var song=Ui.ListItem(track.Name,track.ArtistText,()=>Play([track],track),cover:AppServices.Offline.Cover(track),trailing:Ui.Glyph("play",24,"Primary",true));song.Margin=new Thickness(-Md.Margin,0);Header.Add(song);
        if(parent!=null)
        {
            var quote=new VerticalStackLayout{Spacing=4};quote.Add(Ui.Line(parent.Nickname,TypeRole.TitleSmall));quote.Add(Ui.Text(parent.Content,TypeRole.BodyMedium));
            var card=Ui.Card(quote,"SurfaceContainerHigh",Md.ShapeMedium);card.Margin=new Thickness(0,8);Header.Add(card);editor.Placeholder="回复 "+parent.Nickname+"…";
        }
        else
        {
            var choices=new List<FilterChip>();string[] names=["推荐","最热","最新"];var row=new HorizontalStackLayout{Spacing=8,Padding=new Thickness(0,8)};
            for(int i=0;i<names.Length;i++){int index=i;var chip=DesignControls.Chip(names[i],()=>Run(async()=>{if(loading)return;sortIndex=index;for(int j=0;j<choices.Count;j++)choices[j].IsSelected=j==index;await Fetch(true);}),i==0);choices.Add(chip);row.Add(chip);}
            Header.Add(row);
        }
        more=Ui.Button("加载更多评论",()=>Run(()=>Fetch(false)),"tonal");more.HorizontalOptions=LayoutOptions.Center;Footer.Add(more);
        List.ItemSizingStrategy=ItemSizingStrategy.MeasureAllItems;List.ItemTemplate=new DataTemplate(()=>new CommentView(this));
        SetEmptyView(Ui.Empty("还没有评论","成为第一个写下感受的人。","comments"));
        Body.Content=null;
        var content=new Grid{RowDefinitions=[new(GridLength.Star),new(GridLength.Auto)]};content.Add(ListContent);
        // Composer: a filled pill field with a send icon button that is enabled only with text.
        var compose=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto)],ColumnSpacing=8,Padding=new Thickness(Md.Margin,8,8,8)};
        compose.SetDynamicResource(BackgroundColorProperty,"SurfaceContainer");
        var input=Ui.Card(editor,"SurfaceContainerHighest",24,new Thickness(16,4));input.MinimumHeightRequest=48;compose.Add(input);
        send=new IconButton("send",()=>Run(Send),"发送评论",IconStyle.Filled,24,40,48,true){IsEnabled=false};compose.Add(send,1);
        editor.TextChanged+=(_,e)=>send.IsEnabled=!string.IsNullOrWhiteSpace(e.NewTextValue);
        content.Add(compose,0,1);Body.Content=content;
    }
    protected override Task LoadAsync()=>Fetch(true);
    private async Task Fetch(bool reset)
    {
        if(loading)return;loading=true;
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
                result=await AppServices.Api.GetCommentsAsync(CommentThread.ForTrack(track),kinds[Math.Clamp(sortIndex,0,2)],reset?1:pageNo,20,reset?null:cursor,Token);
            }
            Token.ThrowIfCancellationRequested();if(reset)pageNo=1;var rows=result.Items.Select(c=>new MediaRow{Title=c.Nickname,Subtitle=c.Content,Value=c});if(reset){Replace(rows);ScrollTop();}else Rows.AddRange(rows);
            pageNo++;cursor=result.Cursor;more.IsVisible=result.HasMore&&result.Items.Count>0;
        }
        finally{loading=false;}
    }
    private async Task Send()
    {
        if(string.IsNullOrWhiteSpace(editor.Text))throw new ArgumentException("请先输入评论内容。");
        if(!await Actions.RequireAccount(this))return;
        await AppServices.Api.PostCommentAsync(CommentThread.ForTrack(track),editor.Text,replyTo,Token);
        editor.Text="";replyTo=parent?.Id??0;editor.Placeholder=parent==null?"写下这一刻的感受…":"回复 "+parent.Nickname+"…";
        await Fetch(true);ShowMessage("评论已发送。");
    }
    internal Task Like(MusicComment comment,Action<bool,int> render)=>Run(async()=>{
        if(!await Actions.RequireAccount(this))return;
        await AppServices.Api.SetCommentLikedAsync(CommentThread.ForTrack(track),comment.Id,!comment.Liked,Token);
        comment.Liked=!comment.Liked;comment.LikeCount+=comment.Liked?1:-1;render(comment.Liked,Math.Max(0,comment.LikeCount));
    });
    internal Task Reply(MusicComment comment){replyTo=comment.Id;editor.Placeholder="回复 "+comment.Nickname+"…";editor.Focus();return Task.CompletedTask;}
    internal Task Replies(MusicComment comment)=>Open(new CommentsPage(track,comment));
}
internal sealed class CommentView : ContentView
{
    private MusicComment? comment;
    private readonly Label nickname=Ui.Line("",TypeRole.TitleSmall),date=Ui.Line("",TypeRole.BodySmall,"OnSurfaceVariant"),words=Ui.Text("",TypeRole.BodyMedium),quote=Ui.Text("",TypeRole.BodySmall,"OnSurfaceVariant"),likes=Ui.Line("",TypeRole.LabelMedium,"OnSurfaceVariant");
    private readonly Image avatar=Ui.Avatar(null,40);
    private readonly IconButton liked;
    private readonly Button replies;
    public CommentView(CommentsPage page)
    {
        var grid=new Grid{ColumnDefinitions=[new(40),new(GridLength.Star)],ColumnSpacing=16,Padding=new Thickness(Md.Margin,12,8,4)};grid.Add(avatar);avatar.VerticalOptions=LayoutOptions.Start;
        var body=new VerticalStackLayout{Spacing=4};
        var meta=new Grid{ColumnDefinitions=[new(GridLength.Star),new(GridLength.Auto),new(GridLength.Auto)]};
        var caption=new VerticalStackLayout{Spacing=0,VerticalOptions=LayoutOptions.Center};caption.Add(nickname);caption.Add(date);meta.Add(caption);
        void Render(bool on,int count){liked!.SetIcon("thumb",on,true);liked.Selected=on;likes.Text=count>0?count.ToString():"";likes.SetDynamicResource(Label.TextColorProperty,on?"Primary":"OnSurfaceVariant");}
        liked=new IconButton("thumb",async()=>{if(comment is{} c)await page.Like(c,Render);},"点赞",iconSize:20){Toggle=true};meta.Add(likes,1);meta.Add(liked,2);
        body.Add(meta);
        quote.MaxLines=3;quote.LineBreakMode=LineBreakMode.TailTruncation;var quoted=Ui.Card(quote,"SurfaceContainerHigh",Md.ShapeSmall,new Thickness(12,8));body.Add(quoted);
        body.Add(words);
        var actions=new HorizontalStackLayout{Spacing=0,Margin=new Thickness(-12,0,0,0)};
        actions.Add(Ui.Button("回复",()=>comment is{} c?page.Reply(c):Task.CompletedTask,"text"));
        replies=Ui.Button("",()=>comment is{} c?page.Replies(c):Task.CompletedTask,"text");actions.Add(replies);body.Add(actions);
        grid.Add(body,1);
        var stack=new VerticalStackLayout{Spacing=0};stack.Add(grid);var divider=Ui.Divider();divider.Margin=new Thickness(72,0,0,0);stack.Add(divider);Content=stack;
        BindingContextChanged+=(_,_)=>{
            if(BindingContext is not MediaRow{Value:MusicComment c})return;
            comment=c;nickname.Text=c.Nickname;date.Text=c.DateText+(string.IsNullOrEmpty(c.Location)?"":" · "+c.Location);
            avatar.Source=Ui.Art(c.User?.AvatarUrl,120);liked.SetIcon("thumb",c.Liked);liked.Selected=c.Liked;likes.Text=c.LikeCount>0?c.LikeCount.ToString():"";likes.SetDynamicResource(Label.TextColorProperty,c.Liked?"Primary":"OnSurfaceVariant");
            quote.Text="回复 "+c.RepliedNickname+"："+c.RepliedContent;quoted.IsVisible=!string.IsNullOrEmpty(c.RepliedNickname);
            words.Text=c.Content;replies.Text="查看 "+c.ReplyCount+" 条回复";replies.IsVisible=c.ReplyCount>0;
        };
    }
}
