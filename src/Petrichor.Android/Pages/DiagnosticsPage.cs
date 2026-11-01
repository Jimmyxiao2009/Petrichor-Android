using Petrichor.Mobile.Services;
using Petrichor.Mobile.UI;
namespace Petrichor.Mobile.Pages;
public sealed class DiagnosticsPage : BasePage
{
    private readonly Label summary = Ui.Text("", 12, "Muted");
    private readonly Editor entries = new() { IsReadOnly = true, AutoSize = EditorAutoSizeOption.Disabled, HeightRequest = 480, FontSize = 11, FontFamily = "monospace" };
    public DiagnosticsPage() : base("播放诊断")
    {
        var stack = Stack();stack.Add(Ui.Heading("播放诊断日志", "记录中断与恢复，帮助定位偶发问题。"));
        var enabled = new Switch { IsToggled = PlaybackDiagnostics.Enabled };
        SemanticProperties.SetDescription(enabled, "记录播放诊断日志");
        enabled.Toggled += (_, e) => PlaybackDiagnostics.SetEnabled(e.Value);
        stack.Add(DesignControls.SettingRow("记录播放诊断日志", "只保存在本机，不记录账号凭证和播放链接", enabled));
        summary.Margin = Ui.Inset(0, 12); stack.Add(summary);
        var actions = new Grid { ColumnDefinitions = [new(GridLength.Star),new(GridLength.Star),new(GridLength.Star)], ColumnSpacing = 6 };
        actions.Add(Ui.Button("刷新日志", () => Run(Refresh), "tonal"));
        actions.Add(Ui.Button("导出日志", () => Run(async () => {
            var path = await PlaybackDiagnostics.ExportAsync();
            await Share.Default.RequestAsync(new ShareFileRequest { Title = "Petrichor 播放诊断", File = new ShareFile(path) });
        }), "tonal"), 1);
        actions.Add(Ui.Button("清空日志", () => Run(async () => {
            if (await DisplayAlertAsync("清空诊断日志", "将删除这台设备上的播放诊断记录。", "清空", "取消")) { await PlaybackDiagnostics.ClearAsync(); await Refresh(); }
        }), "text"), 2);
        stack.Add(actions);
        var note = Ui.Text("显示最近 100 条。导出包含最多约 1 MB 的记录，可在出现问题后分享给排查人员。", 12, "Muted");note.Margin = Ui.Inset(0,12);stack.Add(note);
        entries.SetDynamicResource(Editor.TextColorProperty, "OnSurface");entries.SetDynamicResource(Editor.BackgroundColorProperty, "SurfaceHigh");stack.Add(entries);
        Body.Content = Scroll(stack);
    }
    protected override bool RefreshOnAppear => true;
    protected override Task LoadAsync() => Refresh();
    private async Task Refresh()
    {
        summary.Text = PlaybackDiagnostics.Summary().Replace("System power saver: True", "系统省电：已开启").Replace("System power saver: False", "系统省电：未开启");
        var text = await PlaybackDiagnostics.ReadAsync();
        entries.Text = string.IsNullOrWhiteSpace(text) ? "还没有播放记录，开始播放后会在这里显示。" : string.Join("\n", text.Split('\n').TakeLast(100));
    }
}