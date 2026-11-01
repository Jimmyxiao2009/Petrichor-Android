using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using AndroidX.AppCompat.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using View=Microsoft.Maui.Controls.View;
using TextChangedEventArgs=Microsoft.Maui.Controls.TextChangedEventArgs;
using TextAlignment=Microsoft.Maui.TextAlignment;

namespace Petrichor.Mobile.UI;

// Android owns the editable buffer, caret and composing spans. This deliberately
// avoids the Entry handler's round trip of native selection through MAUI.
public sealed class NativeEntry : View
{
    public static readonly BindableProperty IsMultilineProperty=BindableProperty.Create(nameof(IsMultiline),typeof(bool),typeof(NativeEntry),false);
    public bool IsMultiline{get=>(bool)GetValue(IsMultilineProperty);set=>SetValue(IsMultilineProperty,value);}
    public static readonly BindableProperty TextProperty=BindableProperty.Create(nameof(Text),typeof(string),typeof(NativeEntry),"",BindingMode.TwoWay,propertyChanged:(b,o,n)=>((NativeEntry)b).TextChanged?.Invoke(b,new TextChangedEventArgs((string?)o,(string?)n)));
    public static readonly BindableProperty PlaceholderProperty=BindableProperty.Create(nameof(Placeholder),typeof(string),typeof(NativeEntry),"");
    public static readonly BindableProperty TextColorProperty=BindableProperty.Create(nameof(TextColor),typeof(Color),typeof(NativeEntry),Colors.Black);
    public static readonly BindableProperty PlaceholderColorProperty=BindableProperty.Create(nameof(PlaceholderColor),typeof(Color),typeof(NativeEntry),Colors.Gray);
    public static readonly BindableProperty FontSizeProperty=BindableProperty.Create(nameof(FontSize),typeof(double),typeof(NativeEntry),16d);
    public static readonly BindableProperty KeyboardProperty=BindableProperty.Create(nameof(Keyboard),typeof(Keyboard),typeof(NativeEntry),Keyboard.Default);
    public static readonly BindableProperty IsPasswordProperty=BindableProperty.Create(nameof(IsPassword),typeof(bool),typeof(NativeEntry),false);
    public static readonly BindableProperty MaxLengthProperty=BindableProperty.Create(nameof(MaxLength),typeof(int),typeof(NativeEntry),int.MaxValue);
    public static readonly BindableProperty ReturnTypeProperty=BindableProperty.Create(nameof(ReturnType),typeof(ReturnType),typeof(NativeEntry),ReturnType.Default);
    public static readonly BindableProperty IsTextPredictionEnabledProperty=BindableProperty.Create(nameof(IsTextPredictionEnabled),typeof(bool),typeof(NativeEntry),true);
    public static readonly BindableProperty IsSpellCheckEnabledProperty=BindableProperty.Create(nameof(IsSpellCheckEnabled),typeof(bool),typeof(NativeEntry),true);
    public string? Text{get=>(string?)GetValue(TextProperty);set=>SetValue(TextProperty,value);}
    public string Placeholder{get=>(string)GetValue(PlaceholderProperty);set=>SetValue(PlaceholderProperty,value);}
    public Color TextColor{get=>(Color)GetValue(TextColorProperty);set=>SetValue(TextColorProperty,value);}
    public Color PlaceholderColor{get=>(Color)GetValue(PlaceholderColorProperty);set=>SetValue(PlaceholderColorProperty,value);}
    public double FontSize{get=>(double)GetValue(FontSizeProperty);set=>SetValue(FontSizeProperty,value);}
    public Keyboard Keyboard{get=>(Keyboard)GetValue(KeyboardProperty);set=>SetValue(KeyboardProperty,value);}
    public bool IsPassword{get=>(bool)GetValue(IsPasswordProperty);set=>SetValue(IsPasswordProperty,value);}
    public int MaxLength{get=>(int)GetValue(MaxLengthProperty);set=>SetValue(MaxLengthProperty,value);}
    public ReturnType ReturnType{get=>(ReturnType)GetValue(ReturnTypeProperty);set=>SetValue(ReturnTypeProperty,value);}
    public bool IsTextPredictionEnabled{get=>(bool)GetValue(IsTextPredictionEnabledProperty);set=>SetValue(IsTextPredictionEnabledProperty,value);}
    public bool IsSpellCheckEnabled{get=>(bool)GetValue(IsSpellCheckEnabledProperty);set=>SetValue(IsSpellCheckEnabledProperty,value);}
    public ClearButtonVisibility ClearButtonVisibility{get;set;}
    public TextAlignment VerticalTextAlignment{get;set;}=TextAlignment.Center;
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler? Completed;
    public new bool Focus()=>Handler is NativeEntryHandler handler&&handler.FocusEditor();
    public new void Unfocus(){if(Handler?.PlatformView is AppCompatEditText native)native.ClearFocus();}
    internal void NativeFocusChanged(bool focused)=>((IView)this).IsFocused=focused;
    internal void Complete()=>Completed?.Invoke(this,EventArgs.Empty);
}

public sealed class NativeEntryHandler : ViewHandler<NativeEntry,AppCompatEditText>
{
    private static readonly PropertyMapper<NativeEntry,NativeEntryHandler> InputMapper=new(ViewMapper)
    {
        [nameof(NativeEntry.Text)]=(h,v)=>{var text=v.Text??"";if(h.PlatformView.Text!=text){h.PlatformView.Text=text;h.PlatformView.SetSelection(text.Length);}},
        [nameof(NativeEntry.Placeholder)]=(h,v)=>h.PlatformView.Hint=v.Placeholder,
        [nameof(NativeEntry.TextColor)]=(h,v)=>h.PlatformView.SetTextColor(v.TextColor.ToPlatform()),
        [nameof(NativeEntry.PlaceholderColor)]=(h,v)=>h.PlatformView.SetHintTextColor(v.PlaceholderColor.ToPlatform()),
        [nameof(NativeEntry.FontSize)]=(h,v)=>h.PlatformView.SetTextSize(global::Android.Util.ComplexUnitType.Sp,(float)v.FontSize),
        [nameof(NativeEntry.IsMultiline)]=(h,_)=>h.UpdateInput(),
        [nameof(NativeEntry.Keyboard)]=(h,_)=>h.UpdateInput(),
        [nameof(NativeEntry.IsPassword)]=(h,_)=>h.UpdateInput(),
        [nameof(NativeEntry.IsTextPredictionEnabled)]=(h,_)=>h.UpdateInput(),
        [nameof(NativeEntry.IsSpellCheckEnabled)]=(h,_)=>h.UpdateInput(),
        [nameof(NativeEntry.MaxLength)]=(h,v)=>h.PlatformView.SetFilters(v.MaxLength==int.MaxValue?Array.Empty<IInputFilter>():new IInputFilter[]{new InputFilterLengthFilter(v.MaxLength)}),
        [nameof(NativeEntry.ReturnType)]=(h,v)=>h.PlatformView.ImeOptions=v.ReturnType switch{ReturnType.Search=>ImeAction.Search,ReturnType.Next=>ImeAction.Next,ReturnType.Go=>ImeAction.Go,ReturnType.Send=>ImeAction.Send,_=>ImeAction.Done}
    };
    public NativeEntryHandler():base(InputMapper){}
    protected override AppCompatEditText CreatePlatformView()
    {
        var native=new AppCompatEditText(Context);native.SetSingleLine(true);native.Background=null;
        native.SetPadding(0,0,0,0);native.SetMinimumHeight(0);native.SetIncludeFontPadding(false);
        native.Gravity=GravityFlags.CenterVertical|GravityFlags.Start;return native;
    }
    protected override void ConnectHandler(AppCompatEditText native)
    {base.ConnectHandler(native);native.TextChanged+=Changed;native.FocusChange+=FocusChanged;native.EditorAction+=EditorAction;}
    protected override void DisconnectHandler(AppCompatEditText native)
    {native.TextChanged-=Changed;native.FocusChange-=FocusChanged;native.EditorAction-=EditorAction;base.DisconnectHandler(native);}
    private void Changed(object? sender,global::Android.Text.TextChangedEventArgs e)
    {var value=PlatformView.Text??"";if(VirtualView.Text!=value)VirtualView.Text=value;if(VirtualView.IsMultiline)PlatformView.Post(Grow);}
    private void FocusChanged(object? sender,global::Android.Views.View.FocusChangeEventArgs e)=>VirtualView.NativeFocusChanged(e.HasFocus);
    private void EditorAction(object? sender,global::Android.Widget.TextView.EditorActionEventArgs e)
    {
        if(VirtualView.IsMultiline)return;
        if(e.Event?.KeyCode==Keycode.Enter){e.Handled=true;if(e.Event.Action==KeyEventActions.Up)VirtualView.Complete();}
        else if(e.Event==null){e.Handled=true;VirtualView.Complete();}
    }
    private void UpdateInput()
    {
        var start=PlatformView.SelectionStart;var end=PlatformView.SelectionEnd;var input=VirtualView;
        var type=input.Keyboard==Keyboard.Telephone?InputTypes.ClassPhone:input.Keyboard==Keyboard.Numeric?InputTypes.ClassNumber:InputTypes.ClassText;
        if(input.Keyboard==Keyboard.Email)type|=InputTypes.TextVariationEmailAddress;
        if(input.IsPassword)type=InputTypes.ClassText|InputTypes.TextVariationPassword;
        else if(!input.IsSpellCheckEnabled||!input.IsTextPredictionEnabled)type|=InputTypes.TextFlagNoSuggestions;
        if(input.IsMultiline)type|=InputTypes.TextFlagMultiLine;
        PlatformView.InputType=type;PlatformView.SetSingleLine(!input.IsMultiline);if(input.IsMultiline){PlatformView.SetMaxLines(4);PlatformView.ImeOptions=ImeAction.None;}PlatformView.Typeface=global::Android.Graphics.Typeface.Default;
        PlatformView.TransformationMethod=input.IsPassword?global::Android.Text.Method.PasswordTransformationMethod.Instance:null;
        PlatformView.Gravity=GravityFlags.CenterVertical|GravityFlags.Start;
        var length=PlatformView.Text?.Length??0;if(start>=0&&end>=start)PlatformView.SetSelection(Math.Min(start,length),Math.Min(end,length));
    }
    private void Grow()
    {
        if(VirtualView==null||!VirtualView.IsMultiline||PlatformView.Width<=0)return;
        var density=PlatformView.Resources!.DisplayMetrics!.Density;
        var height=Math.Max(36,Math.Min(4,Math.Max(1,PlatformView.LineCount))*PlatformView.LineHeight/density+12);
        if(Math.Abs(VirtualView.HeightRequest-height)>.5)VirtualView.HeightRequest=height;
    }
    public bool FocusEditor()
    {
        var focused=PlatformView.RequestFocus();if(focused)PlatformView.Post(()=>
            (Context.GetSystemService(global::Android.Content.Context.InputMethodService) as InputMethodManager)?.ShowSoftInput(PlatformView,ShowFlags.Implicit));
        return focused;
    }
}
