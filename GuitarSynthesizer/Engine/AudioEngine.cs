using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GuitarSynthesizer.Engine
{
    public class AudioEngine : IDisposable
    {
        private const int DesiredLatency = 20; //ms
        private const float FadeOutTime = 0.06f; //seconds
        private Dictionary<Note, Tuple<ISampleProvider, AdsrSampleProvider>> _playingSamples;
        private Random _randomGenerator;
        private Dictionary<Note, CancellationTokenSource> _ringingNoteFadeTasks;

        public AudioEngine()
        {
            var enumerator = new MMDeviceEnumerator();
            MMDevice defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            OutWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(defaultDevice.AudioClient.MixFormat.SampleRate, 1);

            WasapiOut = new WasapiOut(AudioClientShareMode.Shared, false, DesiredLatency);
            Mixer = new MixingSampleProvider(OutWaveFormat) { ReadFully = true };

            WasapiOut.Init(Mixer);
            WasapiOut.Play();

            SynchronizationContext = new SynchronizationContext();
        }

        public TimeSpan LetItRingFadeTime { get; set; } = TimeSpan.FromMilliseconds(600);

        public SynchronizationContext SynchronizationContext { get; set; }

        public Dictionary<Note, CancellationTokenSource> RingingNoteFadeTasks 
            => _ringingNoteFadeTasks ?? (_ringingNoteFadeTasks = new Dictionary<Note, CancellationTokenSource>());

        private WasapiOut WasapiOut { get; set; }
        private MixingSampleProvider Mixer { get; }

        private Dictionary<Note, Tuple<ISampleProvider, AdsrSampleProvider>> PlayingSamples 
            => _playingSamples ?? (_playingSamples = new Dictionary<Note, Tuple<ISampleProvider, AdsrSampleProvider>>());

        public MediaBankBase MediaBank { get; set; }

        public bool LetItRing { get; set; }
        public WaveFormat OutWaveFormat { get; }

        public Random RandomGenerator => _randomGenerator ?? (_randomGenerator = new Random());

        public void Dispose()
        {
            WasapiOut.Dispose();
            WasapiOut = null;
        }

        public void PlayNote(Note note)
        {
            if(MediaBank != null)
            {
                WaveStream sound = MediaBank.GetMedia(note);
                if(sound == null)
                {
                    return;
                }

                ISampleProvider sample = sound.ToSampleProvider();

                if(IsPlayingNote(note))
                {
                    StopNote(note, true, false);
                }

                sound.Seek(0, SeekOrigin.Begin);

                var volume = new VolumeSampleProvider(sample) { Volume = RandomGenerator.Next(85, 100) / 100.0f - 0.3f }; //+- 15% of volume and reduce gain to 30%
                var adsrSampleProvider = new AdsrSampleProvider(volume) { ReleaseSeconds = FadeOutTime + (RandomGenerator.Next(0, 20) - 10) / 1000.0f }; //+- 10ms
                Mixer.AddMixerInput(adsrSampleProvider);

                AddToPlayingSamples(note, sample, adsrSampleProvider);
            }
        }

        public void StopNote(Note note)
        {
            StopNote(note, false, false);
        }

        public void StopFaded(Note note)
        {
            StopNote(note, false, true);
        }

        private void StopNote(Note note, bool forceStop, bool fadeStop)
        {
            if(IsPlayingNote(note))
            {
                ISampleProvider sample;
                AdsrSampleProvider adsr;
                lock(PlayingSamples)
                {
                    Tuple<ISampleProvider, AdsrSampleProvider> playingSample = PlayingSamples[note];
                    sample = playingSample.Item1;
                    adsr = playingSample.Item2;
                }

                if((!LetItRing || forceStop) && !fadeStop)
                {
                    CancelRingingNoteFadeTask(note);
                    adsr.Stop();
                    RemoveFromPlayingSamples(note);

                    Task.Delay((int)(adsr.ReleaseSeconds * 1000)).ContinueWith(delegate { Mixer.RemoveMixerInput(sample); });
                }
                else if(LetItRing || fadeStop)
                {
                    var cts = new CancellationTokenSource();
                    Task.Delay(LetItRingFadeTime, cts.Token).ContinueWith(delegate
                                                                          {
                                                                              if(!cts.IsCancellationRequested)
                                                                              {
                                                                                  RemoveFromRingingNotes(note);
                                                                                  SynchronizationContext.Send(delegate { StopNote(note, true, false); }, null);
                                                                              }
                                                                          }, cts.Token);
                    AddToRingingNotes(note, cts);
                }
            }
        }

        private bool IsPlayingNote(Note note)
        {
            lock(PlayingSamples)
            {
                return PlayingSamples.ContainsKey(note);
            }
        }

        private void AddToPlayingSamples(Note note, ISampleProvider sample, AdsrSampleProvider adsr)
        {
            lock(PlayingSamples)
            {
                PlayingSamples.Add(note,
                    new Tuple<ISampleProvider, AdsrSampleProvider>(sample, adsr));
            }
        }

        private void RemoveFromPlayingSamples(Note note)
        {
            lock(PlayingSamples)
            {
                PlayingSamples.Remove(note);
            }
        }

        private void RemoveFromRingingNotes(Note note)
        {
            lock(RingingNoteFadeTasks)
            {
                RingingNoteFadeTasks.Remove(note);
            }
        }

        private void AddToRingingNotes(Note note, CancellationTokenSource cts)
        {
            lock(RingingNoteFadeTasks)
            {
                RingingNoteFadeTasks.Add(note, cts);
            }
        }

        private void CancelRingingNoteFadeTask(Note note)
        {
            lock(RingingNoteFadeTasks)
            {
                if(RingingNoteFadeTasks.ContainsKey(note))
                {
                    CancellationTokenSource cancelationToken = RingingNoteFadeTasks[note];
                    cancelationToken.Cancel(false);
                    RingingNoteFadeTasks.Remove(note);
                }
            }
        }
    }
}