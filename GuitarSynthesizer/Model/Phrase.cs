using GuitarSynthesizer.Engine;

namespace GuitarSynthesizer.Model
{
    internal struct Phrase
    {
        public Phrase(int channel, float duration, params Note[] notes)
            : this()
        {
            Channel = channel;
            Notes = notes;
            Duration = duration;
            Command = PlayingCommand.None;
        }

        public Phrase(int channel,PlayingCommand command)
            : this()
        {
            Channel = channel;
            Command = command;
        }


        public int Channel { get; set; }
        public Note[] Notes { get; set; }
        public float Duration { get; set; }
        public PlayingCommand Command { get; set; }
    }
}