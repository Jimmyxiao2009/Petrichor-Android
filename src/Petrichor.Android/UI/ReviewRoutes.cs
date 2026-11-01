#if DEBUG
using Petrichor.Core;
using Petrichor.Mobile.Pages;
using Petrichor.Mobile.Services;
namespace Petrichor.Mobile.UI;

// Disposable emulator review only; no route or fixture is compiled in Release.
internal static class ReviewRoutes
{
    public static async Task OpenAsync(string name,long id)
    {
        try
        {
            await AppServices.InitializeAsync();
            var track=(await AppServices.Api.GetTracksAsync([id>0?id:186016])).First();
            Page page;
            switch(name)
            {
                case "player":AppServices.Playback.Queue.Replace([track]);AppServices.Playback.Notify();page=new PlayerPage();break;
                case "comments":page=new CommentsPage(track);break;
                case "downloads":page=new DownloadsPage();break;
                case "settings":page=new SettingsPage();break;
                case "login":page=new LoginPage();break;
                case "playlist":page=new PlaylistPage(id>0?id:3778678);break;
                case "artist":page=new ArtistPage(id>0?id:6452);break;
                case "albums":page=new ArtistAlbumsPage(id>0?id:6452);break;
                case "album":page=new AlbumPage(id>0?id:18954);break;
                case "podcast":page=new PodcastPage(id>0?id:(await AppServices.Api.GetHotRadiosAsync(1,0)).Items.First().Id);break;
                case "queue":page=new QueuePage();break;
                case "lyrics":page=new LyricsPage();break;
                case "fm":page=new FmPage();break;
                case "daily":page=new CatalogPage(CatalogKind.Daily);break;
                case "charts":page=new CatalogPage(CatalogKind.Charts);break;
                case "catalog":page=new CatalogPage(CatalogKind.Playlists);break;
                case "search":page=new SearchPage("Coldplay");break;
                case "programs":page=new SearchPage("Coldplay",5);break;
                case "flac":
                    var fixture=new Track{Id=-42,Name="FLAC 解码测试",LocalFilePath=Path.Combine(FileSystem.AppDataDirectory,"qa.flac"),Artists=[new Artist{Name="本机测试音频"}]};
                    await AppServices.Playback.PlayAsync([fixture],fixture);AppServices.Playback.Seek(TimeSpan.FromSeconds(20));page=new PlayerPage();break;
                case "export":await DownloadFolder.ExportAsync(Path.Combine(FileSystem.AppDataDirectory,"qa.flac"),"Petrichor FLAC review","flac");return;
                default:return;
            }
            await Ui.Push(page);
        }
        catch(Exception error){if(Shell.Current.CurrentPage is BasePage current)current.ShowMessage(Ui.ErrorText(error));}
    }
}
#endif
