using System.Collections.Generic;

namespace GuitarSynthesizer.Model
{
    public class Track
    {
        public MediaPatch Patch { get; set; }
        public IReadOnlyCollection<Phrase> Phrases { get; set; }
        public int Tempo { get; set; }
        public int Channel { get; set; }
    }
}
