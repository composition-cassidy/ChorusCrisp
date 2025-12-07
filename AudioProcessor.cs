using System;
using ScriptPortal.Vegas;

namespace ChorusCrisp
{
    public static class AudioProcessor
    {
        public const double MIN_SPLICE_TIME = 0.020;
        public const double MAX_SPLICE_TIME = 0.060;
        public const double MIN_DUCK_DB = 0.0;
        public const double MAX_DUCK_DB = -15.0;

        public static void ProcessEvent(TrackEvent originalEvent, double spliceTime, 
            double duckDb, double offsetPercent, CurveType fadeType)
        {
            Timecode eventLength = originalEvent.Length;
            Timecode spliceOffset = Timecode.FromSeconds(spliceTime);

            if (eventLength.ToMilliseconds() < spliceTime * 1000 + 10)
            {
                return;
            }

            TrackEvent secondEvent = originalEvent.Split(spliceOffset);

            if (secondEvent == null)
                return;

            Timecode overlapDuration = Timecode.FromSeconds(spliceTime * offsetPercent);

            AudioEvent audioSecond = secondEvent as AudioEvent;
            if (audioSecond != null)
            {
                Timecode newStart = secondEvent.Start - overlapDuration;
                
                foreach (Take take in audioSecond.Takes)
                {
                    take.Offset = take.Offset - overlapDuration;
                }
                
                secondEvent.Start = newStart;
                secondEvent.Length = secondEvent.Length + overlapDuration;
                
                double linearGain = Math.Pow(10.0, duckDb / 20.0);
                audioSecond.NormalizeGain = linearGain;
            }

            secondEvent.FadeIn.Length = overlapDuration;
            secondEvent.FadeIn.Curve = fadeType;

            originalEvent.FadeOut.Length = overlapDuration;
            originalEvent.FadeOut.Curve = fadeType;
        }

        public static double CalculateSpliceTime(double splicePercent)
        {
            return MIN_SPLICE_TIME + (splicePercent * (MAX_SPLICE_TIME - MIN_SPLICE_TIME));
        }

        public static double CalculateDuckDb(double crispPercent)
        {
            return MIN_DUCK_DB + (crispPercent * (MAX_DUCK_DB - MIN_DUCK_DB));
        }
    }
}
