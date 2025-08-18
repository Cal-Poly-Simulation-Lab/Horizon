
using System;
using System.Diagnostics;
using System.Security.Policy;
using IronPython.Compiler.Ast;


namespace Utilities
{
    public static class DevEnvironment
    {
        // Relative pathing set up:
        public static string ExecutablePath {get; private set; } = Environment.ProcessPath; // Gets the current executable path of Horizon. 
        public static string ExecutableDirectory {get; private set; }= Path.GetDirectoryName(ExecutablePath); // Returns the executable directory of Horizon. 
        
        // Currently, the main repository directory is 5 directories up from where the executable lives within the directory
        // (during Debug and Run mode). Released version(s) of Horizon will likely need to rework this pathing setup. This
        // currently works for development on MacOS and Windows on the newly migrated .NET8 Horizon. 
        public static string RepoDirectory { get; private set; } = Path.GetFullPath(Path.Combine(ExecutableDirectory, @"../../../../../"));

        // Testing wy directories are different JB 7/29
        //public static DirectoryInfo testDirectory {get; private set; } = new DirectoryInfo(Directory.GetCurrentDirectory());

        public static string GetTestDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory()); 
            RepoDirectory = Path.GetFullPath(Path.Combine(directory.FullName,@"../../../../../")); //Need to make sure that the test framework gets repo dir right
            
            DirectoryInfo RepoDirectoryInfo = new DirectoryInfo(RepoDirectory);
            if (RepoDirectoryInfo.Name.ToLower() != "horizon")
            {
                Console.WriteLine("RepoDirectory not found corectly in GetTestDirectory() ...");
                // Exception.Equals(RepoDirectoryInfo.Name.ToLower(),"horizon");
            }

            while (directory != null)
            {
                if (directory.Name.Equals("test",StringComparison.OrdinalIgnoreCase))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            return null;

        }

    }

}

