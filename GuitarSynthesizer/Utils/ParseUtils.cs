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
        private const float WholeNoteDuration = 1920.0f;
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
                {'l', 1.0f / 64.0f}
            });

        public static void SaveSong(IEnumerable<Track> tracks, string filePath)
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
            MediaBankBase bank = new FenderStratCleanB(waveFormat);
            MediaBankBase bassBank = new RockdaleBassBridge(waveFormat);
            MediaBankBase drumkitBank = new DrumkitMediaBank(waveFormat);

            var mixer = new MixingSampleProvider(waveFormat);

            foreach(var track in tracks)
            {
                TrackSampleProvider trackSampleProvider;
                switch(track.Patch)
                {
                    case MediaPatch.CleanGuitar:
                        trackSampleProvider = new TrackSampleProvider(bank, track);
                        break;
                    case MediaPatch.Bass:
                        trackSampleProvider = new TrackSampleProvider(bassBank, track);
                        break;
                    case MediaPatch.Drums:
                        trackSampleProvider = new TrackSampleProvider(drumkitBank, track);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                var resultingSampleProvider = new VolumeSampleProvider(trackSampleProvider)
                {
                    Volume = 0.7f
                };

                mixer.AddMixerInput(resultingSampleProvider);
            }

            WaveFileWriter.CreateWaveFile(filePath, new SampleToWaveProvider(mixer));
        }

        public static Track ParseString(string songStr)
        {
            var phrases = new List<Phrase>();

            if(!string.IsNullOrWhiteSpace(songStr))
            {
                var noteModifier = PlayingCommand.None;

                foreach(var token in songStr.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries))
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
                            var dots = token.Count(c => c == '.');
                            var duration = token[token.Length - 1 - dots];
                            var phraseDuration = Durations.ContainsKey(duration) ? Durations[duration] : Durations['w'];
                            if(dots > 0)
                            {
                                var tempDuration = phraseDuration;
                                for(var i = 0; i < dots; i++)
                                {
                                    tempDuration /= 2;
                                    phraseDuration += tempDuration;
                                }
                            }

                            var notesString = token.Substring(0, token.Length - 1 - dots);
                            var notes = notesString.Split(new[] {'_'}, StringSplitOptions.RemoveEmptyEntries).Select(Note.FromString);

                            var phrase = new Phrase(phraseDuration, notes.Distinct().ToArray()) {Command = noteModifier};
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


            return new Track
            {
                Patch = MediaPatch.CleanGuitar, Phrases = phrases, Tempo = 60, Channel = 0
            };
        }

        public static IEnumerable<Track> ParseMidi(string fileName)
        {
            var tracks = new List<Track>();
            var file = new MidiFile(fileName);

            var tempo = 120;
            for(var trackNumber = 0; trackNumber < file.Tracks; ++trackNumber)
            {
                var events = file.Events[trackNumber];
                var tempoEvent = events.FirstOrDefault(c => c is TempoEvent) as TempoEvent;

                if(tempoEvent != null)
                {
                    tempo = (int)tempoEvent.Tempo;
                }
            }

            var channel = 0;
            for(var trackNumber = 0; trackNumber < file.Tracks; ++trackNumber, ++channel)
            {
                var phrases = new List<Phrase>();

                var events = file.Events[trackNumber];
                var path = events.FirstOrDefault(c => c is PatchChangeEvent) as PatchChangeEvent;
                if(path != null && PatchChangeEvent.GetPatchName(path.Patch) == "Steel Drums")
                {
                    continue;
                }

                var pathIndex = path?.Patch;
                var pathName = PatchChangeEvent.GetPatchName(pathIndex ?? 0);
                MediaPatch mediaPatch;
                if(pathIndex == 0)
                {
                    mediaPatch = MediaPatch.Drums;
                }
                else if(pathName.IndexOf("bass", StringComparison.InvariantCultureIgnoreCase) != -1)
                {
                    mediaPatch = MediaPatch.Bass;
                }
                else
                {
                    mediaPatch = MediaPatch.CleanGuitar;
                }

                var notes = events.OfType<NoteOnEvent>().GroupBy(c => c.AbsoluteTime);

                long lastTime = 0;
                foreach(var noteCollection in notes)
                {
                    if(noteCollection.Key - lastTime > 0)
                    {
                        var pauseTime = noteCollection.Key - lastTime;

                        phrases.Add(new Phrase(pauseTime / WholeNoteDuration));
                    }

                    var duration = noteCollection.Max(c => c.NoteLength);
                    var phrase = new Phrase
                    {
                        Duration = duration / WholeNoteDuration, Notes = noteCollection.Select(c => Note.FromId(c.NoteNumber)).ToArray()
                    };

                    lastTime = noteCollection.Key + duration;

                    phrases.Add(phrase);
                }

                tracks.Add(new Track
                {
                    Tempo = tempo, Patch = mediaPatch, Phrases = phrases.ToArray(), Channel = channel
                });
            }

            return tracks;
        }
    }
}