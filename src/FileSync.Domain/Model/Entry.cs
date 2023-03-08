using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Model
{
    public record Entry
    {
        public EntryType Type { get; private set; }

        public string Path { get; private set; }

        public DateTime CreatedUtc { get; private set; }
        
        public DateTime ModifiedUtc { get; private set; }

        public long? Size { get; private set; }
        
        public string Hash { get; init; } = string.Empty;

        private Entry(string path)
        {
            Path = path;
        }

        public static Entry File(string path, DateTime createdUtc, DateTime modifiedUtc, long size, string hash = "")
        {
            return new Entry(path)
            {
                Type = EntryType.File,
                Size = size,
                CreatedUtc= createdUtc,
                ModifiedUtc = modifiedUtc,
                Hash = hash
            };
        }

        public static Entry Dir(string path, DateTime createdUtc, DateTime modifiedUtc)
        {
            return new Entry(path)
            {
                Type = EntryType.Directory,
                CreatedUtc= createdUtc,
                ModifiedUtc = modifiedUtc,
            };
        }
    }

    public enum EntryType
    {
        Undefined,
        File,
        Directory
    }
}
