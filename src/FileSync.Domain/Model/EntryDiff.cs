using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Model
{
    public class EntryDiff
    {
        public List<Entry> AdditionalEntriesInSource { get; } = new List<Entry>();
        
        public List<Entry> RemovedEntriesInSource { get; } = new List<Entry>();
        
        public List<ModifiedEvent> ModifiedEntriesInSource { get; } = new List<ModifiedEvent>();

        public long TotalChanges =>
            AdditionalEntriesInSource.Count
          + RemovedEntriesInSource.Count
          + ModifiedEntriesInSource.Count;
    }

    public record ModifiedEvent(Entry SourceEntry, Entry TargetEntry, ChangeReason ChangeReason);

    public enum ChangeReason
    {
        SizeChanged,

        ModifiedDateChanged,

        ChecksumChanged
    }
}
