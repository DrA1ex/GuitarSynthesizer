using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GuitarSynthesizer.Engine.SampleProviders
{
    public class DelayedMixingSampleProvider : ISampleProvider
    {
        private MixingSampleProvider MixingSampleProvider { get; }
        public DelayedMixingSampleProvider(IEnumerable<ISampleProvider> providers, float delay, bool reverse)
        {
            float currentDelay = -delay;
            var sampleProviders = reverse ? providers.Reverse() : providers;
            sampleProviders = sampleProviders.Select(c => new OffsetSampleProvider(c)
            {
                DelayBy = TimeSpan.FromSeconds(currentDelay += delay)
            });

            MixingSampleProvider = new MixingSampleProvider(sampleProviders);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return MixingSampleProvider.Read(buffer, offset, count);
        }

        public WaveFormat WaveFormat => MixingSampleProvider.WaveFormat;
    }
}
