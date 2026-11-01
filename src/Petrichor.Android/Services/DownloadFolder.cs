using Android.Content;
using Android.Provider;
namespace Petrichor.Mobile.Services;

internal static class DownloadFolder
{
    internal const int RequestCode=5721;
    private static TaskCompletionSource<bool>? pending;
    public static string Description=>Preferences.Get("downloadTreeName","应用专属空间");
    public static async Task<bool> ChooseAsync()
    {
        if(pending!=null)return false;
        var activity=MainActivity.Instance??throw new InvalidOperationException("当前没有可用的窗口。");
        pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var intent=new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission|ActivityFlags.GrantWriteUriPermission|ActivityFlags.GrantPersistableUriPermission|ActivityFlags.GrantPrefixUriPermission);
            var saved=Preferences.Get("downloadTree","");
            if(saved.Length>0)intent.PutExtra(DocumentsContract.ExtraInitialUri,global::Android.Net.Uri.Parse(saved));
            activity.StartActivityForResult(intent,RequestCode);return await pending.Task;
        }
        finally{pending=null;}
    }
    internal static void Complete(global::Android.App.Result result,Intent? data)
    {
        if(result!=global::Android.App.Result.Ok||data?.Data is not{} uri){pending?.TrySetResult(false);return;}
        try
        {
            var resolver=MainActivity.Instance!.ContentResolver!;
            var flags=data.Flags&(ActivityFlags.GrantReadUriPermission|ActivityFlags.GrantWriteUriPermission);
            resolver.TakePersistableUriPermission(uri,flags);
            var document=DocumentsContract.BuildDocumentUriUsingTree(uri,DocumentsContract.GetTreeDocumentId(uri))!;
            using var cursor=resolver.Query(document,[DocumentsContract.Document.ColumnDisplayName],null,null,null);
            var name=cursor?.MoveToFirst()==true?cursor.GetString(0):"所选文件夹";
            Preferences.Set("downloadTree",uri.ToString());Preferences.Set("downloadTreeName",name??"所选文件夹");pending?.TrySetResult(true);
        }
        catch(Exception e){pending?.TrySetException(new IOException("无法获得文件夹访问权限，请重新选择。",e));}
    }
    public static void UsePrivate(){Preferences.Remove("downloadTree");Preferences.Remove("downloadTreeName");}
    public static async Task ExportAsync(string path,string title,string format)
    {
        var saved=Preferences.Get("downloadTree","");if(saved.Length==0)return;
        var resolver=global::Android.App.Application.Context.ContentResolver!;
        var tree=global::Android.Net.Uri.Parse(saved)!;
        global::Android.Net.Uri? file=null;
        try
        {
            var extension=format.ToLowerInvariant() switch{"flac"=>"flac","mp3"=>"mp3","aac"=>"aac","m4a"=>"m4a","ogg"=>"ogg",_=>"audio"};
            var name=string.Concat(title.Select(c=>Path.GetInvalidFileNameChars().Contains(c)||c is '/' or '\\'?'_':c));
            if(name.Length>120)name=name[..120];
            var parent=DocumentsContract.BuildDocumentUriUsingTree(tree,DocumentsContract.GetTreeDocumentId(tree))!;
            file=DocumentsContract.CreateDocument(resolver,parent,extension=="mp3"?"audio/mpeg":"audio/"+extension,name+"."+extension)??throw new IOException("无法创建文件。");
            await using var output=resolver.OpenOutputStream(file,"w")??throw new IOException("无法写入文件。");
            await using var input=File.OpenRead(path);await input.CopyToAsync(output);
        }
        catch(Exception e)
        {
            if(file!=null)try{DocumentsContract.DeleteDocument(resolver,file);}catch{}
            throw new IOException("无法写入下载文件夹，请重新选择文件夹后重试。",e);
        }
    }
}
