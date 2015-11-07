using System;
using NAudio.Wave;

namespace GuitarSynthesizer.Engine.SampleProviders
{
    public class FadeOutSampleProvider : ISampleProvider
    {
        private long FadeAfterPosition { get; }
        private int FadeBytes { get; }
        private int FadeBytesRemaining { get; set; }
        private ISampleProvider Source { get; }

        private long Position { get; set; }

        public FadeOutSampleProvider(ISampleProvider source, float fadeAfter, float fadeDuration)
        {
            Source = source;
            FadeAfterPosition = (long)(fadeAfter * WaveFormat.AverageBytesPerSecond);
            FadeBytes = (int)(fadeDuration * WaveFormat.AverageBytesPerSecond);
            FadeBytesRemaining = FadeBytes;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if(FadeBytesRemaining <= 0)
                return 0;

            var bytesToRead = (int)Math.Min(count, FadeAfterPosition + FadeBytes - Position + 1);

            var readed = Source.Read(buffer, offset, bytesToRead);
            Position += readed;

            if(Position > FadeAfterPosition)
            {
                int startIndex = (int)Math.Max(0, readed - (Position - FadeAfterPosition));
                for(int index = startIndex; index < readed; index++)
                {
                    var fadeMultiplier = FadeBytesRemaining / (float)FadeBytes;
                    buffer[offset + index] *= fadeMultiplier;
                    --FadeBytesRemaining;
                }
            }

            return readed;
        }

        public WaveFormat WaveFormat => Source.WaveFormat;
    }
}
