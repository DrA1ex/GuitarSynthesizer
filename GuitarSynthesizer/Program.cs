using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GuitarSynthesizer.Engine;
using GuitarSynthesizer.Engine.BankImpl;
using NAudio.Midi;
using NAudio.Wave;

namespace GuitarSynthesizer
{
    internal enum PlayingCommand
    {
        None,
        LetItRingOn,
        LetItRingOff,
        LetRingNotes,
        LetRingWhole,
        LetRingHalf,
        LetRingQuarter
    }

    internal struct Phrase
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

    internal class Program
    {
        private const float Metre = 4; //4 quarter notes per beat
        private static Dictionary<string, RawSourceWaveStream> _mediaBank;

        private static Dictionary<char, float> _durations;
        private static readonly object SyncDummy = new object();

        private static readonly SynchronizationContext SynchronizationContext = SynchronizationContext.Current ?? new SynchronizationContext();

        public static Dictionary<string, RawSourceWaveStream> MediaBank => 
            _mediaBank ?? (_mediaBank = new Dictionary<string, RawSourceWaveStream>());

        public static Dictionary<char, float> Durations => _durations ?? (_durations =
            new Dictionary<char, float>
            {
                {'w', 1.0f},
                {'h', 1.0f / 2.0f},
                {'q', 1.0f / 4.0f},
                {'e', 1.0f / 8.0f},
                {'s', 1.0f / 16.0f},
                {'t', 1.0f / 32.0f},
                {'l', 1.0f / 64.0f},
            });

        private static void Main(string[] arg)
        {
            //File format:
            //<tempo in bpm>
            //[<note>_<note>..._<note>]<duration><space>[<note>_<note>..._<note>]<duration><space>[etc]
            //where note is a tone name (e.g. Eb, Gd, F) and octave number (e.g. E2, Eb3 etc)
            //      duration (w,h,q,e,s,t,l) -- whole note, half note, etc
            //      also duration may includes points (.)

            IEnumerable<Phrase> phrases;
            string songStr = "C5s C#5s D5h q. G3_G4_B4q D5_F5q q G3_D5_F5q G3_D5_F5e e. A3_C5_E5e. e B3_B4_D5e. e. C4_G4_C5q E4q G3q E4q C3_C4w ";
            int tempo = 400;

            if(arg.Length < 1 || !File.Exists(arg[0]))
            {
                Console.WriteLine("Song file missing :(");
                Console.WriteLine();
                Console.WriteLine("Usage: {0}.exe <song file path>", Process.GetCurrentProcess().ProcessName);
                phrases = ParseString(songStr);
            }
            else
            {
                string fileExtension = Path.GetExtension(arg[0]);

                if(String.Equals(fileExtension, ".mid", StringComparison.OrdinalIgnoreCase) || String.Equals(fileExtension, ".midi", StringComparison.OrdinalIgnoreCase))
                {
                    phrases = ParseMidi(arg[0], out tempo);
                }
                else
                {
                    string[] lines = File.ReadAllLines(arg[0]);

                    if(lines.Length >= 2)
                    {
                        songStr = lines[1];
                        tempo = int.Parse(lines[0]);
                    }
                    else
                    {
                        Console.WriteLine("File invalid :(");
                    }

                    phrases = ParseString(songStr);
                }

                Console.WriteLine("Song: {0}",
                    (Path.GetFileNameWithoutExtension(arg[0]) ?? String.Empty).ToUpper());
            }

            try
            {
                Console.WriteLine();
                Task.Run(() => PlayString(phrases, tempo)).Wait();
            }
            catch(AggregateException e)
            {
                Console.WriteLine();
                string errorMesssage = e.InnerException != null ? string.Join(Environment.NewLine, e.InnerExceptions.Select(c => c.Message)) : e.Message;
                Console.WriteLine("Error happened: {0}", errorMesssage);
            }
            catch(Exception e)
            {
                Console.WriteLine();
                string errorMesssage = e.InnerException?.Message ?? e.Message;
                Console.WriteLine("Error happened: {0}", errorMesssage);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        private static IEnumerable<Phrase> ParseString(string songStr)
        {
            var phrases = new List<Phrase>();

            if(!String.IsNullOrWhiteSpace(songStr))
            {
                var noteModifier = PlayingCommand.None;

                foreach(string token in songStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if(token.StartsWith("=")) //command
                    {
                        var command = new Phrase(PlayingCommand.None);

                        switch(token)
                        {
                            case "=LROn=":
                                command.Command = PlayingCommand.LetItRingOn;
                                break;

                            case "=LROff=":
                                command.Command = PlayingCommand.LetItRingOff;
                                break;

                            case "=LRw=":
                                command.Command = PlayingCommand.LetRingWhole;
                                break;

                            case "=LRh=":
                                command.Command = PlayingCommand.LetRingHalf;
                                break;

                            case "=LRq=":
                                command.Command = PlayingCommand.LetRingQuarter;
                                break;

                            case "=LR=":
                                noteModifier = PlayingCommand.LetRingNotes;
                                continue;

                            default:
                                throw new Exception($"Unknown command: {token}");
                        }

                        phrases.Add(command);
                    }
                    else
                    {
                        try
                        {
                            int dots = token.Count(c => c == '.');
                            char duration = token[token.Length - 1 - dots];
                            float phraseDuration = Durations.ContainsKey(duration) ? Durations[duration] : Durations['w'];
                            if(dots > 0)
                            {
                                float tempDuration = phraseDuration;
                                for(int i = 0; i < dots; i++)
                                {
                                    tempDuration /= 2;
                                    phraseDuration += tempDuration;
                                }
                            }

                            string notesString = token.Substring(0, token.Length - 1 - dots);
                            IEnumerable<Note> notes = notesString.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries).Select(Note.FromString);

                            var phrase = new Phrase(phraseDuration, notes.Distinct().ToArray()) { Command = noteModifier };
                            noteModifier = PlayingCommand.None;

                            phrases.Add(phrase);
                        }
                        catch(Exception)
                        {
                            throw new Exception($"Unable to parse phrase: {token}");
                        }
                    }
                }
            }


            return phrases;
        }

        private static IEnumerable<Phrase> ParseMidi(string fileName, out int tempo)
        {
            var phrases = new List<Phrase>();
            tempo = 120;

            var file = new MidiFile(fileName);
            if(file.Tracks > 0)
            {
                IList<MidiEvent> events = file.Events[0];
                var tempoEvent = events.FirstOrDefault(c => c is TempoEvent) as TempoEvent;
                if(tempoEvent != null)
                {
                    tempo = (int)tempoEvent.Tempo;
                }

                IEnumerable<IGrouping<long, NoteOnEvent>> notes = events.OfType<NoteOnEvent>().GroupBy(c => c.AbsoluteTime);

                // ReSharper disable PossibleMultipleEnumeration
                long lastTime = notes.First().Key;
                foreach(var noteCollection in notes)
                {
                    if(noteCollection.Key - lastTime > 0)
                    {
                        long pauseTime = noteCollection.Key - lastTime;

                        phrases.Add(new Phrase(pauseTime / WholeNoteDuration));
                    }

                    int duration = noteCollection.Max(c => c.NoteLength);
                    var phrase = new Phrase
                                 {
                                     Duration = duration / WholeNoteDuration,
                                     Notes = noteCollection.Select(c => Note.FromId(c.NoteNumber)).ToArray()
                                 };

                    lastTime = noteCollection.Key + duration;

                    phrases.Add(phrase);
                }
                // ReSharper restore PossibleMultipleEnumeration
            }

            return phrases;
        }

        public static void PlayString(IEnumerable<Phrase> phrases, int tempo)
        {
            lock(SyncDummy)
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;

                using(var engine = new AudioEngine())
                {
                    engine.MediaBank = new FenderStratCleanB(engine.OutWaveFormat);
                    float barDuration = 60.0f / tempo * Metre * 1000; //in ms
                    engine.LetItRingFadeTime = TimeSpan.FromMilliseconds(barDuration);

                    SynchronizationContext.Post(state =>
                                                {
                                                    PrintHeaderOfTable();
                                                    PrintContentTable("NOTE", "DURATION", "COMMAND");
                                                    PrintRowDividerTable();
                                                }, null);


                    foreach(Phrase phrase in phrases)
                    {
                        Phrase copyOfPhrase = phrase;
                        SynchronizationContext.Post(state => PrintContentTable(copyOfPhrase.Notes != null && copyOfPhrase.Notes.Length > 0
                            ? String.Join(",", copyOfPhrase.Notes)
                            : "NONE"
                            , (int)(copyOfPhrase.Duration * barDuration)
                            , copyOfPhrase.Command), null);


                        switch(phrase.Command)
                        {
                            case PlayingCommand.None:
                                break;
                            case PlayingCommand.LetItRingOn:
                                engine.LetItRing = true;
                                break;
                            case PlayingCommand.LetItRingOff:
                                engine.LetItRing = false;
                                break;

                            case PlayingCommand.LetRingWhole:
                                engine.LetItRingFadeTime = TimeSpan.FromMilliseconds(barDuration);
                                break;
                            case PlayingCommand.LetRingHalf:
                                engine.LetItRingFadeTime = TimeSpan.FromMilliseconds(barDuration / 2);
                                break;
                            case PlayingCommand.LetRingQuarter:
                                engine.LetItRingFadeTime = TimeSpan.FromMilliseconds(barDuration / 4);
                                break;
                        }

                        if(phrase.Notes != null)
                        {
                            foreach(Note note in phrase.Notes)
                            {
                                engine.PlayNote(note);
                            }

                            var notePlayingTime = (int)(barDuration * phrase.Duration);
                            Thread.Sleep(notePlayingTime);

                            foreach(Note note in phrase.Notes)
                            {
                                if(phrase.Command == PlayingCommand.LetRingNotes)
                                {
                                    engine.StopFaded(note);
                                }
                                else
                                {
                                    engine.StopNote(note);
                                }
                            }
                        }
                    }

                    SynchronizationContext.Post(state => PrintFooterOfTable(), null);

                    Thread.Sleep(1000);
                }
            }
        }

        #region Table Drawing

        private const int FirstColumnLength = 32;
        private const int SecondColumnLength = 20;
        private const int ThirdColumnLength = 22;

        private const string ContentPattern = "{{{0},-{1}}}";
        private const string HeaderPattern = "┌{0}┬{1}┬{2}┐";
        private const string RowDividerPattern = "├{0}┼{1}┼{2}┤";
        private const string FooterPattern = "└{0}┴{1}┴{2}┘";
        private const char HorizontalLineSymbol = '─';
        private const float WholeNoteDuration = 1920.0f;

        private static readonly string ContentLinePattern =
            $"│{String.Format(ContentPattern, 0, FirstColumnLength)}" +
            $"│{String.Format(ContentPattern, 1, SecondColumnLength)}" +
            $"│{String.Format(ContentPattern, 2, ThirdColumnLength)}│";

        private static void PrintHeaderOfTable()
        {
            Console.WriteLine(HeaderPattern
                , new string(HorizontalLineSymbol, FirstColumnLength)
                , new string(HorizontalLineSymbol, SecondColumnLength)
                , new string(HorizontalLineSymbol, ThirdColumnLength));
        }

        private static void PrintContentTable(object first, object second, object third)
        {
            Console.WriteLine(ContentLinePattern, first, second, third);
        }


        private static void PrintRowDividerTable()
        {
            Console.WriteLine(RowDividerPattern
                , new string(HorizontalLineSymbol, FirstColumnLength)
                , new string(HorizontalLineSymbol, SecondColumnLength)
                , new string(HorizontalLineSymbol, ThirdColumnLength));
        }

        private static void PrintFooterOfTable()
        {
            Console.WriteLine(FooterPattern
                , new string(HorizontalLineSymbol, FirstColumnLength)
                , new string(HorizontalLineSymbol, SecondColumnLength)
                , new string(HorizontalLineSymbol, ThirdColumnLength));
        }

        #endregion
    }
}