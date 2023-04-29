using FileSync.Domain.Abstractions;
using FileSync.Domain.Extensions;
using FileSync.Domain.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Infrastructure
{
    public class Md5ChecksumGenerator : IChecksumGenerator
    {
        private readonly ILogger<Md5ChecksumGenerator> _logger;

        public Md5ChecksumGenerator(ILogger<Md5ChecksumGenerator> logger)
        {
            _logger = logger;
        }

        public Action<string, Exception> OnException { get; set; } = delegate { };

        public async Task GenerateChecksumsAsync(FileIndex index, bool forceRegenerateExisting)
        {
            var watch = Stopwatch.StartNew();

            var fileEntries = index.Entries
                                   .Where(entry => entry.Type == EntryType.File)
                                   .Where(entry => forceRegenerateExisting || entry.Hash.IsEmpty())
                                   .ToArray();

            _logger.LogInformation("Hashing {count} files...", fileEntries.Length);

            var hashedEntries = new ConcurrentBag<Entry>();
            await Parallel.ForEachAsync(fileEntries, async (entry, _) =>
            {
                Entry hashedEntry;

                try
                {
                    hashedEntry = await CreateMd5Task(index, entry);
                }
                catch (Exception ex)
                {
                    OnException(entry.Path, ex);
                    return;
                }

                hashedEntries.Add(hashedEntry);
            });

            foreach (var entry in hashedEntries)
            {
                index.ReplaceEntry(entry);
            }

            var totalSize = fileEntries.Sum(e => e.Size) / 1024 / 1024;
            _logger.LogInformation("Hashing {count} files ({size}MB) took {elapsed}", fileEntries.Length, totalSize, watch.Elapsed);
        }

        public async Task<string> GenerateChecksumForFileAsync(string path)
        {
            using (var md5 = MD5.Create())
            {
                using var stream = new BufferedStream(File.OpenRead(path), 1024 * 1024);
                var hash = await md5.ComputeHashAsync(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private async Task<Entry> CreateMd5Task(FileIndex index, Entry entry)
        {
            var filePath = Path.Combine(index.BasePath, entry.Path);
            var md5 = await GenerateChecksumForFileAsync(filePath);
            var hashedEntry = entry with
            {
                Hash = md5
            };

            return hashedEntry;
        }
    }
}
