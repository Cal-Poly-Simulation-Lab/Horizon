
using System;
using System.Diagnostics;
using System.Security.Policy;


namespace Utilities
{
    public static class DevEnvironment
    {
        // Relative pathing set up:
        public static string ExecutablePath {get; } = Environment.ProcessPath; // {get; set;}
        public static string ExecutableDirectory {get; }= Path.GetDirectoryName(ExecutablePath); // Might not be executable directory{get; set;}
        public static string RepoDirectory { get; } = Path.GetFullPath(Path.Combine(ExecutableDirectory, @"../../../../../"));

    }

}

