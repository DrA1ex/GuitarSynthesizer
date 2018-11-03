using NAudio.Wave;

namespace GuitarSynthesizer.Engine.BankImpl
{
    public class DrumkitMediaBank : MediaBankGuitarBase
    {
        public DrumkitMediaBank(WaveFormat targetFormat) : base(targetFormat)
        {
        }

        protected override string SearchPath => "media\\Drumkit";

        protected override string SearchPattern => "*.wav";

        protected override Note StartNote => new Note(0, Tones.F);
    }
}