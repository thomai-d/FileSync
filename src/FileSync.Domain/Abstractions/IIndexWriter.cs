using FileSync.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Abstractions
{
    public interface IIndexWriter
    {
        Task PersistIndexAsync(FileIndex index, string filePath);

        Task<FileIndex> RestoreFromFileAsync(string filePath);
    }
}
