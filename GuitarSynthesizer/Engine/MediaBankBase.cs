using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace GuitarSynthesizer.Engine
{
    public abstract class MediaBankBase
    {
        private Dictionary<Note, List<byte[]>> _mediaDictionary;

        protected MediaBankBase(WaveFormat targetFormat)
        {
            TargetWaveFormat = targetFormat;
            Random = new Random();

            LoadMedia();
        }

        private Random Random { get; }

        protected Dictionary<Note, List<byte[]>> MediaDictionary => 
            _mediaDictionary ?? (_mediaDictionary = new Dictionary<Note, List<byte[]>>());

        public WaveFormat TargetWaveFormat { get; }

        public WaveStream GetMedia(Note note)
        {
            if(MediaDictionary.ContainsKey(note))
            {
                List<byte[]> mediaForNote = MediaDictionary[note];
                byte[] data = mediaForNote[Random.Next(mediaForNote.Count)];

                return new RawSourceWaveStream(new MemoryStream(data), TargetWaveFormat);
            }

            return null;
        }

        public WaveStream GetMedia(string note)
        {
            return GetMedia(Note.FromString(note));
        }

        public WaveStream GetMedia(int id)
        {
            return GetMedia(Note.FromId(id));
        }

        protected void LoadMedia()
        {
            var buffer = new byte[1024];

            foreach(var media in GetMediaPathes())
            {
                string fileName = Path.GetFileNameWithoutExtension(media.Value);
                if(String.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                using(var reader = new AudioFileReader(media.Value))
                {
                    bool needResampling = TargetWaveFormat.SampleRate != reader.WaveFormat.SampleRate;

                    WaveStream resampledStream = needResampling
                        ? (WaveStream)new ResamplerDmoStream(reader, WaveFormat.CreateIeeeFloatWaveFormat(TargetWaveFormat.SampleRate, 1))
                        : reader;

                    var outStream = new MemoryStream { Capacity = (int)reader.Length };
                    int readed;
                    while((readed = resampledStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        outStream.Write(buffer, 0, readed);
                    }

                    if(MediaDictionary.ContainsKey(media.Key))
                    {
                        MediaDictionary[media.Key].Add(outStream.GetBuffer());
                    }
                    else
                    {
                        MediaDictionary.Add(media.Key, new List<byte[]> { outStream.GetBuffer() });
                    }
                }
            }
        }

        protected abstract IEnumerable<KeyValuePair<Note, string>> GetMediaPathes();
    }
}