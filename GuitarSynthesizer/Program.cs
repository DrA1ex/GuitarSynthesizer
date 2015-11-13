using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using GuitarSynthesizer.Engine.BankImpl;
using GuitarSynthesizer.Engine.SampleProviders;
using GuitarSynthesizer.Helpers;
using GuitarSynthesizer.Model;
using GuitarSynthesizer.Utils;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GuitarSynthesizer
{
    internal class Program
    {
        private static void Main(string[] arg)
        {
            //File format:
            //<tempo in bpm>
            //[<note>_<note>..._<note>]<duration><space>[<note>_<note>..._<note>]<duration><space>[etc]
            //where note is a tone name (e.g. Eb, Gd, F) and octave number (e.g. E2, Eb3 etc)
            //      duration (w,h,q,e,s,t,l) -- whole note, half note, etc
            //      also duration may includes points (.)

            IEnumerable<Phrase[]> tracks;
            string songStr = "C5s C#5s D5h q. G3_G4_B4q D5_F5q q G3_D5_F5q G3_D5_F5e e. A3_C5_E5e. e B3_B4_D5e. e. C4_G4_C5q E4q G3q E4q C3_C4w ";
            int tempo = 400;

            if(arg.Length < 1 || !File.Exists(arg[0]))
            {
                Console.WriteLine("Song file missing :(");
                Console.WriteLine();
                Console.WriteLine("Usage: {0}.exe <song file path> [-e <file name>]", Process.GetCurrentProcess().ProcessName);
                Console.WriteLine("Options: -e <file name.wav> Export track as wave file");
                tracks = new[] { ParseUtils.ParseString(songStr).ToArray() };
            }
            else
            {
                string fileExtension = Path.GetExtension(arg[0]);

                if(String.Equals(fileExtension, ".mid", StringComparison.OrdinalIgnoreCase) || String.Equals(fileExtension, ".midi", StringComparison.OrdinalIgnoreCase))
                {
                    tracks = ParseUtils.ParseMidi(arg[0], out tempo);
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

                    tracks = new[] { ParseUtils.ParseString(songStr).ToArray() };
                }

                Console.WriteLine("Song: {0}",
                    (Path.GetFileNameWithoutExtension(arg[0]) ?? String.Empty).ToUpper());
            }

            try
            {
                Console.WriteLine();
                if(arg.Length >= 3 && arg[1] == "-e")
                {
                    var filePath = arg[2];

                    ParseUtils.SaveSong(tracks, tempo, filePath);
                    Console.WriteLine("Export finished");
                }
                else
                {
                    PlaySong(tracks, tempo);
                }
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

        public static void PlaySong(IEnumerable<Phrase[]> tracks, int tempo)
        {
            var enumerator = new MMDeviceEnumerator();
            MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(defaultDevice.AudioClient.MixFormat.SampleRate, 1);

            var wasapiOut = new WasapiOut(AudioClientShareMode.Shared, false, 60);
            var bank = new FenderStratCleanB(waveFormat);

            var mixer = new MixingSampleProvider(waveFormat);

            var trackSampleProviders = tracks.Select(t => new TrackSampleProvider(bank, t, tempo)).ToArray();
            foreach(var track in trackSampleProviders)
            {
                mixer.AddMixerInput(track);
            }

            wasapiOut.Init(new VolumeSampleProvider(mixer)
            {
                Volume = 0.7f
            });

            PrintUtils.PrintHeaderOfTable();
            PrintUtils.PrintContentTable("NOTES", "DURATION", "COMMAND");
            PrintUtils.PrintRowDividerTable();

            if(trackSampleProviders.Any())
            {
                trackSampleProviders.OrderByDescending(t => t.Phrases.Length).First().OnPhrasePlaying += (sender, phrase) =>
                {
                    PrintUtils.PrintContentTable(phrase.Notes != null && phrase.Notes.Length > 0
                        ? String.Join(",", phrase.Notes)
                        : "NONE"
                        , (int)(phrase.GetPhraseSeconds(tempo) * 1000)
                        , phrase.Command);
                };
            }

            wasapiOut.Play();

            var resetEvent = new ManualResetEvent(false);

            wasapiOut.PlaybackStopped += (sender, args) =>
            {
                resetEvent.Set();
                if(args.Exception != null)
                {
                    throw args.Exception;
                }
            };

            resetEvent.WaitOne();
            PrintUtils.PrintFooterOfTable();
        }
    }
}