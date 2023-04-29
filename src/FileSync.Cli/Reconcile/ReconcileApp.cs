using FileSync.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Cli.Index
{
    internal class ReconcileApp
    {
        private readonly ILogger<ReconcileApp> _logger;
        private readonly IDirectoryEnumerator _dirEnumerator;
        private readonly IIndexWriter _indexWriter;
        private readonly IEntryComparer _comparer;
        private readonly IChecksumGenerator _checksumGenerator;

        public ReconcileApp(
            ILogger<ReconcileApp> logger,
            IDirectoryEnumerator dirEnumerator,
            IIndexWriter indexWriter,
            IEntryComparer comparer,
            IChecksumGenerator checksumGenerator)
        {
            _logger = logger;
            _dirEnumerator = dirEnumerator;
            _indexWriter = indexWriter;
            _comparer = comparer;
            _checksumGenerator = checksumGenerator;

            _dirEnumerator.OnException = (path, ex) =>
            {
                _logger.LogError("Failed to enumerate: {path}", path);
            };

            _checksumGenerator.OnException = (path, ex) =>
            {
                _logger.LogError("Failed to create checksum: {path} ({ex} - {message})", path, ex.GetType().Name, ex.Message);
            };
        }

        public async Task RunAsync(ReconcileOptions options)
        {
            VerifyAndLogOptions(options);

            ApplyIgnorePatterns(options);

            var index = await _indexWriter.RestoreFromFileAsync(options.IndexFile);

            var actualFiles = _dirEnumerator.Enumerate(options.DestinationPath);

            var diff = _comparer.Compare(index, actualFiles);

            foreach (var addedFile in diff.RemovedEntriesInSource)
            {
                _logger.LogWarning("Added file: {file}", addedFile.Path);
                index.AddNewEntry(addedFile);
            }

            foreach (var removedFile in diff.AdditionalEntriesInSource)
            {
                _logger.LogWarning("Removing file: {file}", removedFile.Path);
                index.RemoveEntry(removedFile.Path);
            }

            if (options.IsChecksumGenerationEnabled)
            {
                await _checksumGenerator.GenerateChecksumsAsync(index, forceRegenerateExisting: false);
            }

            _logger.LogInformation("Press any key to persist new index.");
            Console.ReadKey();

            await _indexWriter.PersistIndexAsync(index, options.IndexFile);
        }
        
        private void ApplyIgnorePatterns(ReconcileOptions options)
        {
            foreach (var ignorePattern in options.IgnorePatterns)
                _dirEnumerator.AddIgnorePattern(ignorePattern);
        }

        private void VerifyAndLogOptions(ReconcileOptions options)
        {
            _logger.LogInformation("Using WorkingDir: {dir}", Directory.GetCurrentDirectory());

            if (!Directory.Exists(options.DestinationPath))
                throw new ApplicationException($"DestinationPath does not exist: {options.DestinationPath}");
            
            if (!File.Exists(options.IndexFile))
                throw new ApplicationException($"Index does not exist: {options.IndexFile}");

            _logger.LogInformation("Using DestinationPath: {path}", options.DestinationPath);
            _logger.LogInformation("Using IndexFile: {file}", options.IndexFile);

            foreach (var ignorePattern in options.IgnorePatterns)
            {
                _logger.LogInformation("Using IgnorePattern: {pattern}", ignorePattern);
            }
        }
    }
}
