using FileSync.Domain.Abstractions;
using FileSync.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Infrastructure
{
    public class IndexWriter : IIndexWriter
    {
        public async Task PersistIndexAsync(FileIndex index, string filePath)
        {
            using var file = File.CreateText(filePath);

            await file.WriteLineAsync(index.BasePath);

            foreach (var entry in index.Entries)
            {
                if (entry.Type == EntryType.File)
                    await file.WriteLineAsync($"{entry.Type}|{entry.Path}|{entry.CreatedUtc.Ticks}|{entry.ModifiedUtc.Ticks}|{entry.Size}|{entry.Hash}");
                if (entry.Type == EntryType.Directory)
                    await file.WriteLineAsync($"{entry.Type}|{entry.Path}|{entry.CreatedUtc.Ticks}|{entry.ModifiedUtc.Ticks}");
            }
        }

        public async Task<FileIndex> RestoreFromFileAsync(string filePath)
        {
            using var file = File.OpenText(filePath);

            var basePath = await file.ReadLineAsync()
                            ?? throw new ApplicationException($"Can't find base path in {filePath}");

            return new FileIndex(basePath, await RestoreFromFileCore(file).ToListAsync());
        }

        private async IAsyncEnumerable<Entry> RestoreFromFileCore(StreamReader file)
        {
            while (!file.EndOfStream)
            {
                var line = await file.ReadLineAsync();
                if (line is null)
                    yield break;

                var parts = line.Split('|');

                var createdUtc = new DateTime(long.Parse(parts[2]), DateTimeKind.Utc);
                var modifiedUtc = new DateTime(long.Parse(parts[3]), DateTimeKind.Utc);

                if (parts[0] == nameof(EntryType.File))
                {
                    yield return Entry.File(parts[1], createdUtc, modifiedUtc, long.Parse(parts[4]), parts[5]);
                }
                else if (parts[0] == nameof(EntryType.Directory))
                {
                    yield return Entry.Dir(parts[1], createdUtc, modifiedUtc);
                }
            }
        }
    }
}
