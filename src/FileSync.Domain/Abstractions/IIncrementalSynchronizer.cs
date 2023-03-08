using FileSync.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Abstractions
{
    public interface IIncrementalSynchronizer
    {
        Action<string, Exception> OnException { get; set; }

        Task SynchronizeAsync(EntryDiff compareResult, FileIndex sourceIndex, FileIndex destinationIndex);
    }
}
