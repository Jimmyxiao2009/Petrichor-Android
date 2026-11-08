using Petrichor.Core;
using Petrichor.Mobile.UI;
using Petrichor.Mobile.Services;
using QRCoder;
using Entry=Petrichor.Mobile.UI.NativeEntry;
namespace Petrichor.Mobile.Pages;

public sealed class LoginPage : BasePage
{
    private readonly Entry country=new(){Text="+86",Keyboard=Keyboard.Telephone,FontSize=16,MaxLength=5,WidthRequest=48,Placeholder="区号"};
    private readonly Entry phone=new(){Keyboard=Keyboard.Telephone,FontSize=16,MaxLength=20,Placeholder="请输入手机号码",HorizontalOptions=LayoutOptions.Fill};
    private readonly Entry code=new(){Keyboard=Keyboard.Numeric,FontSize=16,MaxLength=8,Placeholder="请输入验证码"};
    private readonly Entry account=new(){Keyboard=Keyboard.Email,FontSize=16,Placeholder="手机号码或网易邮箱",ReturnType=ReturnType.Next};
    private readonly Entry password=new(){IsPassword=true,FontSize=16,Placeholder="请输入密码",ReturnType=ReturnType.Go,IsTextPredictionEnabled=false,IsSpellCheckEnabled=false};
    private readonly Entry passwordCountry=new(){Text="+86",Keyboard=Keyboard.Telephone,FontSize=16,MaxLength=5,WidthRequest=48,Placeholder="区号"};
    private readonly VerticalStackLayout sms=new(){Spacing=12},credentials=new(){Spacing=12,IsVisible=false},qr=new(){Spacing=16,IsVisible=false};
    private readonly Label qrStatus=Ui.Text("点击生成登录二维码",TypeRole.BodyMedium,"OnSurfaceVariant");
    private readonly Image qrImage=new(){HeightRequest=224,WidthRequest=224,HorizontalOptions=LayoutOptions.Center};
    private readonly Button send;
    private readonly TabStrip tabs;
    private readonly IDispatcherTimer timer;
    private CancellationTokenSource? qrLife;
    private bool signingIn;
    protected override bool TintAppBarOnScroll=>false;
    public LoginPage():base("登录",showMini:false)
    {
        var stack=Stack();stack.Padding=new Thickness(24,8,24,32);
        var mark=Ui.Card(Ui.Glyph("headphones",36,"OnPrimaryContainer"),"PrimaryContainer",Md.ShapeExtraLarge,0);mark.WidthRequest=72;mark.HeightRequest=72;mark.HorizontalOptions=LayoutOptions.Center;mark.Margin=new Thickness(0,16,0,24);stack.Add(mark);
        var heading=Ui.Text("欢迎来到 Petrichor",TypeRole.HeadlineSmall);heading.HorizontalTextAlignment=TextAlignment.Center;stack.Add(heading);
        var intro=Ui.Text("用你的网易云账号，接着听。",TypeRole.BodyMedium,"OnSurfaceVariant");intro.HorizontalTextAlignment=TextAlignment.Center;intro.Margin=new Thickness(0,8,0,24);stack.Add(intro);
        View[] panes=[sms,credentials,qr];var shown=0;
        tabs=new TabStrip(["手机验证码","账号密码","扫码登录"],index=>Run(async()=>{
            PageMotion.Swap((VisualElement)panes[shown],(VisualElement)panes[index]);shown=index;
            heading.Text=index!=2?"欢迎来到 Petrichor":"用扫码，接着听。";intro.Text=index!=2?"用你的网易云账号，接着听。":"在另一台设备上使用网易云音乐扫描。";
            if(index!=2)qrLife?.Cancel();else await StartQr();
        }));
        tabs.Margin=new Thickness(-24,0,-24,16);stack.Add(tabs);
        var number=new Grid{ColumnDefinitions=[new(48),new(1),new(GridLength.Star)],ColumnSpacing=12};number.Add(country);var divider=new BoxView{WidthRequest=1,HeightRequest=24,VerticalOptions=LayoutOptions.Center};divider.SetDynamicResource(BoxView.ColorProperty,"OutlineVariant");number.Add(divider,1);number.Add(phone,2);
        SemanticProperties.SetDescription(country,"手机号国家区号");
        sms.Add(DesignControls.Field("手机号码",number));
        send=Ui.Button("获取验证码",()=>Run(async()=>{if(DateTimeOffset.UtcNow<AppServices.SmsAvailableAt)return;await AppServices.Api.SendLoginCodeAsync(phone.Text??"",Country(),Token);AppServices.SmsAvailableAt=DateTimeOffset.UtcNow.AddSeconds(60);UpdateCooldown();ShowMessage("验证码已发送，请查看短信。");}),"text");
        sms.Add(DesignControls.Field("短信验证码",code,send,supporting:"验证码将由网易云发送至你的手机。"));
        var smsLogin=Ui.Button("登录",()=>SignIn(()=>AppServices.Api.LoginWithCodeAsync(phone.Text??"",code.Text??"",Country(),Token)));smsLogin.Margin=new Thickness(0,12,0,0);sms.Add(smsLogin);
        var notice=DesignControls.Notice("无需安装网易云音乐。登录凭据仅保存在这台设备的安全存储中。若网易云要求额外验证，会显示真实提示，也可以改用扫码登录。","lock");notice.Margin=new Thickness(0,12,0,0);sms.Add(notice);
        var accountRow=new Grid{ColumnDefinitions=[new(48),new(1),new(GridLength.Star)],ColumnSpacing=12};accountRow.Add(passwordCountry);var accountDivider=new BoxView{WidthRequest=1,HeightRequest=24,VerticalOptions=LayoutOptions.Center};accountDivider.SetDynamicResource(BoxView.ColorProperty,"OutlineVariant");accountRow.Add(accountDivider,1);accountRow.Add(account,2);
        SemanticProperties.SetDescription(passwordCountry,"手机号国家区号");
        account.TextChanged+=(_,_)=>{var isEmail=(account.Text??"").Contains('@');passwordCountry.IsVisible=!isEmail;accountDivider.IsVisible=!isEmail;accountRow.ColumnDefinitions[0].Width=isEmail?0:48;accountRow.ColumnDefinitions[1].Width=isEmail?0:1;accountRow.ColumnSpacing=isEmail?0:12;};
        account.Completed+=(_,_)=>password.Focus();
        credentials.Add(DesignControls.Field("手机号 / 网易邮箱",accountRow));
        IconButton? visibility=null;
        visibility=Ui.Icon("visible",()=>{password.IsPassword=!password.IsPassword;visibility!.SetIcon(password.IsPassword?"visible":"hidden",false,true);SemanticProperties.SetDescription(visibility,password.IsPassword?"显示密码":"隐藏密码");return Task.CompletedTask;},"显示密码");
        credentials.Add(DesignControls.Field("密码",password,visibility,supporting:"使用网易云音乐账号密码登录。若要求额外验证，可改用验证码或扫码。"));
        Task PasswordLogin()=>SignIn(()=>AppServices.Api.LoginWithPasswordAsync(account.Text??"",password.Text??"",(passwordCountry.Text??"86").Trim().TrimStart('+'),Token));
        var passwordButton=Ui.Button("登录",PasswordLogin);passwordButton.Margin=new Thickness(0,12,0,0);credentials.Add(passwordButton);password.Completed+=async(_,_)=>await PasswordLogin();
        var passwordNotice=DesignControls.Notice("密码不会保存到设备。登录会话保存在这台设备的安全存储中。","lock");passwordNotice.Margin=new Thickness(0,12,0,0);credentials.Add(passwordNotice);
        // QR codes keep a white quiet zone in both themes so scanners can read them.
        var qrCard=Ui.Card(qrImage,"SurfaceContainerHigh",Md.ShapeExtraLarge,16);qrCard.BackgroundColor=Colors.White;qrCard.HorizontalOptions=LayoutOptions.Center;qrCard.Margin=new Thickness(0,8,0,0);qr.Add(qrCard);
        qrStatus.HorizontalTextAlignment=TextAlignment.Center;qr.Add(qrStatus);
        var refresh=Ui.Button("刷新二维码",()=>Run(StartQr),"tonal","history");refresh.HorizontalOptions=LayoutOptions.Center;qr.Add(refresh);
        var qrTip=Ui.Text("请在另一台设备使用网易云音乐扫描并确认登录。",TypeRole.BodySmall,"OnSurfaceVariant");qrTip.HorizontalTextAlignment=TextAlignment.Center;qr.Add(qrTip);
        stack.Add(sms);stack.Add(credentials);stack.Add(qr);Body.Content=Scroll(stack);
        timer=Dispatcher.CreateTimer();timer.Interval=TimeSpan.FromSeconds(1);timer.Tick+=(_,_)=>UpdateCooldown();
    }
    private string Country()=>(country.Text??"86").Trim().TrimStart('+');
    protected override void OnAppearing(){base.OnAppearing();timer.Start();UpdateCooldown();}
    protected override void OnDisappearing(){timer.Stop();qrLife?.Cancel();password.Text="";password.IsPassword=true;base.OnDisappearing();}
    private async Task SignIn(Func<Task<UserProfile>> login)
    {
        if(signingIn)return;signingIn=true;qrLife?.Cancel();tabs.IsEnabled=false;sms.IsEnabled=false;credentials.IsEnabled=false;
        try{await Run(async()=>{await login();Token.ThrowIfCancellationRequested();password.Text="";AppServices.NotifyAccount();await Ui.Pop();});}
        finally{signingIn=false;tabs.IsEnabled=true;sms.IsEnabled=true;credentials.IsEnabled=true;UpdateCooldown();}
    }
    private void UpdateCooldown(){var seconds=(int)Math.Ceiling((AppServices.SmsAvailableAt-DateTimeOffset.UtcNow).TotalSeconds);send.Text=seconds>0?seconds+" 秒后重试":"获取验证码";send.IsEnabled=seconds<=0;}
    private async Task StartQr()
    {
        qrLife?.Cancel();qrLife?.Dispose();qrLife=CancellationTokenSource.CreateLinkedTokenSource(Token);
        var ct=qrLife.Token;var challenge=await AppServices.Api.BeginQrLoginAsync(ct);
        var png=PngByteQRCodeHelper.GetQRCode(challenge.LoginUrl.ToString(),QRCodeGenerator.ECCLevel.M,8);
        qrImage.Source=ImageSource.FromStream(()=>new MemoryStream(png));qrStatus.Text="等待扫描…";_ = Poll(challenge,ct);
    }
    private async Task Poll(QrLoginChallenge challenge,CancellationToken ct)
    {
        try
        {
            var progress=new Progress<QrLoginState>(s=>{if(!ct.IsCancellationRequested)qrStatus.Text=s switch{QrLoginState.Scanned=>"已扫描，请在网易云音乐中确认。",QrLoginState.Expired=>"二维码已过期，请刷新。",QrLoginState.Authorized=>"登录成功",_=>"等待扫描…"};});
            await AppServices.Api.WaitForQrLoginAsync(challenge,progress,ct);ct.ThrowIfCancellationRequested();AppServices.NotifyAccount();await Ui.Pop();
        }
        catch(OperationCanceledException)when(ct.IsCancellationRequested){}
        catch(Exception e){if(!ct.IsCancellationRequested)qrStatus.Text=e.Message;}
    }
}
