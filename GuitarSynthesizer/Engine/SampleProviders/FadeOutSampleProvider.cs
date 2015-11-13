using System;
using NAudio.Wave;

namespace GuitarSynthesizer.Engine.SampleProviders
{
    public class FadeOutSampleProvider : ISampleProvider
    {
        private long FadeAfterPosition { get; }
        private int FadeSamples { get; }
        private int FadeSamplesRemaining { get; set; }
        private ISampleProvider Source { get; }

        private long Position { get; set; }

        public FadeOutSampleProvider(ISampleProvider source, float fadeAfter, float fadeDuration)
        {
            if(source.WaveFormat.Channels != 1)
            {
                throw new NotSupportedException("Supported only 1 channel Sample providers");
            }

            Source = source;
            FadeAfterPosition = (long)(fadeAfter * WaveFormat.AverageBytesPerSecond);
            FadeSamples = (int)(fadeDuration * WaveFormat.AverageBytesPerSecond);
            FadeSamplesRemaining = FadeSamples;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if(FadeSamplesRemaining <= 0)
                return 0;

            var samplesToRead = (int)Math.Min(count, FadeAfterPosition + FadeSamples - Position + 1);

            var readed = Source.Read(buffer, offset, samplesToRead);
            Position += readed;

            if(Position > FadeAfterPosition)
            {
                int startIndex = (int)Math.Max(0, readed - (Position - FadeAfterPosition));
                for(int index = startIndex; index < readed; index++)
                {
                    var fadeMultiplier = FadeSamplesRemaining / (float)FadeSamples;
                    buffer[offset + index] *= fadeMultiplier;
                    --FadeSamplesRemaining;
                }
            }

            return readed;
        }

        public WaveFormat WaveFormat => Source.WaveFormat;
    }
}
