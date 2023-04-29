using FileSync.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Cli.Verify
{
    public class VerifyApp
    {
        private readonly IDirectoryEnumerator _dirEnumerator;
        private readonly IIndexWriter _indexWriter;
        private readonly IEntryComparer _entryComparer;
        private readonly IChecksumGenerator _checksumGen;
        private readonly IDiffWriter _diffWriter;
        private readonly ILogger<VerifyApp> _logger;

        public VerifyApp(
            IDirectoryEnumerator dirEnumerator,
            IIndexWriter indexWriter,
            IEntryComparer entryComparer,
            IChecksumGenerator checksumGen,
            IDiffWriter diffWriter,
            ILogger<VerifyApp> logger)
        {
            _dirEnumerator = dirEnumerator;
            _indexWriter = indexWriter;
            _entryComparer = entryComparer;
            _checksumGen = checksumGen;
            _diffWriter = diffWriter;
            _logger = logger;

            _dirEnumerator.OnException = (path, ex) =>
            {
                _logger.LogError("Failed to enumerate: {path}", path);
            };

            _checksumGen.OnException = (path, ex) =>
            {
                _logger.LogError("Failed to create checksum: {path} ({ex} - {message})", path, ex.GetType().Name, ex.Message);
            };
        }

        public async Task RunAsync(VerifyOptions options)
        {
            VerifyAndLogOptions(options);

            ApplyIgnorePatterns(options);

            var indexToVerify = await _indexWriter.RestoreFromFileAsync(options.IndexFile);

            var sourceIndex = _dirEnumerator.Enumerate(options.SourcePath);

            await _checksumGen.GenerateChecksumsAsync(sourceIndex, forceRegenerateExisting: true);

            var diff = _entryComparer.Compare(sourceIndex, indexToVerify);

            _logger.LogInformation("{count} removed entries", diff.RemovedEntriesInSource.Count);
            _logger.LogInformation("{count} additional entries ({total} MB)", diff.AdditionalEntriesInSource.Count, diff.AdditionalEntriesInSource.Sum(i => i.Size) / 1024 / 1024);
            _logger.LogInformation("{count} modified entries ({total} MB)", diff.ModifiedEntriesInSource.Count, diff.ModifiedEntriesInSource.Sum(i => i.SourceEntry.Size) / 1024 / 1024);

            await _diffWriter.WriteDiffAsync(diff, options.OutputFilePath);
        }

        private void ApplyIgnorePatterns(VerifyOptions options)
        {
            foreach (var ignorePattern in options.IgnorePatterns)
                _dirEnumerator.AddIgnorePattern(ignorePattern);
        }

        private void VerifyAndLogOptions(VerifyOptions options)
        {
            _logger.LogInformation("Using WorkingDir: {dir}", Directory.GetCurrentDirectory());

            if (!Directory.Exists(options.SourcePath))
                throw new ApplicationException($"SourcePath does not exist: {options.SourcePath}");
            if (!File.Exists(options.IndexFile))
                throw new ApplicationException($"IndexFile does not exist: {options.IndexFile}");

            _logger.LogInformation("Using SourcePath: {path}", options.SourcePath);
            _logger.LogInformation("Using IndexFile: {index}", options.IndexFile);
            _logger.LogInformation("Using OutputFilePath: {output}", options.OutputFilePath);

            foreach (var ignorePattern in options.IgnorePatterns)
            {
                _logger.LogInformation("Using IgnorePattern: {pattern}", ignorePattern);
            }
        }
    }
}
