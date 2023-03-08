using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Model
{
    public class FileIndex
    {
        private readonly Dictionary<string, Entry> _entries;

        public FileIndex(string basePath, params Entry[] entries)
        {
            _entries = entries.ToDictionary(i => i.Path, i => i);
            BasePath = basePath;
        }

        public FileIndex(string basePath, IEnumerable<Entry> entries)
        {
            _entries = entries.ToDictionary(i => i.Path, i => i);
            BasePath = basePath;
        }

        public IEnumerable<Entry> Entries => _entries.Values;

        public string BasePath { get; }

        public bool HasEntry(string path)
            => _entries.ContainsKey(path);

        public bool TryGetEntry(string path, [MaybeNullWhen(false)] out Entry entry)
            => _entries.TryGetValue(path, out entry);

        public void RemoveEntry(string path)
            => _entries.Remove(path);

        public void AddNewEntry(Entry entry)
        {
            if (!_entries.TryAdd(entry.Path, entry))
                throw new InvalidOperationException($"Entry already exists: {entry.Path}");
        }

        public Entry GetRequiredEntry(string path)
        {
            if (!_entries.TryGetValue(path, out var entry))
                throw new InvalidOperationException($"Required entry not found: {path}");

            return entry;
        }

        public void ReplaceEntry(Entry sourceEntry)
        {
            if (!_entries.TryGetValue(sourceEntry.Path, out var _))
                throw new InvalidOperationException($"Cannot replace nonexistant entry: {sourceEntry.Path}");

            _entries[sourceEntry.Path] = sourceEntry;
        }

        public long EntryCount
            => _entries.Count;

        public long Size
            => _entries.Values.Sum(e => e.Size ?? 0);
    }
}
