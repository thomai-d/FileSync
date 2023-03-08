using FileSync.Domain.Abstractions;
using FileSync.Domain.Model;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace FileSync.Infrastructure
{
    public class DirectoryEnumerator : IDirectoryEnumerator
    {
        private List<Regex> _ignorePatterns = new List<Regex>();

        public DirectoryEnumerator(ILogger<DirectoryEnumerator> logger)
        {
            _logger = logger;
        }

        public void AddIgnorePattern(string pattern)
        {
            _ignorePatterns.Add(new Regex(pattern, RegexOptions.Compiled));
        }

        public FileIndex Enumerate(string basePath)
        {
            var normalizedBasePath = NormalizeDirectory(basePath);

            _logger.LogInformation("Enumerating {path}...", normalizedBasePath);

            var watch = Stopwatch.StartNew();

            var entries = EnumerateCore(normalizedBasePath, normalizedBasePath);

            var index = new FileIndex(normalizedBasePath, entries);

            _logger.LogInformation("Enumerating {path} ({count} items, {size} MB) took {time}", normalizedBasePath, index.EntryCount, index.Size/1024/1024, watch.Elapsed);

            return index;
        }

        public Action<string, Exception> OnException { get; set; } = delegate { };

        public void ClearIgnorePatterns()
        {
            this._ignorePatterns.Clear();
        }

        internal Action<string> OnBeforeEnumerateDirectory = delegate { };

        private readonly ILogger<DirectoryEnumerator> _logger;

        private IEnumerable<Entry> EnumerateCore(string fullBasePath, string fullCurrentPath)
        {
            IEnumerable<string>? files;

            try
            {
                OnBeforeEnumerateDirectory(fullCurrentPath);
                files = Directory.EnumerateFiles(fullCurrentPath);
            }
            catch (Exception ex)
            {
                OnException(fullCurrentPath, ex);
                yield break;
            }

            foreach (var file in files)
            {
                if (_ignorePatterns.Any(p => p.IsMatch(file)))
                    continue;

                yield return BuildFileMetadata(fullBasePath, file);
            }

            foreach (var directory in Directory.EnumerateDirectories(fullCurrentPath))
            {
                if (_ignorePatterns.Any(p => p.IsMatch(directory)))
                    continue;

                Entry? entry = null;
                try
                {
                    entry = BuildDirectoryMetadata(fullBasePath, directory);
                }
                catch (Exception ex)
                {
                    OnException(fullCurrentPath, ex);
                }

                if (entry is not null)
                    yield return entry;

                foreach (var item in EnumerateCore(fullBasePath, directory))
                    yield return item;
            }
        }

        private Entry BuildDirectoryMetadata(string fullBasePath, string directory)
        {
            var relPath = directory.Substring(fullBasePath.Length);
            var dirInfo = new DirectoryInfo(relPath);
            return Entry.Dir(relPath, dirInfo.CreationTimeUtc, dirInfo.LastWriteTimeUtc);
        }

        private Entry BuildFileMetadata(string fullBasePath, string file)
        {
            var relPath = file.Substring(fullBasePath.Length);
            var fileInfo = new FileInfo(file);
            return Entry.File(relPath, fileInfo.CreationTimeUtc, fileInfo.LastWriteTimeUtc, fileInfo.Length);
        }

        private string NormalizeDirectory(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (fullPath.EndsWith(Path.DirectorySeparatorChar))
                return fullPath;
            return fullPath + Path.DirectorySeparatorChar;
        }
    }
}