using System;
using System.Globalization;
using System.Linq;

namespace GuitarSynthesizer.Engine
{
    public enum Tones
    {
        A = 9,
        Ad = 10,
        B = 11,
        C = 0,
        Cd = 1,
        D = 2,
        Dd = 3,
        E = 4,
        F = 5,
        Fd = 6,
        G = 7,
        Gd = 8
    }

    public struct Note
    {
        public Note(byte oct, Tones t)
            : this()
        {
            Octave = oct;
            Tone = t;

            Id = 12 + Octave*12 + (int) Tone;
        }

        public int Id { get; }
        public byte Octave { get; }
        public Tones Tone { get; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is Note && Equals((Note) obj);
        }

        public override int GetHashCode()
        {
            return Id;
        }

        public static Note FromString(string str)
        {
            byte octave = byte.Parse(str.Last().ToString(CultureInfo.InvariantCulture));
            var tone = str.Substring(0, str.Length - 1).Replace('#', 'd').ConvertToEnum<Tones>();

            return new Note(octave, tone);
        }

        public static Note FromId(int id)
        {
            return new Note((byte) (id/12 - 1), (Tones) (id%12));
        }

        public bool IsSharp()
        {
            return Tone.ToString().Contains('d');
        }

        #region Operators Defenition

        public static int operator -(Note note1, Note note2)
        {
            return Math.Abs(note1.Id - note2.Id);
        }

        public static Note operator +(Note note, int semitons)
        {
            var octave = (byte) (note.Octave + semitons/12); // 12 полутонов в октаве
            int tmp = (int) note.Tone + semitons%12;
            if (tmp > (int) Tones.B) // Последняя нота в октаве
            {
                ++octave;
                tmp = tmp%12;
            }
            var tone = (Tones) (tmp);

            return new Note(octave, tone);
        }

        public static bool operator <(Note note1, Note note2)
        {
            return note1.Id < note2.Id;
        }

        public static bool operator >(Note note1, Note note2)
        {
            return note1.Id > note2.Id;
        }

        public static bool operator >=(Note note1, Note note2)
        {
            return note1.Id >= note2.Id;
        }

        public static bool operator <=(Note note1, Note note2)
        {
            return note1.Id <= note2.Id;
        }

        public static bool operator ==(Note note1, Note note2)
        {
            return note1.Id == note2.Id;
        }

        public static bool operator !=(Note note1, Note note2)
        {
            return note1.Id != note2.Id;
        }

        public override string ToString()
        {
            return Tone.ToString().Replace('d', '#') + Octave;
        }

        public bool Equals(Note other)
        {
            return Id == other.Id;
        }

        #endregion
    }
}