using Android.Media;
using Android.Net.Wifi;
using Petrichor.Core;
using Petrichor.Mobile.Services;

static class Program
{
    private static int checks;
    static void Check(bool ok,string label) {
        if(!ok)throw new Exception("FAIL: "+label);
        checks++; Console.WriteLine("PASS: "+label);
    }
    static Track[] Tracks() => [new(){Id=1},new(){Id=2},new(){Id=3}];
    static async Task Until(Func<bool> condition) {
        for(var i=0;i<800;i++){if(condition())return;await Task.Delay(5);}
        throw new Exception("Timed out waiting for playback");
    }
    static async Task Main() {
        var p = new PlaybackController();
        await p.PlayAsync(Tracks(),Tracks()[1]);
        var old = p.Player!;
        old.CurrentPosition=60000;
        await p.PreviousAsync();
        Check(p.Queue.Current.Id==1 && p.Player!.StartPosition==0,"Previous after 60 seconds selects the previous song on one press");
        var current=p.Player;
        old.End();
        Check(p.Player==current && p.Queue.Current.Id==1,"A queued completion from a released player cannot skip another song");
        p.Queue.Repeat=RepeatMode.One;
        await p.NextAsync();
        Check(p.Queue.Current.Id==2,"Manual next bypasses repeat-one on one press");
        old.RaiseError();
        Check(p.Error==null && p.IsPlaying,"A released player's late error cannot stop the current song");
        p.OnAudioFocusChange(AudioFocus.LossTransient);
        Check(!p.IsPlaying && WifiManager.HeldLocks==0,"Temporary focus loss releases the streaming Wi-Fi lock");
        p.OnAudioFocusChange(AudioFocus.Gain);
        Check(p.IsPlaying && WifiManager.HeldLocks==1,"Focus gain resumes the same player with the network lock");
        p.Pause();

        // Simulate an API that returns after cancellation rather than obeying it.
        var first=new TaskCompletionSource<StreamInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second=new TaskCompletionSource<StreamInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        AppServices.Api.Request=(id,_)=>id==1?first.Task:second.Task;
        var a=p.PlayAsync(Tracks());
        var b=p.NextAsync();
        first.SetResult(FakeApi.Stream(1));
        await a;
        Check(p.IsLoading && p.CurrentStream==null && p.Queue.Current.Id==2,"Cancelled URL response cannot clear a newer loading state or install stale metadata");
        second.SetResult(FakeApi.Stream(2));
        await b;
        Check(p.IsPlaying && p.CurrentStream!.TrackId==2,"The latest skip request owns the player and stream metadata");
        p.Pause();
        AppServices.Api.Request=null;

        MediaPlayer.AutoPrepare=false;
        var prepare=p.PlayAsync(Tracks());
        var candidate=MediaPlayer.Created.Last();
        p.Pause(); candidate.CompletePreparation(); await prepare;
        Check(p.Player==null && !p.IsPlaying && !p.IsLoading && candidate.Released && WifiManager.HeldLocks==0,
            "Pausing during preparation cancels and releases the candidate without starting it");
        MediaPlayer.AutoPrepare=true;
        await p.ToggleAsync();
        Check(p.IsPlaying && p.Queue.Current.Id==1,"Playback can restart after preparation was cancelled");
        p.Player!.CurrentPosition=90000;
        p.Player.End();
        await Until(()=>p.IsPlaying&&!p.IsLoading);
        Check(p.Queue.Current.Id==1 && p.Player!.StartPosition==90000,"An early EOF reloads the same song and seeks before audio starts");
        p.Player.CurrentPosition=120000; p.Player.End();
        await Until(()=>p.IsPlaying&&!p.IsLoading);
        p.Player.CurrentPosition=130000; p.Player.End();
        await Until(()=>p.Error!=null);
        Check(p.Queue.Current.Id==1 && !p.IsPlaying && WifiManager.HeldLocks==0,"Repeated early EOF stops with an error instead of silently skipping");
        p.Pause();

        p.Queue.Repeat=RepeatMode.All;
        await p.PlayAsync(Tracks());
        var ended=p.Player!;
        ended.CurrentPosition=ended.Duration; ended.End();
        await Until(()=>p.IsPlaying&&!p.IsLoading&&p.Queue.Current.Id==2);
        ended.End();
        Check(p.Queue.Current.Id==2,"Normal completion advances exactly once even if the old callback repeats");
        p.Pause();

        MediaPlayer.NativeDuration=30000;
        await p.PlayAsync(Tracks());
        p.Player!.CurrentPosition=30000; p.Player.End();
        await Until(()=>p.IsPlaying&&!p.IsLoading&&p.Queue.Current.Id==2);
        Check(p.Queue.Current.Id==2,"A preview clip's native end advances normally");
        p.Player!.RaiseError(-1010);
        Check(p.Player==null && p.Error!=null && p.Queue.Current.Id==2 && WifiManager.HeldLocks==0,
            "Native stream errors remain on the current track and release the player and lock");
        p.Pause();
        MediaPlayer.NativeDuration=240000;
        await p.PlayAsync(Tracks());
        p.Player!.CurrentPosition=62000; p.Notify();
        p.Player.RaiseError();
        await Until(()=>p.IsPlaying&&!p.IsLoading);
        Check(p.Queue.Current.Id==1 && p.Player!.StartPosition==62000,"A transient native network error recovers the same song at its previous position");
        var active=p.Player;
        var updates=0; p.Changed+=()=>updates++;
        p.SetUiActive(false); p.Notify(); await Task.Delay(650);
        Check(updates==0 && !p.AnimateProgress && p.IsPlaying,"Background playback suspends UI ticks and waveform animation without pausing audio");
        p.SetUiActive(true);
        Check(updates>0 && p.AnimateProgress && p.Player==active,"Foreground resume refreshes the existing player once and resumes visual progress");
        p.Player!.RaiseError(); p.Pause(); await Task.Delay(700);
        Check(!p.IsPlaying && p.Player==null && WifiManager.HeldLocks==0,"User pause cancels a pending network recovery without restarting audio");
        var calls=0;
        AppServices.Api.Request=(id,_)=>++calls<3?Task.FromException<StreamInfo>(new HttpRequestException("temporary disconnect")):Task.FromResult(FakeApi.Stream(id));
        await p.PlayAsync(Tracks());
        Check(calls==3 && p.IsPlaying && p.Queue.Current.Id==1,"Temporary URL lookup failures retry with a bound and preserve the selected song");
        p.Pause(); AppServices.Api.Request=null;
        MediaPlayer.AutoPrepare=false;
        var interrupted=p.PlayAsync(Tracks());
        p.OnAudioFocusChange(AudioFocus.LossTransient);
        await interrupted;
        Check(p.Player==null && !p.IsLoading,"Transient focus loss also cancels unfinished preparation");
        MediaPlayer.AutoPrepare=true; p.OnAudioFocusChange(AudioFocus.Gain);
        await Until(()=>p.IsPlaying&&!p.IsLoading);
        Check(p.Queue.Current.Id==1 && p.IsPlaying,"Focus gain restarts a preparation interrupted by transient focus loss");
        p.Pause();
        await DiagnosticLogTests.Run(Check);
        Console.WriteLine("All "+checks+" playback lifecycle checks passed.");
    }
}