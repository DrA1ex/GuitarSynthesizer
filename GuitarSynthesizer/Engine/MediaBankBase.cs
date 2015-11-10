using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace GuitarSynthesizer.Engine
{
    public abstract class MediaBankBase
    {
        protected MediaBankBase(WaveFormat targetFormat)
        {
            TargetWaveFormat = targetFormat;
            Random = new Random();

            LoadMedia();
        }

        private Random Random { get; }

        protected Dictionary<Note, byte[][]> MediaDictionary { get; private set; }

        public WaveFormat TargetWaveFormat { get; }

        public WaveStream GetMedia(Note note)
        {
            if(MediaDictionary.ContainsKey(note))
            {
                var mediaForNote = MediaDictionary[note];
                var data = mediaForNote[Random.Next(mediaForNote.Length)];

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
            var mediaPathes = GetMediaPathes();

            MediaDictionary = mediaPathes.Where(
                m => !string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(m.Value)))
                .GroupBy(m => m.Key)
                .ToDictionary(group => group.Key,
                    group => group.Select(path => GetMediaBytes(path.Value)).ToArray());
        }

        private byte[] GetMediaBytes(string path)
        {
            using(var reader = new AudioFileReader(path))
            {
                var buffer = new byte[1024];

                var needResampling = TargetWaveFormat.SampleRate != reader.WaveFormat.SampleRate;

                var resampledStream = needResampling
                    ? (WaveStream)
                        new ResamplerDmoStream(reader,
                            WaveFormat.CreateIeeeFloatWaveFormat(TargetWaveFormat.SampleRate, 1))
                    : reader;

                var outStream = new MemoryStream {Capacity = (int)reader.Length};
                int readed;
                while((readed = resampledStream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    outStream.Write(buffer, 0, readed);
                }

                return outStream.GetBuffer();
            }
        }

        protected abstract IEnumerable<KeyValuePair<Note, string>> GetMediaPathes();
    }
}