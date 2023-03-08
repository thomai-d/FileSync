using FileSync.Domain.Model;
using FileSync.Domain.Strategies;
using FluentAssertions;

namespace FileSync.Domain.Tests
{
    public class MetadataEntryComparerTests
    {
        [Fact]
        public void Added_Files_Should_Be_Detected()
        {
            var source = new FileIndex("", File("1"), File("2"));
            var dest = new FileIndex("", File("1"));

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.RemovedEntriesInSource.Should().BeEmpty();
            result.ModifiedEntriesInSource.Should().BeEmpty();
            result.AdditionalEntriesInSource
                .Should()
                .BeEquivalentTo(new[] { File("2") });

        }

        [Fact]
        public void Removed_Files_Should_Be_Detected()
        {
            var source = new FileIndex("", File("1"));
            var dest = new FileIndex("", File("1"), File("2"));

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.AdditionalEntriesInSource.Should().BeEmpty();
            result.ModifiedEntriesInSource.Should().BeEmpty();
            result.RemovedEntriesInSource
                .Should()
                .BeEquivalentTo(new[] { File("2") });

        }

        [Fact]
        public void Changed_Files_Should_Be_Detected()
        {
            var source = new FileIndex("",
                Entry.File("size", DateTime.MinValue, DateTime.MinValue, 5),
                Entry.File("modified", DateTime.MinValue, DateTime.MinValue, 5));

            var dest = new FileIndex("",
                Entry.File("size", DateTime.MinValue, DateTime.MinValue, 6),
                Entry.File("modified", DateTime.MinValue, DateTime.MaxValue, 5));

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.AdditionalEntriesInSource.Should().BeEmpty();
            result.ModifiedEntriesInSource.Should().HaveCount(2);
            result.ModifiedEntriesInSource[0].SourceEntry.Path.Should().Be("size");
            result.ModifiedEntriesInSource[0].ChangeReason.Should().Be(ChangeReason.SizeChanged);
            result.ModifiedEntriesInSource[1].SourceEntry.Path.Should().Be("modified");
            result.ModifiedEntriesInSource[1].ChangeReason.Should().Be(ChangeReason.ModifiedDateChanged);
            result.RemovedEntriesInSource.Should().BeEmpty();
        }
        
        [Fact]
        public void Checksum_Changes_Should_Be_Detected_Only_If_Checksum_Is_Set_On_Both_Files()
        {
            var source = new FileIndex("",
                Entry.File("check", DateTime.MinValue, DateTime.MinValue, 5, "123"),
                Entry.File("nocheck", DateTime.MinValue, DateTime.MinValue, 5),
                Entry.File("nocheck2", DateTime.MinValue, DateTime.MinValue, 5, "123"));

            var dest = new FileIndex("",
                Entry.File("check", DateTime.MinValue, DateTime.MinValue, 5, "124"),
                Entry.File("nocheck", DateTime.MinValue, DateTime.MinValue, 5, "123"),
                Entry.File("nocheck2", DateTime.MinValue, DateTime.MinValue, 5));

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.AdditionalEntriesInSource.Should().BeEmpty();
            result.ModifiedEntriesInSource.Should().HaveCount(1);
            result.ModifiedEntriesInSource[0].SourceEntry.Path.Should().Be("check");
            result.ModifiedEntriesInSource[0].ChangeReason.Should().Be(ChangeReason.ChecksumChanged);
            result.RemovedEntriesInSource.Should().BeEmpty();
        }
        
        [Fact]
        public void Modification_Date_Should_Precede_Other_Changes()
        {
            var source = new FileIndex("",
                Entry.File("file", DateTime.MinValue, DateTime.MaxValue, 6, "123"));

            var dest = new FileIndex("",
                Entry.File("file", DateTime.MinValue, DateTime.MinValue, 5, "124"));

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.AdditionalEntriesInSource.Should().BeEmpty();
            result.ModifiedEntriesInSource.Should().HaveCount(1);
            result.ModifiedEntriesInSource[0].SourceEntry.Path.Should().Be("file");
            result.ModifiedEntriesInSource[0].ChangeReason.Should().Be(ChangeReason.ModifiedDateChanged);
            result.RemovedEntriesInSource.Should().BeEmpty();
        }
        
        [Fact]
        public void Size_Change_Should_Precede_Hash_Changes()
        {
            var source = new FileIndex("",
                Entry.File("file", DateTime.MinValue, DateTime.MinValue, 6, "123"));

            var dest = new FileIndex("",
                Entry.File("file", DateTime.MinValue, DateTime.MinValue, 5, "124"));

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.AdditionalEntriesInSource.Should().BeEmpty();
            result.ModifiedEntriesInSource.Should().HaveCount(1);
            result.ModifiedEntriesInSource[0].SourceEntry.Path.Should().Be("file");
            result.ModifiedEntriesInSource[0].ChangeReason.Should().Be(ChangeReason.SizeChanged);
            result.RemovedEntriesInSource.Should().BeEmpty();
        }
        
        [Fact]
        public void Changed_Directories_Should_Be_Ignored()
        {
            var source = new FileIndex("",
                Entry.Dir("dir1", DateTime.MinValue, DateTime.MinValue));
            
            var dest = new FileIndex("",
                Entry.Dir("dir1", DateTime.MinValue, DateTime.MinValue));

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.AdditionalEntriesInSource.Should().BeEmpty();
            result.ModifiedEntriesInSource.Should().BeEmpty();
            result.RemovedEntriesInSource.Should().BeEmpty();
        }
        
        [Fact]
        public void Directory_To_File_Conversions_Should_Be_Handled_Correctly()
        {
            var dir1 = Entry.Dir("dir1", DateTime.MinValue, DateTime.MinValue);
            var file1 = Entry.File("dir1", DateTime.MinValue, DateTime.MinValue, 1);

            var source = new FileIndex("", file1);
            
            var dest = new FileIndex("", dir1);

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.AdditionalEntriesInSource.Should().BeEquivalentTo(new[] { file1 });
            result.ModifiedEntriesInSource.Should().BeEmpty();
            result.RemovedEntriesInSource.Should().BeEquivalentTo(new[] { dir1 });
        }
        
        [Fact]
        public void File_To_Directory_Conversions_Should_Be_Handled_Correctly()
        {
            var dir1 = Entry.Dir("dir1", DateTime.MinValue, DateTime.MinValue);
            var file1 = Entry.File("dir1", DateTime.MinValue, DateTime.MinValue, 1);

            var source = new FileIndex("", dir1);
            
            var dest = new FileIndex("", file1);

            var target = new MetadataEntryComparer();
            var result = target.Compare(source, dest);

            result.AdditionalEntriesInSource.Should().BeEquivalentTo(new[] { dir1 });
            result.ModifiedEntriesInSource.Should().BeEmpty();
            result.RemovedEntriesInSource.Should().BeEquivalentTo(new[] { file1 });
        }

        private static Entry File(string name)
            => Entry.File(name, DateTime.MinValue, DateTime.MinValue, 1);
    }
}