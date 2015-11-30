using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace GuitarSynthesizer
{
    internal class Options 
    {
        [Option('i', "input", Required = true,
            HelpText = "Input file (*.txt, *.mid, *.midi)")]
        public String InputFileName { get; set; }

        [Option('e', "export", Required = false,
            HelpText = " Output song file name")]
        public String ExportFileName { get; set; }

        [Option('s', "separated", Required = false,
            HelpText = "Export every track into separate file")]
        public bool ExportSeparated { get; set; }
    }
}
