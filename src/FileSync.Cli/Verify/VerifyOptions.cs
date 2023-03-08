using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSync.Cli.Verify
{
    [Verb("verify", HelpText = "Verifies a folder against an image file (diff + check md5s)")]
    public class VerifyOptions
    {
        [Option('s', "sourcePath", Required = true, HelpText = "Source path that should be verified")]
        public string SourcePath { get; set; } = "";

        [Option('i', "ignore", Required = false, HelpText = "Ignored file pattern (Regex)")]
        public IEnumerable<string> IgnorePatterns { get; set; } = Array.Empty<string>();
        
        [Option('x', "indexFile", Required = true, HelpText = "Index file used for verification")]
        public string IndexFile { get; set; } = "";
        
        [Option('o', "output", Required = true, HelpText = "Output file for diff result")]
        public string OutputFilePath { get; set; } = "";
    }
}
