
using System;
using System.Diagnostics;
using System.Security.Policy;


namespace Utilities
{
    public static class DevEnvironment
    {
        // Relative pathing set up:
        public static string? executablePath {get; } = Process.GetCurrentProcess().MainModule.FileName; // {get; set;}
        public static string? executableDirectory {get; }= Environment.CurrentDirectory; // Might not be executable directory{get; set;}
        public static string? srcDirectory {get; set; }= Path.GetDirectoryName(executableDirectory); // This grabs the directory of the Horizon project (ie "Horizon/src/") {get; set; }
        public static string? repoDirectory {get; set; }= Path.GetDirectoryName(srcDirectory); // {get; set;}


    }

}

