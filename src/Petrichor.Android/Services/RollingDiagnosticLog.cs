using System.Text.RegularExpressions;
using System.Threading.Channels;
namespace Petrichor.Mobile.Services;
internal sealed class RollingDiagnosticLog : IAsyncDisposable
{
    private sealed record Entry(string? Line = null, bool Clear = false, TaskCompletionSource? Done = null);
    private readonly Channel<Entry> queue = Channel.CreateBounded<Entry>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    private readonly SemaphoreSlim disk = new(1, 1);
    private readonly string root;
    private readonly long maxBytes;
    private readonly Task consumer;
    private string Current => Path.Combine(root, "playback.log");
    private string Previous => Path.Combine(root, "playback.previous.log");
    public RollingDiagnosticLog(string root, long maxBytes = 512 * 1024)
    {
        this.root = root; this.maxBytes = maxBytes; consumer = Task.Run(ConsumeAsync);
    }
    internal static string Sanitize(string value)
    {
        value = Regex.Replace(value, @"https?://\S+", "[url]", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"(?i)(cookie|authorization|password|token|secret|music_u|__csrf)\s*[:=]\s*[^\r\n]+", "$1=[redacted]");
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        return value.Length <= 512 ? value : value[..512];
    }
    public void Write(string name, string details = "") =>
        queue.Writer.TryWrite(new Entry(DateTimeOffset.Now.ToString("O") + " " + Sanitize(name + " " + details)));
    private async Task ConsumeAsync()
    {
        await foreach (var entry in queue.Reader.ReadAllAsync()) {
            await disk.WaitAsync();
            try {
                Directory.CreateDirectory(root);
                if (entry.Clear) {
                    await File.WriteAllTextAsync(Current, "");
                    await File.WriteAllTextAsync(Previous, "");
                } else if (entry.Line != null) {
                    if (File.Exists(Current) && new FileInfo(Current).Length >= maxBytes) File.Move(Current, Previous, true);
                    await File.AppendAllTextAsync(Current, entry.Line + System.Environment.NewLine);
                }
                entry.Done?.TrySetResult();
            } catch (Exception error) {
                entry.Done?.TrySetException(new IOException("无法读取或保存本机诊断日志。", error));
            } finally { disk.Release(); }
        }
    }
    public async Task FlushAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await queue.Writer.WriteAsync(new Entry(Done: done)); await done.Task;
    }
    public async Task<string> ReadAsync()
    {
        await FlushAsync(); await disk.WaitAsync();
        try {
            var previous = File.Exists(Previous) ? await File.ReadAllTextAsync(Previous) : "";
            var current = File.Exists(Current) ? await File.ReadAllTextAsync(Current) : "";
            return previous + current;
        } finally { disk.Release(); }
    }
    public async Task ClearAsync()
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await queue.Writer.WriteAsync(new Entry(Clear: true, Done: done)); await done.Task;
    }
    public async ValueTask DisposeAsync() { queue.Writer.TryComplete(); await consumer; disk.Dispose(); }
}