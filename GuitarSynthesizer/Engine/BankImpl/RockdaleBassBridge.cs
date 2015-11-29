using NAudio.Wave;

namespace GuitarSynthesizer.Engine.BankImpl
{
    public class RockdaleBassBridge : MediaBankGuitarBase
    {
        public RockdaleBassBridge(WaveFormat targetFormat) : base(targetFormat)
        {
        }

        protected override string SearchPath => "media\\Rockdale_Bass_Bridge";

        protected override string SearchPattern => "*.wav";

        protected override Note StartNote => new Note(1, Tones.E);
    }
}
