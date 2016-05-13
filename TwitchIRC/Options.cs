using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TwitchIRC
{
    class Options
    {
        [Option('t', Required = true, HelpText = "Time in ms to fetch comments for, must be greater than 2000 (i.e. 5000)")]
        public uint TimeToFetch { get; set; }

        [Option('c', Required = true, HelpText = "Channels to fetch comments from, separated by a comma (i.e. lirik,esl_csgo)")]
        public string Channels { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
