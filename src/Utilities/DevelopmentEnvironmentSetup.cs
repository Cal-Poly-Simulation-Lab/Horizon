
using System;
using System.Diagnostics;
using System.Security.Policy;
using IronPython.Compiler.Ast;


namespace Utilities
{
    public static class DevEnvironment
    {
        // Relative pathing set up:
        public static string ExecutablePath {get; } = Environment.ProcessPath; // Gets the current executable path of Horizon. 
        public static string ExecutableDirectory {get; }= Path.GetDirectoryName(ExecutablePath); // Returns the executable directory of Horizon. 
        
        // Currently, the main repository directory is 5 directories up from where the executable lives within the directory
        // (during Debug and Run mode). Released version(s) of Horizon will likely need to rework this pathing setup. This
        // currently works for development on MacOS and Windows on the newly migrated .NET8 Horizon. 
        public static string RepoDirectory { get; } = Path.GetFullPath(Path.Combine(ExecutableDirectory, @"../../../../../"));

    

        public static string GetTestDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory()); 
             
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

