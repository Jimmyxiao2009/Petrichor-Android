namespace Petrichor.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        void AlignEntry(Microsoft.Maui.Handlers.IEntryHandler handler)
        {
            var view=handler.PlatformView;view.Background=null;view.SetPadding(0,0,0,0);
            view.Gravity=global::Android.Views.GravityFlags.CenterVertical|global::Android.Views.GravityFlags.Start;
            view.SetIncludeFontPadding(false);
        }
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("PetrichorField",(handler,_)=>AlignEntry(handler));
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Entry.VerticalTextAlignment),(handler,_)=>AlignEntry(handler));
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Entry.IsPassword),(handler,_)=>AlignEntry(handler));
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Entry.Keyboard),(handler,_)=>AlignEntry(handler));
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("PetrichorField",(handler,_)=>handler.PlatformView.Background=null);
        return MauiApp.CreateBuilder().UseMauiApp<App>()
            .ConfigureMauiHandlers(handlers=>{handlers.AddHandler(typeof(UI.IconTextButton),typeof(UI.IconButtonHandler));handlers.AddHandler(typeof(UI.NativeEntry),typeof(UI.NativeEntryHandler));handlers.AddHandler(typeof(UI.PlayerDeck),typeof(UI.PlayerDeckHandler));handlers.AddHandler(typeof(UI.MusicSlider),typeof(UI.MusicSliderHandler));handlers.AddHandler(typeof(Entry),typeof(Microsoft.Maui.Handlers.EntryHandler));handlers.AddHandler(typeof(Slider),typeof(Microsoft.Maui.Handlers.SliderHandler));})
            .ConfigureFonts(f => f.AddFont("MaterialIconsOutlined-Regular.otf", "Icons").AddFont("MaterialIcons-Regular.ttf","IconsFilled")).Build();
    }
}

