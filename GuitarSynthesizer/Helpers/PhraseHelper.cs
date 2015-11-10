using GuitarSynthesizer.Model;
using NAudio.Wave;

namespace GuitarSynthesizer.Helpers
{
    static class PhraseHelper
    {
        public const float BaseTempo = 60.0f;

        public static int GetPhraseBytes(this Phrase phrase, int tempo, WaveFormat waveFormat)
        {
            return (int)(phrase.GetPhraseSeconds(tempo) * waveFormat.AverageBytesPerSecond);
        }

        public static float GetPhraseSeconds(this Phrase phrase, int tempo)
        {
            var barDuration = BaseTempo / tempo; //seconds
            return barDuration * phrase.Duration;
        }
    }
}
