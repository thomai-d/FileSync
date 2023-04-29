using FileSync.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Cli.Index
{
    internal class IndexApp
    {
        private readonly ILogger<IndexApp> _logger;
        private readonly IDirectoryEnumerator _dirEnumerator;
        private readonly IIndexWriter _indexWriter;
        private readonly IChecksumGenerator _checksumGenerator;

        public IndexApp(
            ILogger<IndexApp> logger,
            IDirectoryEnumerator dirEnumerator,
            IIndexWriter indexWriter,
            IChecksumGenerator checksumGenerator)
        {
            _logger = logger;
            _dirEnumerator = dirEnumerator;
            _indexWriter = indexWriter;
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

        public async Task RunAsync(IndexOptions options)
        {
            VerifyAndLogOptions(options);

            ApplyIgnorePatterns(options);

            var index = _dirEnumerator.Enumerate(options.SourcePath);

            if (options.IsChecksumGenerationEnabled)
            {
                await _checksumGenerator.GenerateChecksumsAsync(index, forceRegenerateExisting: true);
            }

            await _indexWriter.PersistIndexAsync(index, options.DestinationIndexFile);
        }
        
        private void ApplyIgnorePatterns(IndexOptions options)
        {
            foreach (var ignorePattern in options.IgnorePatterns)
                _dirEnumerator.AddIgnorePattern(ignorePattern);
        }

        private void VerifyAndLogOptions(IndexOptions options)
        {
            _logger.LogInformation("Using WorkingDir: {dir}", Directory.GetCurrentDirectory());

            if (!Directory.Exists(options.SourcePath))
                throw new ApplicationException($"SourcePath does not exist: {options.SourcePath}");

            _logger.LogInformation("Using SourcePath: {path}", options.SourcePath);
            _logger.LogInformation("Using IndexFile: {file}", options.DestinationIndexFile);

            foreach (var ignorePattern in options.IgnorePatterns)
            {
                _logger.LogInformation("Using IgnorePattern: {pattern}", ignorePattern);
            }
        }
    }
}
