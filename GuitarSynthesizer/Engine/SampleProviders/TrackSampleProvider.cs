using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GuitarSynthesizer.Helpers;
using GuitarSynthesizer.Model;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace GuitarSynthesizer.Engine.SampleProviders
{
    internal class TrackSampleProvider : ISampleProvider, IWavePosition
    {
        private const float DefaultFadeOutTime = 0.01f; //seconds

        public TrackSampleProvider(MediaBankBase mediaBank, Track track)
        {
            SyncContext = SynchronizationContext.Current;

            Tempo = track.Tempo;
            WholeNoteFadeOutTime = PhraseHelper.BaseTempo / Tempo;
            HalfNoteFadeOutTime = WholeNoteFadeOutTime / 2;
            QuarterNoteFadeOutTime = HalfNoteFadeOutTime / 2;
            CurrentLetRingFadeOut = QuarterNoteFadeOutTime;

            MediaBank = mediaBank;
            Phrases = track.Phrases.ToArray();
            PhrasesQueue = new Queue<Phrase>(Phrases);
            // ReSharper disable once SuspiciousTypeConversion.Global
            TrackSamples = Phrases.Sum(c => (long)c.GetPhraseSamples(Tempo, WaveFormat));
            TrackDuration = TimeSpan.FromTicks(TrackSamples / WaveFormat.AverageBytesPerSecond * (WaveFormat.BitsPerSample / 8) * TimeSpan.TicksPerSecond);

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
        public Phrase[] Phrases { get; }
        private Queue<Phrase> PhrasesQueue { get; set; }
        public int Tempo { get; }
        public TimeSpan TrackDuration { get; }
        private long TrackSamples { get; }

        private long PositionInSamples { get; set; }

        private float CurrentLetRingFadeOut { get; set; }
        private bool LetRingEnabled { get; set; }

        private MixingSampleProvider Mixer { get; }

        private int RemainingSamples { get; set; }
        private int RemainingDelayBeforeStop { get; set; }

        public WaveFormat WaveFormat => MediaBank.TargetWaveFormat;

        private readonly object _syncDummy = new object();

        public int Read(float[] buffer, int offset, int count)
        {
            lock(_syncDummy)
            {
                var written = 0;
                if(RemainingSamples > 0)
                {
                    var readed = Math.Min(RemainingSamples, count);
                    RemainingSamples -= readed;
                    written += readed;
                }

                while(PhrasesQueue.Count > 0 && written < count)
                {
                    var phrase = PhrasesQueue.Dequeue();
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

                    RemainingSamples = phrase.GetPhraseSamples(Tempo, WaveFormat);
                    var phraseSampleProvider = CreateSampleProvider(phrase, written, letRingPhrase);
                    if(phraseSampleProvider != null)
                    {
                        Mixer.AddMixerInput(phraseSampleProvider);
                    }

                    var samplesToRead = Math.Min(RemainingSamples, count - written);
                    written += samplesToRead;
                    RemainingSamples -= samplesToRead;
                }

                if(RemainingDelayBeforeStop > 0)
                {
                    var samplesToRead = PhrasesQueue.Any()
                        ? count
                        : Math.Min(written + RemainingDelayBeforeStop, count);
                    var readedFromMixer = Mixer.Read(buffer, offset, samplesToRead);

                    if(RemainingSamples <= 0 && !PhrasesQueue.Any())
                    {
                        RemainingDelayBeforeStop -= readedFromMixer - written;
                    }

                    PositionInSamples += readedFromMixer;
                    return readedFromMixer;
                }

                return 0;
            }
        }

        private ISampleProvider CreateSampleProvider(Phrase phrase, int offset, bool letRingPhrase)
        {
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
                    var phraseSampleProvider =
                        new FadeOutSampleProvider(new MixingSampleProvider(sampleProviders),
                            phraseDuration + additionalDuration, DefaultFadeOutTime);
                    if(offset > 0)
                    {
                        return new OffsetSampleProvider(phraseSampleProvider)
                        {
                            DelayBySamples = offset
                        };
                    }

                    return phraseSampleProvider;
                }
            }

            return null;
        }

        public void Seek(long newPosition)
        {
            lock(_syncDummy)
            {
                Mixer.RemoveAllMixerInputs();
                PhrasesQueue = new Queue<Phrase>(Phrases);

                if(newPosition == 0)
                {
                    PositionInSamples = 0;
                }
                else if(newPosition > 0 && newPosition < TrackSamples)
                {
                    long currentPosition = 0;
                    long lastPhraseDuration = 0;
                    long previousPosition = 0;
                    Phrase lastPhrase = default(Phrase);
                    while(currentPosition < newPosition && PhrasesQueue.Any())
                    {
                        lastPhrase = PhrasesQueue.Dequeue();
                        lastPhraseDuration = lastPhrase.GetPhraseSamples(Tempo, WaveFormat);
                        previousPosition = currentPosition;
                        currentPosition += lastPhraseDuration;
                    }

                    int offset = (int)(newPosition - previousPosition);
                    PositionInSamples = newPosition;
                    RemainingSamples = (int)(lastPhraseDuration - offset);
                    var phraseSampleProvider = CreateSampleProvider(lastPhrase, 0, false);
                    if(phraseSampleProvider != null)
                    {
                        Mixer.AddMixerInput(new OffsetSampleProvider(phraseSampleProvider)
                        {
                            SkipOverSamples = offset
                        });
                    }
                }
                else if(newPosition > 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(newPosition), "New position should be less than track duration");
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(newPosition), "New position should be grether then 0");
                }
            }
        }

        public void Seek(TimeSpan newPosition)
        {
            // ReSharper disable once PossibleLossOfFraction
            var samplePosition = (long)(newPosition.TotalSeconds * WaveFormat.AverageBytesPerSecond / (WaveFormat.BitsPerSample / 8));
            Seek(samplePosition);
        }

        public long GetPosition() => PositionInSamples * (WaveFormat.BitsPerSample / 8);

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