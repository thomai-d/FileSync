using FileSync.Domain.Model;

namespace FileSync.Domain.Abstractions
{
    public interface IDirectoryEnumerator
    {
        Action<string, Exception> OnException { get; set; }

        void AddIgnorePattern(string pattern);
        void ClearIgnorePatterns();

        FileIndex Enumerate(string basePath);
    }
}