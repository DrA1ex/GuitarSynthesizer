using NAudio.Wave;

namespace GuitarSynthesizer.Engine.BankImpl
{
    public class FenderStratCleanB : MediaBankGuitarBase
    {
        public FenderStratCleanB(WaveFormat targetFormat)
            : base(targetFormat)
        {
        }


        protected override string SearchPath => "media\\FenderStratCleanB";

        protected override string SearchPattern => "*.wav";

        protected override Note StartNote => new Note(2, Tones.E);
    }
}