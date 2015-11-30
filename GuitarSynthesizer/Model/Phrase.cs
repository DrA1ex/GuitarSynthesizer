using GuitarSynthesizer.Engine;
using GuitarSynthesizer.Utils;

namespace GuitarSynthesizer.Model
{
    public struct Phrase
    {
        public Phrase(float duration, params Note[] notes)
            : this()
        {
            Notes = notes;
            Duration = duration;
            Command = PlayingCommand.None;
        }

        public Phrase(PlayingCommand command)
            : this()
        {
            Command = command;
        }


        public Note[] Notes { get; set; }
        public float Duration { get; set; }
        public PlayingCommand Command { get; set; }
    }
}