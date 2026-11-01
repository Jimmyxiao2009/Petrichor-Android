using Android.Graphics;
using Petrichor.Core;
namespace Petrichor.Mobile.Services;

// Decode outside the UI thread. SystemUI needs an actual bitmap; an app-private
// path or an HTTP URL in metadata alone is insufficient on many Android builds.
internal static class MediaArtwork
{
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    public static async Task<Bitmap?> LoadAsync(string source, CancellationToken token)
    {
        byte[] bytes;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") {
            var url = new UriBuilder(uri);
            if (uri.Host.EndsWith(".music.126.net", StringComparison.OrdinalIgnoreCase)) {
                url.Scheme = "https"; url.Port = -1;
                if (!uri.Query.Contains("param=")) url.Query = uri.Query.TrimStart('?') + (uri.Query.Length > 1 ? "&" : "") + "param=512y512";
            }
            bytes = await http.GetByteArrayAsync(url.Uri, token).ConfigureAwait(false);
        } else if (File.Exists(source)) {
            bytes = await File.ReadAllBytesAsync(source, token).ConfigureAwait(false);
        } else {
            var context = global::Android.App.Application.Context;
            var resource = context.Resources!.GetIdentifier("forest", "drawable", context.PackageName);
            return resource == 0 ? null : BitmapFactory.DecodeResource(context.Resources, resource);
        }
        token.ThrowIfCancellationRequested();
        using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds);
        var sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / sample > 1024) sample *= 2;
        using var options = new BitmapFactory.Options { InSampleSize = sample };
        var bitmap = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, options);
        if (bitmap == null) return null;
        if (token.IsCancellationRequested) { bitmap.Dispose(); token.ThrowIfCancellationRequested(); }
        var side = Math.Max(bitmap.Width, bitmap.Height);
        if (side <= 512) return bitmap;
        var scaled = Bitmap.CreateScaledBitmap(bitmap, Math.Max(1, bitmap.Width * 512 / side), Math.Max(1, bitmap.Height * 512 / side), true);
        if (!ReferenceEquals(scaled, bitmap)) bitmap.Dispose();
        return scaled;
    }
}