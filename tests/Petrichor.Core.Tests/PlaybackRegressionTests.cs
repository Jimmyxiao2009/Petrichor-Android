using System;
using Petrichor.Core;
internal static class PlaybackRegressionTests
{
    public static void Run(Action<bool,string> check)
    {
        check(PlaybackCompletion.IsPremature(90000,240000), "Mid-song EOF must not advance the queue");
        check(PlaybackCompletion.IsPremature(0,240000), "An immediate stream disconnect is not a completed song");
        check(!PlaybackCompletion.IsPremature(239000,240000), "Codec end timestamp rounding is accepted");
        check(!PlaybackCompletion.IsPremature(240000,240000), "A fully played song advances normally");
        check(!PlaybackCompletion.IsPremature(30000,30000), "A completed preview uses its native clip duration");
        check(!PlaybackCompletion.IsPremature(1000,0), "Unknown duration does not cause an endless recovery loop");
        var queue = new PlaybackQueue { Repeat = RepeatMode.One };
        queue.Replace(new[] { new Track { Id = 1 }, new Track { Id = 2 } }, 1);
        check(queue.MoveNext(true) && queue.Current.Id == 1, "Manual next wraps at the end during repeat-one");
        check(queue.MovePrevious() && queue.Current.Id == 2, "Manual previous wraps at the start during repeat-one");
        check(queue.MoveNext(false) && queue.Current.Id == 2, "Natural repeat-one completion keeps the current track");
    }
}