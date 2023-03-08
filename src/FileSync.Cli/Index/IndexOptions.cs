using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Cli.Index
{
    [Verb("index", HelpText = "Creates a file index")]
    public class IndexOptions
    {
        [Option('s', "sourcePath", Required = true, HelpText = "Source path for indexing")]
        public string SourcePath { get; set; } = "";

        [Option('i', "ignore", Required = false, HelpText = "Ignored file pattern (Regex)")]
        public IEnumerable<string> IgnorePatterns { get; set; } = Array.Empty<string>();

        [Option('x', "indexFile", Required = true, HelpText = "File path for an index file to use")]
        public string DestinationIndexFile { get; set; } = "";

        [Option('c', "checksum", Required = false, HelpText = "Generate checksums")]
        public bool IsChecksumGenerationEnabled { get; set; } = false;
    }
}
