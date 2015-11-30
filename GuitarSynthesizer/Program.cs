﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CommandLine;
using GuitarSynthesizer.Engine;
using GuitarSynthesizer.Engine.BankImpl;
using GuitarSynthesizer.Engine.SampleProviders;
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

            var result = Parser.Default.ParseArguments<Options>(arg);
            Options options = result.MapResult(o => o, errors =>
            {
                foreach(var error in errors)
                {
                    Console.Error.WriteLine(error);
                }
                return null;
            });

            IEnumerable<Track> tracks;
            string songName = "Demo song";
            string songStr = "C5s C#5s D5h q. G3_G4_B4q D5_F5q q G3_D5_F5q G3_D5_F5e e. A3_C5_E5e. e B3_B4_D5e. e. C4_G4_C5q E4q G3q E4q C3_C4w ";
            int tempo = 400;

            var track = ParseUtils.ParseString(songStr);
            track.Tempo = tempo;
            tracks = new[] { track };

            if(options != null)
            {
                string fileExtension = Path.GetExtension(options.InputFileName);
                if(String.Equals(fileExtension, ".mid", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(fileExtension, ".midi", StringComparison.OrdinalIgnoreCase))
                {
                    tracks = ParseUtils.ParseMidi(options.InputFileName);
                }
                else
                {
                    string[] lines = File.ReadAllLines(options.InputFileName);

                    if(lines.Length >= 2)
                    {
                        songStr = lines[1];
                        tempo = int.Parse(lines[0]);
                        track = ParseUtils.ParseString(songStr);
                        track.Tempo = tempo;
                        tracks = new[] { track };
                    }
                    else
                    {
                        AsyncConsole.WriteLine("File invalid :(");
                    }
                }

                songName = Path.GetFileNameWithoutExtension(options.InputFileName) ?? songName;
            }

            try
            {
                AsyncConsole.WriteLine("SONG: {0}", songName.ToUpper());
                if(!String.IsNullOrWhiteSpace(options?.ExportFileName))
                {
                    ParseUtils.SaveSong(tracks, options.ExportFileName);
                    AsyncConsole.WriteLine("Export finished");
                }
                else
                {
                    PlaySong(tracks);
                }
            }
            catch(AggregateException e)
            {
                AsyncConsole.WriteLine();
                string errorMesssage = e.InnerException != null ? string.Join(Environment.NewLine, e.InnerExceptions.Select(c => c.Message)) : e.Message;
                AsyncConsole.WriteLine("Error happened: {0}", errorMesssage);
            }
            catch(Exception e)
            {
                AsyncConsole.WriteLine();
                string errorMesssage = e.InnerException?.Message ?? e.Message;
                AsyncConsole.WriteLine("Error happened: {0}", errorMesssage);
            }

            AsyncConsole.WriteLine();
            AsyncConsole.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        public static void PlaySong(IEnumerable<Track> tracks)
        {
            var enumerator = new MMDeviceEnumerator();
            MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(defaultDevice.AudioClient.MixFormat.SampleRate, 1);

            var wasapiOut = new WasapiOut(AudioClientShareMode.Shared, false, 60);
            MediaBankBase bank = new FenderStratCleanB(waveFormat);
            MediaBankBase bankBass = new RockdaleBassBridge(waveFormat);

            var mixer = new MixingSampleProvider(waveFormat);

            var trackSampleProviders = tracks.Select(t => new TrackSampleProvider(t.Patch == MediaPatch.CleanGuitar ? bank : bankBass, t)).ToArray();
            var playedTracks = new List<int>();

            foreach(var track in trackSampleProviders)
            {
                track.OnPhrasePlaying += (sender, args) =>
                {
                    var channel = args.Track.Channel;
                    var phrase = args.Phrase;

                    if(playedTracks.Contains(channel))
                    {
                        AsyncConsole.WriteLine();
                        PrintUtils.PrintContentTable();

                        playedTracks.Clear();
                    }

                    PrintUtils.PrintContent(phrase.Notes != null && phrase.Notes.Length > 0
                        ? String.Join(",", phrase.Notes)
                        : phrase.Command.ToString(), channel);

                    playedTracks.Add(channel);
                };
                mixer.AddMixerInput(track);
            }

            wasapiOut.Init(new VolumeSampleProvider(mixer)
            {
                Volume = 0.7f
            });

            PrintUtils.Init(trackSampleProviders.Length);

            PrintUtils.PrintHeaderOfTable();
            PrintUtils.PrintRowDividerTable();
            PrintUtils.PrintContentTable();

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
            Console.WriteLine();
            PrintUtils.PrintFooterOfTable();
        }
    }
}