using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Domain.Exceptions
{
    /// <summary>
    /// Exception which is thrown if a copied file's checksum does not match the source file's one.
    /// </summary>
    public class ChecksumChangedOnCopyException : Exception
    {
        public ChecksumChangedOnCopyException(string file, string originalChecksum, string copyChecksum)
            : base($"Checksum of {file} changed during copy")
        {
            OriginalChecksum = originalChecksum;
            CopyChecksum = copyChecksum;
        }

        public string OriginalChecksum { get; }

        public string CopyChecksum { get; }
    }
}
