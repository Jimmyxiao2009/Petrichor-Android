using Petrichor.Mobile.Services;
internal static class DiagnosticLogTests
{
    public static async Task Run(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"PetrichorLogTests-"+Guid.NewGuid().ToString("N"));
        await using var log=new RollingDiagnosticLog(root,2048);
        log.Write("native.error","positionMs=12345 powerSave=True");
        var first=await log.ReadAsync();
        check(first.Contains("native.error")&&first.Contains("positionMs=12345"),"Diagnostic events are persisted asynchronously and readable after a flush");
        log.Write("privacy","url=https://example.invalid/audio?token=private cookie=MUSIC_U=secret");
        var redacted=await log.ReadAsync();
        check(!redacted.Contains("example.invalid")&&!redacted.Contains("MUSIC_U=secret")&&redacted.Contains("[redacted]"),"Diagnostic output removes remote URLs and credential fields");
        for(var i=0;i<150;i++)log.Write("heartbeat","sequence="+i+" "+new string('x',120));
        await log.FlushAsync();log.Write("marker","latest-event");await log.FlushAsync();
        var rotated=await log.ReadAsync();
        check(Directory.GetFiles(root).Length==2&&rotated.Contains("latest-event")&&new DirectoryInfo(root).GetFiles().Sum(f=>f.Length)<6000,"Diagnostic logs rotate between two bounded files while preserving recent events");
        await log.ClearAsync();
        check(string.IsNullOrEmpty(await log.ReadAsync()),"Clearing diagnostics removes both current and previous records");
    }
}