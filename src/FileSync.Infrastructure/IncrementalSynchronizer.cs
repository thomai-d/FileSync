using FileSync.Domain.Abstractions;
using FileSync.Domain.Exceptions;
using FileSync.Domain.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Infrastructure
{
    public class IncrementalSynchronizer : IIncrementalSynchronizer
    {
        private readonly IFileSystem _fileSystem;
        private readonly IChecksumGenerator _checksumGen;
        private readonly ILogger<IncrementalSynchronizer> _logger;

        public IncrementalSynchronizer(
            IFileSystem fileSystem,
            IChecksumGenerator checksumGen,
            ILogger<IncrementalSynchronizer> logger)
        {
            _fileSystem = fileSystem;
            _checksumGen = checksumGen;
            _logger = logger;
        }

        public Action<string, Exception> OnException { get; set; } = delegate { };

        public async Task SynchronizeAsync(EntryDiff compareResult, FileIndex sourceIndex, FileIndex destinationIndex)
        {
            foreach (var removedFile in compareResult.RemovedEntriesInSource.Where(t => t.Type == EntryType.File))
            {
                RemoveEntry(destinationIndex, removedFile);
            }

            var directoriesOrderedByDepth =
                compareResult.RemovedEntriesInSource
                    .Where(t => t.Type == EntryType.Directory)
                    .OrderByDescending(dir => dir.Path.Split(Path.DirectorySeparatorChar, System.StringSplitOptions.RemoveEmptyEntries).Length);

            foreach (var removedDirectory in directoriesOrderedByDepth)
            {
                RemoveEntry(destinationIndex, removedDirectory);
            }

            foreach (var newDir in compareResult.AdditionalEntriesInSource.Where(t => t.Type == EntryType.Directory))
            {
                await CreateDirectoryAsync(sourceIndex, destinationIndex, newDir);
            }

            foreach (var newFile in compareResult.AdditionalEntriesInSource.Where(t => t.Type == EntryType.File))
            {
                await CreateFileAsync(sourceIndex, destinationIndex, newFile);
            }

            foreach (var item in compareResult.ModifiedEntriesInSource.Where(t => t.SourceEntry.Type == EntryType.Directory))
            {
                throw new ApplicationException($"Can't handle modified directories: {item.SourceEntry.Path}, Reason: {item.ChangeReason}");
            }

            foreach (var item in compareResult.ModifiedEntriesInSource.Where(t => t.SourceEntry.Type == EntryType.File))
            {
                var destEntry = destinationIndex.GetRequiredEntry(item.SourceEntry.Path);
                await UpdateFileAsync(sourceIndex, destinationIndex, item.SourceEntry, destEntry, item.ChangeReason);
            }
        }

        private async Task UpdateFileAsync(FileIndex sourceIndex, FileIndex destinationIndex, Entry sourceEntry, Entry destEntry, ChangeReason reason)
        {
            if (sourceEntry.Type == EntryType.Directory)
                throw new ApplicationException($"Can't update folders: {sourceEntry.Path}");
            if (destEntry.Type == EntryType.Directory)
                throw new ApplicationException($"Can't update folders: {destEntry.Path}");

            var sourcePath = Path.Combine(sourceIndex.BasePath, sourceEntry.Path);
            var destinationPath = Path.Combine(destinationIndex.BasePath, destEntry.Path);

            _logger.LogDebug("Copying file {src} to {dest}, reason: {reason}", sourcePath, destinationPath, reason);

            await TransferFileAsync(destinationIndex, sourceEntry, sourcePath, destinationPath, isNew: false);
        }

        private Task CreateDirectoryAsync(FileIndex sourceIndex, FileIndex destinationIndex, Entry addedEntry)
        {
            if (addedEntry.Type != EntryType.Directory)
                throw new ApplicationException($"ExpectedDirectory: {addedEntry.Path}");

            var sourcePath = Path.Combine(sourceIndex.BasePath, addedEntry.Path);
            var destinationPath = Path.Combine(destinationIndex.BasePath, addedEntry.Path);

            try
            {
                _logger.LogDebug("Creating directory {dir}", destinationPath);
                _fileSystem.Directory.CreateDirectory(destinationPath);
            }
            catch (Exception ex)
            {
                OnException(destinationPath, ex);
                return Task.CompletedTask;
            }

            destinationIndex.AddNewEntry(addedEntry);

            return Task.CompletedTask;
        }

        private async Task CreateFileAsync(FileIndex sourceIndex, FileIndex destinationIndex, Entry addedEntry)
        {
            var sourcePath = Path.Combine(sourceIndex.BasePath, addedEntry.Path);
            var destinationPath = Path.Combine(destinationIndex.BasePath, addedEntry.Path);

            await TransferFileAsync(destinationIndex, addedEntry, sourcePath, destinationPath, isNew: true);
        }

        private async Task TransferFileAsync(FileIndex destinationIndex, Entry sourceEntry, string sourcePath, string destinationPath, bool isNew)
        {
            try
            {
                var checksum = await _checksumGen.GenerateChecksumForFileAsync(sourcePath);

                _fileSystem.File.Copy(sourcePath, destinationPath, overwrite: !isNew);

                var checksumAfterCopy = await _checksumGen.GenerateChecksumForFileAsync(destinationPath);

                if (checksum != checksumAfterCopy)
                {
                    _fileSystem.File.Delete(destinationPath);

                    if (!isNew)
                        destinationIndex.RemoveEntry(sourceEntry.Path);

                    throw new ChecksumChangedOnCopyException(destinationPath, checksum, checksumAfterCopy);
                }

                sourceEntry = sourceEntry with { Hash = checksum };
            }
            catch (Exception ex)
            {
                OnException(destinationPath, ex);
                return;
            }

            if (isNew)
                destinationIndex.AddNewEntry(sourceEntry);
            else
                destinationIndex.ReplaceEntry(sourceEntry);
        }

        private void RemoveEntry(FileIndex destinationIndex, Entry removedEntry)
        {
            var path = Path.Combine(destinationIndex.BasePath, removedEntry.Path);

            try
            {
                if (removedEntry.Type == EntryType.File)
                {
                    _logger.LogDebug("Deleting file {file}", path);
                    _fileSystem.File.Delete(path);
                }
                else if (removedEntry.Type == EntryType.Directory)
                {
                    _logger.LogDebug("Deleting directory {dir}", path);
                    _fileSystem.Directory.Delete(path);
                }
            }
            catch (Exception ex)
            {
                OnException(path, ex);
                return;
            }

            destinationIndex.RemoveEntry(removedEntry.Path);
        }
    }
}
