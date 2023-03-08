using Castle.Core.Logging;
using FileSync.Domain.Abstractions;
using FileSync.Domain.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Infrastructure.Tests
{
    public class IncrementalSynchronizerTests
    {
        private static Entry Dir1 = Entry.Dir("dir1", DateTime.MinValue, DateTime.MinValue);
        private static Entry SubDir1 = Entry.Dir("dir1\\subdir", DateTime.MinValue, DateTime.MinValue);
        private static Entry File1 = Entry.File("file1", DateTime.MinValue, DateTime.MinValue, 1);
        private static Entry File1Mod = Entry.File("file1", DateTime.MinValue, DateTime.MinValue, 2);
        private static Entry File2 = Entry.File("file2", DateTime.MinValue, DateTime.MinValue, 1);
        private static Entry File2Mod = Entry.File("file2", DateTime.MinValue, DateTime.MinValue, 2);
        private static Entry File3 = Entry.File("file3", DateTime.MinValue, DateTime.MinValue, 1);
        private static Entry File3Mod = Entry.File("file3", DateTime.MinValue, DateTime.MinValue, 2);

        [Fact]
        public async Task Removed_Entries_Should_Be_Deleted_And_Removed_From_Index_In_Correct_Order()
        {
            var fixture = new AutoMocker(MockBehavior.Strict);
            var sequence = new MockSequence();
            fixture.Use(new Mock<ILogger<IncrementalSynchronizer>>().Object);
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Delete("dest\\file1"));
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Delete("dest\\file2")).Throws<Exception>();
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.Directory.Delete("dest\\dir1\\subdir")); // Subdirs first.
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.Directory.Delete("dest\\dir1"));
            var target = fixture.CreateInstance<IncrementalSynchronizer>();

            var diff = new EntryDiff();
            diff.RemovedEntriesInSource.Add(Dir1);
            diff.RemovedEntriesInSource.Add(SubDir1);
            diff.RemovedEntriesInSource.Add(File1);
            diff.RemovedEntriesInSource.Add(File2);

            var sourceIndex = new FileIndex("source", File2);
            var destIndex = new FileIndex("dest", File1, File2, Dir1, SubDir1);

            await target.SynchronizeAsync(diff, sourceIndex, destIndex);

            destIndex.Entries.Should().BeEquivalentTo(new[] { File2 });

            fixture.GetMock<IFileSystem>().Verify(i => i.File.Delete("dest\\file1"), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.File.Delete("dest\\file2"), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.Directory.Delete("dest\\dir1"), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.Directory.Delete("dest\\dir1\\subdir"), Times.Once());
        }

        [Fact]
        public async Task Added_Entries_Should_Be_Created_And_Added_To_The_Index_In_Correct_Order()
        {
            var fixture = new AutoMocker(MockBehavior.Strict);
            var sequence = new MockSequence();
            fixture.Use(new Mock<ILogger<IncrementalSynchronizer>>().Object);
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.Directory.CreateDirectory("dest\\dir1")).Returns(new Mock<IDirectoryInfo>().Object);
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Copy("source\\file1", "dest\\file1", false));
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Copy("source\\file2", "dest\\file2", false)).Throws<Exception>();
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("source\\file1")).Returns(Task.FromResult("f1"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("source\\file2")).Returns(Task.FromResult("f2"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("dest\\file1")).Returns(Task.FromResult("f1"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("dest\\file2")).Returns(Task.FromResult("f2"));
            var target = fixture.CreateInstance<IncrementalSynchronizer>();

            var diff = new EntryDiff();
            diff.AdditionalEntriesInSource.Add(File1);
            diff.AdditionalEntriesInSource.Add(File2);
            diff.AdditionalEntriesInSource.Add(Dir1);

            var sourceIndex = new FileIndex("source", File1, File2, Dir1);
            var destIndex = new FileIndex("dest");

            await target.SynchronizeAsync(diff, sourceIndex, destIndex);

            destIndex.Entries.Should().Contain(Dir1);
            destIndex.Entries.Should().Contain(File1 with { Hash = "f1" });
            destIndex.Entries.Should().NotContain(File2 with { Hash = "f2" });

            fixture.GetMock<IFileSystem>().Verify(i => i.Directory.CreateDirectory("dest\\dir1"), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.File.Copy("source\\file1", "dest\\file1", false), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.File.Copy("source\\file2", "dest\\file2", false), Times.Once());
        }

        [Fact]
        public async Task Added_Files_Which_Checksum_Changed_When_Copying_Should_Be_Deleted_In_Destination()
        {
            var fixture = new AutoMocker(MockBehavior.Strict);
            var sequence = new MockSequence();
            fixture.Use(new Mock<ILogger<IncrementalSynchronizer>>().Object);
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Copy("source\\file1", "dest\\file1", false));
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Delete("dest\\file1"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("source\\file1")).Returns(Task.FromResult("f1"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("dest\\file1")).Returns(Task.FromResult("x1"));
            var target = fixture.CreateInstance<IncrementalSynchronizer>();

            var diff = new EntryDiff();
            diff.AdditionalEntriesInSource.Add(File1);

            var sourceIndex = new FileIndex("source", File1);
            var destIndex = new FileIndex("dest");

            await target.SynchronizeAsync(diff, sourceIndex, destIndex);

            destIndex.Entries.Should().BeEmpty();

            fixture.GetMock<IFileSystem>().Verify(i => i.File.Copy("source\\file1", "dest\\file1", false), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.File.Delete("dest\\file1"), Times.Once());
        }

        [Fact]
        public async Task Modified_Files_Which_Checksum_Changed_When_Copying_Should_Be_Deleted_In_Destination()
        {
            var fixture = new AutoMocker(MockBehavior.Strict);
            var sequence = new MockSequence();
            fixture.Use(new Mock<ILogger<IncrementalSynchronizer>>().Object);
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Copy("source\\file1", "dest\\file1", true));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("source\\file1")).Returns(Task.FromResult("f1"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("dest\\file1")).Returns(Task.FromResult("x1"));
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Delete("dest\\file1"));
            var target = fixture.CreateInstance<IncrementalSynchronizer>();

            var diff = new EntryDiff();
            diff.ModifiedEntriesInSource.Add(new ModifiedEvent(File1Mod, File1, ChangeReason.SizeChanged));

            var sourceIndex = new FileIndex("source", File1Mod);
            var destIndex = new FileIndex("dest", File1);

            await target.SynchronizeAsync(diff, sourceIndex, destIndex);

            destIndex.Entries.Should().BeEmpty();

            fixture.GetMock<IFileSystem>().Verify(i => i.File.Copy("source\\file1", "dest\\file1", true), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.File.Delete("dest\\file1"), Times.Once());
        }

        [Fact]
        public async Task Modified_Entries_Should_Be_Updated_And_Updated_In_The_Index_Only_On_Success()
        {
            var fixture = new AutoMocker(MockBehavior.Strict);
            var sequence = new MockSequence();
            fixture.Use(new Mock<ILogger<IncrementalSynchronizer>>().Object);
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Copy("source\\file1", "dest\\file1", true));
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Copy("source\\file2", "dest\\file2", true)).Throws<Exception>();
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Copy("source\\file3", "dest\\file3", true));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("source\\file1")).Returns(Task.FromResult("f1"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("source\\file2")).Returns(Task.FromResult("f2"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("dest\\file1")).Returns(Task.FromResult("f1"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("dest\\file2")).Returns(Task.FromResult("f2"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("source\\file3")).Throws<Exception>();
            var target = fixture.CreateInstance<IncrementalSynchronizer>();

            var diff = new EntryDiff();
            diff.ModifiedEntriesInSource.Add(new ModifiedEvent(File1Mod, File1, ChangeReason.SizeChanged));
            diff.ModifiedEntriesInSource.Add(new ModifiedEvent(File2Mod, File2, ChangeReason.SizeChanged));
            diff.ModifiedEntriesInSource.Add(new ModifiedEvent(File3Mod, File3, ChangeReason.SizeChanged));

            var sourceIndex = new FileIndex("source", File1Mod, File2Mod, File3Mod);
            var destIndex = new FileIndex("dest", File1, File2, File3);

            await target.SynchronizeAsync(diff, sourceIndex, destIndex);

            destIndex.Entries.Should().BeEquivalentTo(new Entry[] { File1Mod with { Hash = "f1" }, File2, File3 });

            fixture.GetMock<IFileSystem>().Verify(i => i.File.Copy("source\\file1", "dest\\file1", true), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.File.Copy("source\\file2", "dest\\file2", true), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.File.Copy("source\\file3", "dest\\file3", true), Times.Never(), "failing building checksum should not copy the file because there maybe problems with the file (CRC)");
        }

        [Fact]
        public async Task File_To_Directory_Conversions_Should_Be_Handled_Correctly()
        {
            var dir1 = Entry.Dir("file1", DateTime.MinValue, DateTime.MinValue);

            var fixture = new AutoMocker(MockBehavior.Strict);
            var sequence = new MockSequence();
            fixture.Use(new Mock<ILogger<IncrementalSynchronizer>>().Object);
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Delete("dest\\file1"));
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.Directory.CreateDirectory("dest\\file1")).Returns(new Mock<IDirectoryInfo>().Object);
            var target = fixture.CreateInstance<IncrementalSynchronizer>();

            var diff = new EntryDiff();
            diff.RemovedEntriesInSource.Add(File1);
            diff.AdditionalEntriesInSource.Add(dir1);

            var sourceIndex = new FileIndex("source", File1);
            var destIndex = new FileIndex("dest", dir1);

            await target.SynchronizeAsync(diff, sourceIndex, destIndex);

            destIndex.Entries.Should().BeEquivalentTo(new[] { dir1 });

            fixture.GetMock<IFileSystem>().Verify(i => i.File.Delete("dest\\file1"), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.Directory.CreateDirectory("dest\\file1"), Times.Once());
        }

        [Fact]
        public async Task Directory_To_File_Conversions_Should_Be_Handled_Correctly()
        {
            var file1 = Entry.File("dir1", DateTime.MinValue, DateTime.MinValue, 1);

            var fixture = new AutoMocker(MockBehavior.Strict);
            var sequence = new MockSequence();
            fixture.Use(new Mock<ILogger<IncrementalSynchronizer>>().Object);
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.Directory.Delete("dest\\dir1"));
            fixture.GetMock<IFileSystem>().InSequence(sequence).Setup(i => i.File.Copy("source\\dir1", "dest\\dir1", false));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("source\\dir1")).Returns(Task.FromResult("d1"));
            fixture.GetMock<IChecksumGenerator>().Setup(i => i.GenerateChecksumForFileAsync("dest\\dir1")).Returns(Task.FromResult("d1"));
            var target = fixture.CreateInstance<IncrementalSynchronizer>();

            var diff = new EntryDiff();
            diff.RemovedEntriesInSource.Add(Dir1);
            diff.AdditionalEntriesInSource.Add(file1);

            var sourceIndex = new FileIndex("source", file1);
            var destIndex = new FileIndex("dest", Dir1);

            await target.SynchronizeAsync(diff, sourceIndex, destIndex);

            destIndex.Entries.Should().BeEquivalentTo(new[] { file1 with { Hash = "d1" } });

            fixture.GetMock<IFileSystem>().Verify(i => i.Directory.Delete("dest\\dir1"), Times.Once());
            fixture.GetMock<IFileSystem>().Verify(i => i.File.Copy("source\\dir1", "dest\\dir1", false), Times.Once());
        }
    }
}
