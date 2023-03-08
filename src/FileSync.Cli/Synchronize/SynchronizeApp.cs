using FileSync.Domain.Abstractions;
using FileSync.Domain.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Cli.Synchronize
{
    internal class SynchronizeApp
    {
        private readonly IDirectoryEnumerator _dirEnumerator;
        private readonly ILogger<SynchronizeApp> _logger;
        private readonly IIndexWriter _indexer;
        private readonly IEntryComparer _comparer;
        private readonly IIncrementalSynchronizer _synchronizer;

        public SynchronizeApp(
            IDirectoryEnumerator sourceEnumerator,
            ILogger<SynchronizeApp> logger,
            IIndexWriter indexer,
            IEntryComparer comparer,
            IIncrementalSynchronizer synchronizer
            )
        {
            _dirEnumerator = sourceEnumerator;
            _logger = logger;
            _indexer = indexer;
            _comparer = comparer;
            _synchronizer = synchronizer;

            _dirEnumerator.OnException = (path, ex) =>
            {
                _logger.LogError("Failed to enumerate: {path}", path);
            };

            _synchronizer.OnException = (path, ex) =>
            {
                _logger.LogError("Failed to synchronize: {path} ({ex} - {message})", path, ex.GetType().Name, ex.Message);
            };
        }

        public async Task RunAsync(SynchronizeOptions options)
        {
            VerifyAndLogOptions(options);

            ApplyIgnorePatterns(options);

            var sourceIndex = _dirEnumerator.Enumerate(options.SourcePath);

            var destIndex = await ReadOrBuildDestinationIndexAsync(options);

            var diff = _comparer.Compare(sourceIndex, destIndex);

            _logger.LogInformation("{count} removed entries", diff.RemovedEntriesInSource.Count);
            _logger.LogInformation("{count} additional entries ({total} MB)", diff.AdditionalEntriesInSource.Count, diff.AdditionalEntriesInSource.Sum(i => i.Size) / 1024 / 1024);
            _logger.LogInformation("{count} modified entries ({total} MB)", diff.ModifiedEntriesInSource.Count, diff.ModifiedEntriesInSource.Sum(i => i.SourceEntry.Size) / 1024 / 1024);

            for (var attempt = 1; attempt <= options.Retries; attempt++)
            {
                await _synchronizer.SynchronizeAsync(diff, sourceIndex, destIndex);

                if (!string.IsNullOrEmpty(options.DestinationIndexFile))
                {
                    await _indexer.PersistIndexAsync(destIndex, options.DestinationIndexFile);
                }

                diff = _comparer.Compare(sourceIndex, destIndex);

                _logger.LogInformation("After sync #{attempt}: {count} removed entries", attempt, diff.RemovedEntriesInSource.Count);
                _logger.LogInformation("After sync #{attempt}: {count} additional entries ({total} MB)", attempt, diff.AdditionalEntriesInSource.Count, diff.AdditionalEntriesInSource.Sum(i => i.Size) / 1024 / 1024);
                _logger.LogInformation("After sync #{attempt}: {count} modified entries ({total} MB)", attempt, diff.ModifiedEntriesInSource.Count, diff.ModifiedEntriesInSource.Sum(i => i.SourceEntry.Size) / 1024 / 1024);

                if (diff.TotalChanges == 0)
                    break;
            }

            _logger.LogInformation("Done.");

        }

        private async Task<FileIndex> ReadOrBuildDestinationIndexAsync(SynchronizeOptions options)
        {
            var useDestIndex = !string.IsNullOrEmpty(options.DestinationIndexFile);

            if (useDestIndex)
            {
                if (File.Exists(options.DestinationIndexFile))
                {
                    var fileInfo = new FileInfo(options.DestinationIndexFile);
                    _logger.LogInformation("Found destination index file from {date}.", fileInfo.LastWriteTime);
                    return await _indexer.RestoreFromFileAsync(options.DestinationIndexFile);
                }
                else
                {
                    _logger.LogWarning("No destination index file found.");
                }
            }

            _dirEnumerator.ClearIgnorePatterns();
            var destIndex = _dirEnumerator.Enumerate(options.DestinationPath);

            if (useDestIndex)
            {
                _logger.LogInformation("Persisting index file to {index}", options.DestinationIndexFile);
                await _indexer.PersistIndexAsync(destIndex, options.DestinationIndexFile);
            }

            return destIndex;
        }

        private void ApplyIgnorePatterns(SynchronizeOptions options)
        {
            foreach (var ignorePattern in options.IgnorePatterns)
                _dirEnumerator.AddIgnorePattern(ignorePattern);
        }

        private void VerifyAndLogOptions(SynchronizeOptions options)
        {
            _logger.LogInformation("Using WorkingDir: {dir}", Directory.GetCurrentDirectory());

            if (!Directory.Exists(options.SourcePath))
                throw new ApplicationException($"SourcePath does not exist: {options.SourcePath}");
            if (!Directory.Exists(options.DestinationPath))
                throw new ApplicationException($"DestinationPath does not exist: {options.DestinationPath}");

            var useDestIndex = !string.IsNullOrEmpty(options.DestinationIndexFile);

            _logger.LogInformation("Using SourcePath: {path}", options.SourcePath);
            _logger.LogInformation("Using DestinationPath: {path}", options.DestinationPath);
            _logger.LogInformation("Using Index: {index}", useDestIndex);
            _logger.LogInformation("Using Retries: {retries}", options.Retries);
            if (useDestIndex)
            {
                _logger.LogInformation("Using DestinationIndexFile: {useIndex}", options.DestinationIndexFile);
            }

            foreach (var ignorePattern in options.IgnorePatterns)
            {
                _logger.LogInformation("Using IgnorePattern: {pattern}", ignorePattern);
            }
        }
    }
}
