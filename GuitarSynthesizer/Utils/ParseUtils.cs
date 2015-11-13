using System;
using System.Collections.Generic;
using System.Linq;
using GuitarSynthesizer.Engine;
using GuitarSynthesizer.Engine.BankImpl;
using GuitarSynthesizer.Engine.SampleProviders;
using GuitarSynthesizer.Model;
using NAudio.Midi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GuitarSynthesizer.Utils
{
    internal class ParseUtils
    {
        private static Dictionary<char, float> _durations;

        private static Dictionary<char, float> Durations => _durations ?? (_durations =
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

        private const float WholeNoteDuration = 1920.0f;

        public static void SaveSong(IEnumerable<Phrase[]> tracks, int tempo, string filePath)
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
            var bank = new FenderStratCleanB(waveFormat);

            var mixer = new MixingSampleProvider(waveFormat);

            foreach(var track in tracks)
            {
                var trackSampleProvider = new TrackSampleProvider(bank, track, tempo);
                var resultingSampleProvider = new VolumeSampleProvider(trackSampleProvider)
                {
                    Volume = 0.7f
                };

                mixer.AddMixerInput(resultingSampleProvider);
            }

            WaveFileWriter.CreateWaveFile(filePath, new SampleToWaveProvider(mixer));
        }

        public static IEnumerable<Phrase> ParseString(string songStr)
        {
            var phrases = new List<Phrase>();

            if(!String.IsNullOrWhiteSpace(songStr))
            {
                var noteModifier = PlayingCommand.None;

                foreach(string token in songStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if(token.StartsWith("=")) //command
                    {
                        var command = new Phrase(0, PlayingCommand.None);

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

                            var phrase = new Phrase(0, phraseDuration, notes.Distinct().ToArray()) { Command = noteModifier };
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

        public static IEnumerable<Phrase[]> ParseMidi(string fileName, out int tempo)
        {
            var tracks = new List<Phrase[]>();
            tempo = 120;

            var file = new MidiFile(fileName);
            for(int trackNumber = 0; trackNumber < file.Tracks; ++trackNumber)
            {
                IList<MidiEvent> events = file.Events[trackNumber];
                var tempoEvent = events.FirstOrDefault(c => c is TempoEvent) as TempoEvent;

                if(tempoEvent != null)
                {
                    tempo = (int)tempoEvent.Tempo;
                }
            }

            var channel = 0;
            for(int trackNumber = 0; trackNumber < file.Tracks; ++trackNumber, ++channel)
            {
                var phrases = new List<Phrase>();

                IList<MidiEvent> events = file.Events[trackNumber];
                var path = events.FirstOrDefault(c => c is PatchChangeEvent) as PatchChangeEvent;
                if(path != null && PatchChangeEvent.GetPatchName(path.Patch) == "Steel Drums")
                {
                    continue;
                }

                IEnumerable<IGrouping<long, NoteOnEvent>> notes =
                    events.OfType<NoteOnEvent>().GroupBy(c => c.AbsoluteTime);

                long lastTime = 0;
                foreach(var noteCollection in notes)
                {
                    if(noteCollection.Key - lastTime > 0)
                    {
                        long pauseTime = noteCollection.Key - lastTime;

                        phrases.Add(new Phrase(channel, pauseTime / WholeNoteDuration));
                    }

                    int duration = noteCollection.Max(c => c.NoteLength);
                    var phrase = new Phrase
                    {
                        Duration = duration / WholeNoteDuration,
                        Notes = noteCollection.Select(c => Note.FromId(c.NoteNumber)).ToArray(),
                        Channel = channel
                    };

                    lastTime = noteCollection.Key + duration;

                    phrases.Add(phrase);
                }

                tracks.Add(phrases.ToArray());
            }

            return tracks;
        }
    }
}
