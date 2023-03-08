using FileSync.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Abstractions
{
    public interface IDiffWriter
    {
        Task WriteDiffAsync(EntryDiff diff, string outputFilePath);
    }
}
