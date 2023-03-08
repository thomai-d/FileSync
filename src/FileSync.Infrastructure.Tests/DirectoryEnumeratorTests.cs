using FileSync.Domain.Model;
using FluentAssertions;
using Moq.AutoMock;

namespace FileSync.Infrastructure.Tests
{
    public class DirectoryEnumeratorTests : IDisposable
    {
        private readonly string _tempFolder;

        public DirectoryEnumeratorTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempFolder);
        }

        public void Dispose()
        {
            Directory.EnumerateDirectories(_tempFolder, "*", SearchOption.AllDirectories)
                .ToList()
                .ForEach(dir =>
                {
                    new DirectoryInfo(dir).Attributes &= ~FileAttributes.ReadOnly;
                });

            Directory.EnumerateFiles(_tempFolder, "*", SearchOption.AllDirectories)
                .ToList()
                .ForEach(dir =>
                {
                    new FileInfo(dir).Attributes &= ~FileAttributes.ReadOnly;
                });

            Directory.Delete(_tempFolder, true);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Directories_Should_Be_Enumerated_As_Relative_Paths(bool trailingSlash)
        {
            var target = new AutoMocker()
                .CreateInstance<DirectoryEnumerator>();

            CreateFile(Path.Combine(_tempFolder, "file.txt"), FileAttributes.Normal);
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root1"));
            CreateFile(Path.Combine(_tempFolder, "root1", "file.txt"), FileAttributes.Normal);
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root2"));
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root1", "child1"));
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root2", "child2"));

            var folder = _tempFolder + (trailingSlash ? Path.DirectorySeparatorChar : "");

            var result = target
                .Enumerate(folder);

            result.Entries
              .Should()
              .BeEquivalentTo(new[]
              {
                  new { Type = EntryType.File, Path = "file.txt"},
                  new { Type = EntryType.File, Path = "root1\\file.txt"},
                  new { Type = EntryType.Directory, Path = "root1"},
                  new { Type = EntryType.Directory, Path = "root2"},
                  new { Type = EntryType.Directory, Path = "root1\\child1" },
                  new { Type = EntryType.Directory, Path = "root2\\child2" },
              });
        }

        [Fact]
        public void Special_Directories_Should_Be_Enumerated()
        {
            var target = new AutoMocker()
                .CreateInstance<DirectoryEnumerator>();

            var hiddenDir = Directory.CreateDirectory(Path.Combine(_tempFolder, "hidden"));
            CreateFile(Path.Combine(_tempFolder, "hidden", "hiddenfile.txt"), FileAttributes.Hidden);

            var systemDir = Directory.CreateDirectory(Path.Combine(_tempFolder, "system"));
            CreateFile(Path.Combine(_tempFolder, "system", "systemfile.txt"), FileAttributes.System);

            var readOnlyDir = Directory.CreateDirectory(Path.Combine(_tempFolder, "readonly"));
            CreateFile(Path.Combine(_tempFolder, "readonly", "readonly.txt"), FileAttributes.ReadOnly);

            hiddenDir.Attributes |= FileAttributes.Hidden;
            systemDir.Attributes |= FileAttributes.System;
            readOnlyDir.Attributes |= FileAttributes.ReadOnly;

            var result = target
                .Enumerate(_tempFolder);

            result.Entries
              .Select(i => new { i.Type, i.Path })
              .Should()
              .BeEquivalentTo(new[]
              {
                  new { Type = EntryType.Directory, Path = "hidden" },
                  new { Type = EntryType.File, Path = "hidden\\hiddenfile.txt"},
                  new { Type = EntryType.Directory, Path = "system" },
                  new { Type = EntryType.File, Path = "system\\systemfile.txt"},
                  new { Type = EntryType.Directory, Path = "readonly" },
                  new { Type = EntryType.File, Path = "readonly\\readonly.txt"},
              });
        }

        [Fact]
        public void Access_Denied_On_Directories_Should_Be_Handled()
        {
            var target = new AutoMocker()
                .CreateInstance<DirectoryEnumerator>();

            var throwDirectory = Path.Combine(_tempFolder, "root1");
            Directory.CreateDirectory(throwDirectory);
            CreateFile(Path.Combine(_tempFolder, "root1", "file.txt"), FileAttributes.Normal);
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root2"));
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root1", "child1"));
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root2", "child2"));

            target.OnBeforeEnumerateDirectory = dir =>
            {
                if (dir == throwDirectory)
                    throw new Exception();
            };

            var result = target
                .Enumerate(_tempFolder);

            result.Entries
              .Should()
              .BeEquivalentTo(new[]
              {
                  new { Type = EntryType.Directory, Path = "root1"},
                  new { Type = EntryType.Directory, Path = "root2"},
                  new { Type = EntryType.Directory, Path = "root2\\child2"},
              });
        }

        [Fact]
        public void Ignore_Patterns_Should_Be_Respected()
        {
            var target = new AutoMocker()
                .CreateInstance<DirectoryEnumerator>();

            CreateFile(Path.Combine(_tempFolder, "rootfile.txt"), FileAttributes.Normal);
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root1"));
            CreateFile(Path.Combine(_tempFolder, "root1", "file.txt"), FileAttributes.Normal);
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root2"));
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root1", "child1"));
            Directory.CreateDirectory(Path.Combine(_tempFolder, "root2", "child2"));

            target.AddIgnorePattern("(\\\\)?root2$");
            target.AddIgnorePattern("rootfile.txt$");

            var result = target
                .Enumerate(_tempFolder);

            result.Entries
              .Should()
              .BeEquivalentTo(new[]
              {
                  new { Type = EntryType.File, Path = "root1\\file.txt" },
                  new { Type = EntryType.Directory, Path = "root1" },
                  new { Type = EntryType.Directory, Path = "root1\\child1" },
              });
        }

        private void CreateFile(string path, FileAttributes attributes)
        {
            File.WriteAllText(path, "1");
            new FileInfo(path).Attributes |= attributes;
        }
    }
}