using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GuitarSynthesizer.Helpers;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GuitarSynthesizer.Engine.SampleProviders
{
    internal class TrackSampleProvider : ISampleProvider, IWavePosition
    {
        private const float DefaultFadeOutTime = 0.01f; //seconds

        public TrackSampleProvider(MediaBankBase mediaBank, IEnumerable<Phrase> phrases, int tempo)
        {
            SyncContext = SynchronizationContext.Current;

            WholeNoteFadeOutTime = PhraseHelper.BaseTempo / tempo;
            HalfNoteFadeOutTime = WholeNoteFadeOutTime / 2;
            QuarterNoteFadeOutTime = HalfNoteFadeOutTime / 2;
            CurrentLetRingFadeOut = QuarterNoteFadeOutTime;

            MediaBank = mediaBank;
            Phrases = new Queue<Phrase>(phrases);
            Tempo = tempo;

            Mixer = new MixingSampleProvider(MediaBank.TargetWaveFormat)
            {
                ReadFully = true
            };

            RemainingDelayBeforeStop = (int)(WholeNoteFadeOutTime * WaveFormat.AverageBytesPerSecond);
        }

        private SynchronizationContext SyncContext { get; }

        private float WholeNoteFadeOutTime { get; }
        private float HalfNoteFadeOutTime { get; }
        private float QuarterNoteFadeOutTime { get; }

        public MediaBankBase MediaBank { get; }
        public Queue<Phrase> Phrases { get; }
        public int Tempo { get; }

        private long Position { get; set; }

        private float CurrentLetRingFadeOut { get; set; }
        private bool LetRingEnabled { get; set; }

        private MixingSampleProvider Mixer { get; }

        private int RemainingBytes { get; set; }
        private int RemainingDelayBeforeStop { get; set; }

        public WaveFormat WaveFormat => MediaBank.TargetWaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            var written = 0;
            if(RemainingBytes > 0)
            {
                var readed = Math.Min(RemainingBytes, count);
                RemainingBytes -= readed;
                written += readed;
            }

            while(Phrases.Count > 0 && written < count)
            {
                var phrase = Phrases.Dequeue();
                TriggerPhrasePlaying(phrase);
                var letRingPhrase = false;

                switch(phrase.Command)
                {
                    case PlayingCommand.LetItRingOn:
                        LetRingEnabled = true;
                        break;
                    case PlayingCommand.LetItRingOff:
                        LetRingEnabled = false;
                        break;
                    case PlayingCommand.LetRingNotes:
                        letRingPhrase = true;
                        break;
                    case PlayingCommand.LetRingWhole:
                        CurrentLetRingFadeOut = WholeNoteFadeOutTime;
                        break;
                    case PlayingCommand.LetRingHalf:
                        CurrentLetRingFadeOut = HalfNoteFadeOutTime;
                        break;
                    case PlayingCommand.LetRingQuarter:
                        CurrentLetRingFadeOut = QuarterNoteFadeOutTime;
                        break;
                }

                RemainingBytes = phrase.GetPhraseBytes(Tempo, WaveFormat);
                if(phrase.Notes?.Any() ?? false)
                {
                    var phraseDuration = phrase.GetPhraseSeconds(Tempo);
                    var additionalDuration = LetRingEnabled || letRingPhrase
                        ? CurrentLetRingFadeOut
                        : 0;

                    var sampleProviders = phrase.Notes.Select(n => MediaBank.GetMedia(n)?.ToSampleProvider())
                        .Where(s => s != null).ToArray();

                    if(sampleProviders.Any())
                    {
                        var phraseSampleProvider = new FadeOutSampleProvider(new MixingSampleProvider(sampleProviders),
                            phraseDuration + additionalDuration, DefaultFadeOutTime);
                        Mixer.AddMixerInput(new OffsetSampleProvider(phraseSampleProvider) {DelayBySamples = written});
                    }
                }

                var bytesToRead = Math.Min(RemainingBytes, count - written);
                written += bytesToRead;
                RemainingBytes -= bytesToRead;
            }

            if(RemainingDelayBeforeStop > 0)
            {
                var bytesToRead = Phrases.Any()
                    ? count
                    : Math.Min(written + RemainingDelayBeforeStop, count);
                var readedFromMixer = Mixer.Read(buffer, offset, bytesToRead);

                if(RemainingBytes <= 0 && !Phrases.Any())
                {
                    RemainingDelayBeforeStop -= readedFromMixer - written;
                }

                Position += readedFromMixer;
                return readedFromMixer;
            }

            return 0;
        }

        public long GetPosition() => Position;

        public WaveFormat OutputWaveFormat => WaveFormat;

        public event EventHandler<Phrase> OnPhrasePlaying;

        protected virtual void TriggerPhrasePlaying(Phrase e)
        {
            if(OnPhrasePlaying != null)
            {
                if(SyncContext != null)
                {
                    SyncContext.Post(s => OnPhrasePlaying.Invoke(s, e), this);
                }
                else
                {
                    OnPhrasePlaying.Invoke(this, e);
                }
            }
        }
    }
}