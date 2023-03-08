using FileSync.Domain.Abstractions;
using FileSync.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Infrastructure
{
    public class DiffWriter : IDiffWriter
    {
        public async Task WriteDiffAsync(EntryDiff diff, string outputFilePath)
        {
            using var file = File.CreateText(outputFilePath);

            foreach (var additionalFile in diff.AdditionalEntriesInSource)
            {
                await file.WriteAsync("[ADD] ");
                await file.WriteLineAsync(additionalFile.Path);
            }
            
            foreach (var removedFile in diff.RemovedEntriesInSource)
            {
                await file.WriteAsync("[DEL] ");
                await file.WriteLineAsync(removedFile.Path);
            }

            var modGroups = diff.ModifiedEntriesInSource.GroupBy(i => i.ChangeReason);
            
            foreach (var modReasonGroup in modGroups)
            {
                var prefix = modReasonGroup.Key switch
                {
                    ChangeReason.SizeChanged => "[MOD SIZE]",
                    ChangeReason.ModifiedDateChanged => "[MOD DATE]",
                    ChangeReason.ChecksumChanged => "[MOD CHKS]",
                    _ => throw new NotImplementedException(modReasonGroup.Key.ToString())
                };

                foreach (var modifiedFile in modReasonGroup)
                {
                    await file.WriteAsync(prefix);
                    await file.WriteLineAsync(modifiedFile.SourceEntry.Path);
                }
            }
        }
    }
}
