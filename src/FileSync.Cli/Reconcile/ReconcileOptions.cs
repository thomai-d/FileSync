using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Cli.Index
{
    [Verb("reconcile", HelpText = "Reconciles an index by removing files that are no longer present and add existing files")]
    public class ReconcileOptions
    {
        [Option('d', "DestinationPath", Required = true, HelpText = "Destination path to reconcile")]
        public string DestinationPath { get; set; } = "";

        [Option('i', "ignore", Required = false, HelpText = "Ignored file pattern (Regex)")]
        public IEnumerable<string> IgnorePatterns { get; set; } = Array.Empty<string>();

        [Option('x', "indexFile", Required = true, HelpText = "File path for an index file to use")]
        public string IndexFile { get; set; } = "";

        [Option('c', "checksum", Required = false, HelpText = "Generate checksums")]
        public bool IsChecksumGenerationEnabled { get; set; } = false;
    }
}
