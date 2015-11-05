using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;

namespace GuitarSynthesizer.Engine
{
    public abstract class MediaBankGuitarBase : MediaBankBase
    {
        protected MediaBankGuitarBase(WaveFormat targetFormat)
            : base(targetFormat)
        {
        }

        protected abstract string SearchPath { get; }
        protected abstract string SearchPattern { get; }

        protected abstract Note StartNote { get; }

        protected override IEnumerable<KeyValuePair<Note, string>> GetMediaPathes()
        {
            string bankPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SearchPath);

            if(!Directory.Exists(bankPath))
            {
                throw new Exception("Missing sound bank");
            }

            string[] mediaBank = Directory.GetFiles(bankPath, SearchPattern);
            Note startNote = StartNote;

            if(mediaBank.Length == 0)
            {
                throw new Exception("No files in sound bank");
            }

            foreach(string path in mediaBank)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                if(!String.IsNullOrWhiteSpace(fileName))
                {
                    string[] nameParts = fileName.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                    int noteId;
                    if(int.TryParse(nameParts.First(), out noteId))
                    {
                        yield return new KeyValuePair<Note, string>(startNote + noteId, path);
                    }
                }
            }
        }
    }
}