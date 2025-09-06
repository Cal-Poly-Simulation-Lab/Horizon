
using System;
using System.Diagnostics;
using System.Security.Policy;
using IronPython.Compiler.Ast;

namespace Utilities
{
    public static class DevEnvironment
    {
        public static string RepoDirectory { get; private set; }

        static DevEnvironment()
        {
            RepoDirectory = FindRepoDirectory();
        }

        private static string FindRepoDirectory()
        {
            // Get the location of the current assembly (HSFSystem.dll, Utilities.dll, etc.)
            // This will always be in your build output directory
            string assemblyLocation = typeof(DevEnvironment).Assembly.Location;
            string assemblyDir = Path.GetDirectoryName(assemblyLocation);
            
            // From the assembly directory, walk up to find the repo root
            // Assembly is in: src/HSFSystem/bin/Debug/net8.0/ (or similar)
            // Need to go up 4 levels to get to repo root
            DirectoryInfo dir = new DirectoryInfo(assemblyDir);
            
            // Go up 4 levels: bin/Debug/net8.0 -> HSFSystem -> src -> repo root
            for (int i = 0; i < 5; i++)
            {
                if (dir?.Parent != null)
                    dir = dir.Parent;
                else
                    break;
            }
            
            if (dir != null && (Directory.Exists(Path.Combine(dir.FullName, ".git")) || 
                                File.Exists(Path.Combine(dir.FullName, "Horizon.sln"))))
            {
                return dir.FullName;
            }
            
            throw new InvalidOperationException($"Could not find Horizon repository directory from assembly location: {assemblyLocation}");
        }

        public static string GetTestDirectory()
        {
            // Use the already-calculated RepoDirectory instead of recalculating it
            string repoDir = RepoDirectory;
            
            DirectoryInfo RepoDirectoryInfo = new DirectoryInfo(repoDir);
            if (RepoDirectoryInfo.Name.ToLower() != "horizon")
            {
                Console.WriteLine("RepoDirectory not found correctly in GetTestDirectory() ...");
            }

            // Look for test directory starting from repo root
            DirectoryInfo testDir = new DirectoryInfo(Path.Combine(repoDir, "test"));
            if (testDir.Exists)
                {
                return testDir.FullName;
                }
            
            return null;
        }
    }
}

