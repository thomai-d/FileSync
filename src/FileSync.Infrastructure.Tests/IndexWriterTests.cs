using Castle.Core.Logging;
using FileSync.Domain.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Infrastructure.Tests
{
    public class IndexWriterTests : IDisposable
    {
        private readonly string _tempFile;

        public IndexWriterTests()
        {
            _tempFile = Path.GetTempFileName();
        }

        public void Dispose()
        {
            File.Delete(_tempFile);
        }

        [Fact]
        public async Task Persist_Restore_Test()
        {
            var originalIndex = new FileIndex("",
                Entry.File("file1", DateTime.Now, DateTime.Now, 500, "abc"),
                Entry.Dir("dir1", DateTime.Now, DateTime.Now));

            var target = new IndexWriter(new Mock<ILogger<IndexWriter>>().Object);

            await target.PersistIndexAsync(originalIndex, _tempFile);

            var restoredIndex = await target.RestoreFromFileAsync(_tempFile);

            restoredIndex.Entries
                .Should().BeEquivalentTo(originalIndex.Entries);
        }
    }
}
