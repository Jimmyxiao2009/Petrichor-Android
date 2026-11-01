namespace Petrichor.Core
{
    public static class PlaybackCompletion
    {
        // Allow codec timestamp rounding, but never treat a truncated stream as
        // the end of a song. Callers use the native duration for preview clips.
        public static bool IsPremature(long positionMs, long durationMs)
        {
            return durationMs > 0 && positionMs >= 0 && positionMs < durationMs - 1500;
        }
    }
}