using FileSync.Domain.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Abstractions
{
    public interface IChecksumGenerator
    {
        Action<string, Exception> OnException { get; set; }

        Task GenerateChecksumsAsync(FileIndex index, bool forceRegenerateExisting);

        Task<string> GenerateChecksumForFileAsync(string path);
    }
}
