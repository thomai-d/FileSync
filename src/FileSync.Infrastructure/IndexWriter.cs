using FileSync.Domain.Abstractions;
using FileSync.Domain.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Infrastructure
{
    public class IndexWriter : IIndexWriter
    {
        private readonly ILogger<IndexWriter> _logger;

        public IndexWriter(ILogger<IndexWriter> _logger)
        {
            this._logger = _logger;
        }

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
            _logger.LogInformation("Restoring index {file}...", filePath);
            var watch = Stopwatch.StartNew();

            using var file = File.OpenText(filePath);

            var basePath = await file.ReadLineAsync()
                            ?? throw new ApplicationException($"Can't find base path in {filePath}");

            var index = new FileIndex(basePath, await RestoreFromFileCore(file).ToListAsync());
            
            _logger.LogInformation("Restoring index ({count} items, {size} MB) took {time}", index.EntryCount, index.Size/1024/1024, watch.Elapsed);
            return index;
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
