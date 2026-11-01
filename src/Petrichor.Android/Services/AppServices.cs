using System.Security.Cryptography;
using Newtonsoft.Json;
using Petrichor.Core;
namespace Petrichor.Mobile.Services;

public static class AppServices
{
    public static NeteaseClient Api { get; } = new(new PlatformCrypto(), new SecureSession()){Cache=new ResponseCache(new PageCacheStore())};
    public static OfflineLibrary Offline { get; } = new();
    public static PlaybackController Playback { get; } = new();
    public static DateTimeOffset SmsAvailableAt { get; set; }
    public static event Action? AccountChanged;
    public static void NotifyAccount() { _=SaveProfileAsync();MainThread.BeginInvokeOnMainThread(() => AccountChanged?.Invoke()); }
    private static async Task SaveProfileAsync(){try{if(Api.CurrentUser is{} user)await SecureStorage.Default.SetAsync("netease.profile.v1",JsonConvert.SerializeObject(user));else SecureStorage.Default.Remove("netease.profile.v1");}catch(System.Security.Cryptography.CryptographicException){}}
    private static Task? initialize;
    public static async Task InitializeAsync()
    {
        var task=initialize??=RestoreAsync();
        try{await task;}catch{if(ReferenceEquals(initialize,task))initialize=null;throw;}
    }
    private static async Task RestoreAsync()
    {
        // All disk IO and deserialization happen off the first-frame thread.
        await Task.Run(async()=>{
            await Offline.InitializeAsync();
            UserProfile? profile=null;
            try{var json=await SecureStorage.Default.GetAsync("netease.profile.v1");if(json!=null)profile=JsonConvert.DeserializeObject<UserProfile>(json);}catch(Exception e)when(e is JsonException or System.Security.Cryptography.CryptographicException){}
            var hasCookie=await Api.RestoreLocalSessionAsync(profile);
            _=WarmAsync(hasCookie);
        });
    }
    private static async Task WarmAsync(bool verifySession)
    {
        // Let the first visible page request its data before low-priority prefetch.
        await Task.Delay(1500);
        if(verifySession){try{await Api.GetCurrentUserAsync();NotifyAccount();}catch(MusicApiException e){if(e.RequiresLogin)NotifyAccount();}}
        async Task Try(Func<Task> fetch){try{await fetch();}catch(Exception e)when(e is MusicApiException or HttpRequestException or OperationCanceledException){}}
        await Try(()=>Api.GetHotRadiosAsync(30));
        await Try(()=>Api.GetChartsAsync());
        await Try(()=>Api.GetPlaylistsAsync("全部",30));
        if(Api.IsLoggedIn){await Try(()=>Api.GetMyPlaylistsAsync(100));await Try(()=>Api.GetDailySongsAsync());}
    }
}
internal sealed class SecureSession : ISessionStore
{
    private const string Key = "netease.session.v1";
    public async Task<string> LoadAsync()
    {
        try { return await SecureStorage.Default.GetAsync(Key) ?? ""; }
        catch (System.Security.Cryptography.CryptographicException) { SecureStorage.Default.Remove(Key); return ""; }
    }
    public Task SaveAsync(string cookie) => SecureStorage.Default.SetAsync(Key, cookie);
    public Task ClearAsync() { SecureStorage.Default.Remove(Key); return Task.CompletedTask; }
}
internal sealed class PlatformCrypto : ICryptoProvider, IEapiCryptoProvider
{
    public byte[] RandomBytes(int length) => RandomNumberGenerator.GetBytes(length);
    public byte[] Md5(byte[] input) => MD5.HashData(input);
    public byte[] AesCbcPkcs7(byte[] input, byte[] key, byte[] iv)
    { using var aes = Aes.Create(); aes.Key = key; aes.IV = iv; return aes.CreateEncryptor().TransformFinalBlock(input, 0, input.Length); }
    public byte[] AesEcbPkcs7(byte[] input, byte[] key)
    { using var aes = Aes.Create(); aes.Key = key; aes.Mode = CipherMode.ECB; return aes.CreateEncryptor().TransformFinalBlock(input, 0, input.Length); }
}
