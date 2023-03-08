using FileSync.Domain.Abstractions;
using FileSync.Domain.Extensions;
using FileSync.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Strategies
{
    public class MetadataEntryComparer : IEntryComparer
    {
        public EntryDiff Compare(FileIndex sourceFiles, FileIndex destFiles)
        {
            var result = new EntryDiff();

            foreach (var sourceFile in sourceFiles.Entries)
            {
                if (destFiles.TryGetEntry(sourceFile.Path, out var destFile))
                {
                    if (sourceFile.Type == EntryType.Directory && destFile.Type == EntryType.Directory)
                        continue;

                    if (destFile.Type != sourceFile.Type)
                    {
                        result.RemovedEntriesInSource.Add(destFile);
                        result.AdditionalEntriesInSource.Add(sourceFile);
                        continue;
                    }

                    var modifiedDiffSeconds = Math.Abs((sourceFile.ModifiedUtc - destFile.ModifiedUtc).TotalSeconds);
                    if (modifiedDiffSeconds > 1)
                    {
                        result.ModifiedEntriesInSource.Add(new ModifiedEvent(sourceFile, destFile, ChangeReason.ModifiedDateChanged));
                        continue;
                    }

                    if (sourceFile.Size != destFile.Size)
                    {
                        result.ModifiedEntriesInSource.Add(new ModifiedEvent(sourceFile, destFile, ChangeReason.SizeChanged));
                        continue;
                    }

                    if (sourceFile.Hash.IsSet()
                     && destFile.Hash.IsSet()
                     && sourceFile.Hash != destFile.Hash)
                    {
                        result.ModifiedEntriesInSource.Add(new ModifiedEvent(sourceFile, destFile, ChangeReason.ChecksumChanged));
                        continue;
                    }
                }
                else
                {
                    result.AdditionalEntriesInSource.Add(sourceFile);
                    continue;
                }
            }

            foreach (var destFile in destFiles.Entries)
            {
                if (!sourceFiles.HasEntry(destFile.Path))
                {
                    result.RemovedEntriesInSource.Add(destFile);
                    continue;
                }
            }

            return result;
        }
    }
}
