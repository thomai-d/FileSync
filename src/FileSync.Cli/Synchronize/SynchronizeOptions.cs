using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Cli.Synchronize
{
    [Verb("sync", HelpText = "Synchronizes two directories")]
    public class SynchronizeOptions
    {
        [Option('s', "sourcePath", Required = true, HelpText = "Source path for synchronization")]
        public string SourcePath { get; set; } = "";

        [Option('d', "destPath", Required = true, HelpText = "Destination path for synchronization")]
        public string DestinationPath { get; set; } = "";

        [Option('i', "ignore", Required = false, HelpText = "Ignored file pattern (Regex)")]
        public IEnumerable<string> IgnorePatterns { get; set; } = Array.Empty<string>();

        [Option('x', "indexFile", Required = false, HelpText = "File path for an index file to use")]
        public string DestinationIndexFile { get; set; } = "";
        
        [Option('r', "retries", Required = false, HelpText = "Number of sync retries if errors occured")]
        public int Retries { get; set; } = 2;
    }
}
