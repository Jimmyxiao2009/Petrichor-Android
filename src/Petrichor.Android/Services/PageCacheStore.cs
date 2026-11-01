using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Petrichor.Core;
namespace Petrichor.Mobile.Services;

// Only allowlisted API read models reach this store. Tokens and stream URLs do not.
internal sealed class PageCacheStore : IResponseCacheStore
{
    private string Root => Path.Combine(FileSystem.AppDataDirectory,"page-cache-v1");
    private string FileName(string key)=>Path.Combine(Root,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))+".json");
    public async Task<CachedResponse> ReadAsync(string key)
    {
        var path=FileName(key);if(!File.Exists(path))return null!;
        try{return JsonConvert.DeserializeObject<CachedResponse>(await File.ReadAllTextAsync(path).ConfigureAwait(false))!;}
        catch(JsonException){File.Delete(path);return null!;}
    }
    public async Task WriteAsync(string key,CachedResponse value)
    {
        Directory.CreateDirectory(Root);var path=FileName(key);
        await File.WriteAllTextAsync(path+".tmp",JsonConvert.SerializeObject(value)).ConfigureAwait(false);
        File.Move(path+".tmp",path,true);
        var files=new DirectoryInfo(Root).GetFiles("*.json").OrderByDescending(x=>x.LastWriteTimeUtc).ToArray();
        long bytes=0;for(int i=0;i<files.Length;i++){bytes+=files[i].Length;if(i>=128||bytes>24*1024*1024)files[i].Delete();}
    }
    public Task ClearAsync()
    {
        if(Directory.Exists(Root))foreach(var file in Directory.EnumerateFiles(Root))File.Delete(file);
        return Task.CompletedTask;
    }
}
