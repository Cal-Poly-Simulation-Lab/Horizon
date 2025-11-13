// Copyright (c) 2016 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using UserModel;

namespace Horizon
{
    /// <summary>
    /// Captures all Console.WriteLine output and writes to both console and a log file
    /// with comprehensive run metadata header
    /// </summary>
    public class ConsoleLogger : TextWriter
    {
        private TextWriter _originalOut;
        private StreamWriter _fileWriter;
        private StringBuilder _buffer;
        private string _outputPath;
        private string _scenarioName;
        private string _simInputFile;
        private string _modelInputFile;
        private string _taskInputFile;
        private DateTime _runDateTime;

        public override Encoding Encoding => Encoding.UTF8;

        public ConsoleLogger(string outputPath, string scenarioName, 
                            string simInputFile, string modelInputFile, string taskInputFile,
                            DateTime runDateTime)
        {
            _originalOut = Console.Out;
            _buffer = new StringBuilder();
            _outputPath = outputPath;
            _scenarioName = scenarioName;
            _simInputFile = simInputFile;
            _modelInputFile = modelInputFile;
            _taskInputFile = taskInputFile;
            _runDateTime = runDateTime;
        }

        public void StartLogging()
        {
            // Create the run_log.txt file
            string logFilePath = Path.Combine(_outputPath, "run_log.txt");
            _fileWriter = new StreamWriter(logFilePath, false, Encoding.UTF8);
            _fileWriter.AutoFlush = true;

            // Save git state to .repo_state directory
            SaveGitState();

            // Write comprehensive header
            WriteHeader();

            // Redirect Console.Out to this logger
            Console.SetOut(this);
        }

        private void SaveGitState()
        {
            try
            {
                string repoRoot = Utilities.DevEnvironment.RepoDirectory;
                string repoStateDir = Path.Combine(_outputPath, ".repo_state");
                Directory.CreateDirectory(repoStateDir);

                // Get git information
                string gitBranch = GetGitInfo("rev-parse --abbrev-ref HEAD");
                string gitCommit = GetGitInfo("rev-parse HEAD");
                string gitOrigin = GetGitInfo("config --get remote.origin.url");
                string statusOutput = GetGitInfo("status --porcelain");
                string diffOutput = GetGitInfo("diff HEAD");
                string diffStat = GetGitInfo("diff HEAD --stat");

                // Save git status
                File.WriteAllText(Path.Combine(repoStateDir, "git_status.txt"), statusOutput);

                // Save git diff with header
                var diffWithHeader = new StringBuilder();
                diffWithHeader.AppendLine("================================================================================");
                diffWithHeader.AppendLine("GIT DIFF - Uncommitted Changes");
                diffWithHeader.AppendLine("================================================================================");
                diffWithHeader.AppendLine($"Base Commit: {gitCommit}");
                diffWithHeader.AppendLine($"Branch: {gitBranch}");
                diffWithHeader.AppendLine($"Generated: {_runDateTime:yyyy-MM-dd HH:mm:ss}");
                diffWithHeader.AppendLine();
                diffWithHeader.AppendLine("File Changes Summary:");
                diffWithHeader.AppendLine("--------------------------------------------------------------------------------");
                diffWithHeader.AppendLine(diffStat);
                diffWithHeader.AppendLine("================================================================================");
                diffWithHeader.AppendLine();
                diffWithHeader.AppendLine(diffOutput);
                File.WriteAllText(Path.Combine(repoStateDir, "git_diff.txt"), diffWithHeader.ToString());

                // Parse status to separate modified and untracked files
                var statusLines = statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var modifiedFiles = new List<string>();
                var untrackedFiles = new List<string>();
                
                foreach (var line in statusLines)
                {
                    if (line.StartsWith("??"))
                    {
                        untrackedFiles.Add(line.Substring(3).Trim());
                    }
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        modifiedFiles.Add(line.Trim());
                    }
                }

                // Create README explaining repo state files
                var readme = new StringBuilder();
                readme.AppendLine("# Repository State Snapshot");
                readme.AppendLine();
                readme.AppendLine("This directory contains the exact git repository state at the time of the run.");
                readme.AppendLine();
                readme.AppendLine("## Quick Reference");
                readme.AppendLine($"- **Branch**: {gitBranch}");
                readme.AppendLine($"- **Commit**: {gitCommit}");
                readme.AppendLine($"- **Origin**: {gitOrigin}");
                readme.AppendLine($"- **Captured**: {_runDateTime:yyyy-MM-dd HH:mm:ss}");
                readme.AppendLine($"- **Modified Files**: {modifiedFiles.Count}");
                readme.AppendLine($"- **Untracked Files**: {untrackedFiles.Count}");
                readme.AppendLine();
                readme.AppendLine("## Files");
                readme.AppendLine("- **`git_status.txt`**: Machine-readable list of modified/untracked files");
                readme.AppendLine("- **`git_diff.txt`**: Complete diff of all uncommitted changes to tracked files");
                readme.AppendLine();
                
                if (untrackedFiles.Count > 0)
                {
                    readme.AppendLine("## Untracked Files");
                    readme.AppendLine("The following files were untracked (not in git diff):");
                    foreach (var file in untrackedFiles)
                    {
                        readme.AppendLine($"- `{file}`");
                    }
                    readme.AppendLine();
                }
                
                readme.AppendLine("## How to Reproduce Exact State");
                readme.AppendLine("1. Clone the repository:");
                readme.AppendLine($"   ```bash");
                readme.AppendLine($"   git clone {gitOrigin}");
                readme.AppendLine($"   ```");
                readme.AppendLine();
                readme.AppendLine("2. Checkout the exact commit:");
                readme.AppendLine($"   ```bash");
                readme.AppendLine($"   git checkout {gitCommit}");
                readme.AppendLine($"   ```");
                readme.AppendLine();
                readme.AppendLine("3. Apply uncommitted changes to tracked files:");
                readme.AppendLine($"   ```bash");
                readme.AppendLine($"   git apply .repo_state/git_diff.txt");
                readme.AppendLine($"   ```");
                readme.AppendLine();
                
                if (untrackedFiles.Count > 0)
                {
                    readme.AppendLine("4. **IMPORTANT**: Manually recreate untracked files listed above");
                    readme.AppendLine("   (Untracked files cannot be included in git diff)");
                    readme.AppendLine();
                }
                
                readme.AppendLine("## Notes");
                readme.AppendLine("- If `git_status.txt` is empty, the working tree was clean (no uncommitted changes)");
                readme.AppendLine("- If `git_diff.txt` has no content after the header, there were no modifications to tracked files");
                readme.AppendLine("- Untracked files (marked with `??` in status) cannot be captured in a diff");
                readme.AppendLine("- For complete reproducibility with untracked files, manually copy them from the original workspace");
                
                File.WriteAllText(Path.Combine(repoStateDir, "README.md"), readme.ToString());
            }
            catch
            {
                // Non-critical - continue even if git state capture fails
            }
        }

        private void WriteHeader()
        {
            string repoRoot = Utilities.DevEnvironment.RepoDirectory;
            string executablePath = Process.GetCurrentProcess().MainModule?.FileName ?? "Unknown";
            
            // Get git information
            string gitBranch = GetGitInfo("rev-parse --abbrev-ref HEAD");
            string gitCommit = GetGitInfo("rev-parse HEAD");
            string gitOrigin = GetGitInfo("config --get remote.origin.url");
            string gitUser = GetGitInfo("config user.name");
            string gitStatus = GetGitInfo("status --porcelain");

            // Determine if working tree is clean
            bool isClean = string.IsNullOrWhiteSpace(gitStatus);
            string workingTreeStatus = isClean ? "Clean" : "DIRTY (see .repo_state/ for details)";

            // Get relative paths
            string simRelPath = GetRelativePath(repoRoot, _simInputFile);
            string modelRelPath = GetRelativePath(repoRoot, _modelInputFile);
            string taskRelPath = GetRelativePath(repoRoot, _taskInputFile);
            string execRelPath = GetRelativePath(repoRoot, executablePath);

            var header = new StringBuilder();
            header.AppendLine("================================================================================");
            header.AppendLine($"Date and Time: {_runDateTime:yyyy-MM-dd HH:mm:ss}");
            header.AppendLine();
            header.AppendLine($"Run Scenario Name: {_scenarioName}");
            header.AppendLine();
            header.AppendLine($"Git Repository Root (Absolute Local Path): {repoRoot}");
            header.AppendLine($"Git Repo Origin URL: {gitOrigin}");
            header.AppendLine($"Git Branch (ran): {gitBranch}");
            header.AppendLine($"Git Commit (ran): {gitCommit}");
            header.AppendLine($"Git Working Tree Status: {workingTreeStatus}");
            if (!isClean)
            {
                header.AppendLine($"  → Exact git repo state data saved in ./.repo_state/");
            }
            header.AppendLine($"Git User (who ran): {gitUser}");
            header.AppendLine();
            header.AppendLine($"Local File Executable that was ran (absolute path): {executablePath}");
            header.AppendLine($"Repo-Relative executable path: {execRelPath}");
            header.AppendLine();

            // Check if input files are in repo
            bool allInRepo = !string.IsNullOrEmpty(simRelPath) && !string.IsNullOrEmpty(modelRelPath) && !string.IsNullOrEmpty(taskRelPath);
            
            header.AppendLine("Relative Input Paths (from repo root):");
            if (allInRepo)
            {
                header.AppendLine($"  SimInputFile: {simRelPath}");
                header.AppendLine($"  ModelInputFile: {modelRelPath}");
                header.AppendLine($"  TaskInputFile: {taskRelPath}");
            }
            else
            {
                if (!string.IsNullOrEmpty(simRelPath))
                    header.AppendLine($"  SimInputFile: {simRelPath}");
                else
                    header.AppendLine("  SimInputFile: (not in repo)");
                    
                if (!string.IsNullOrEmpty(modelRelPath))
                    header.AppendLine($"  ModelInputFile: {modelRelPath}");
                else
                    header.AppendLine("  ModelInputFile: (not in repo)");
                    
                if (!string.IsNullOrEmpty(taskRelPath))
                    header.AppendLine($"  TaskInputFile: {taskRelPath}");
                else
                    header.AppendLine("  TaskInputFile: (not in repo)");

                header.AppendLine();
                header.AppendLine("InputFile(s) not found in repo (absolute paths):");
                if (string.IsNullOrEmpty(simRelPath))
                    header.AppendLine($"  {_simInputFile}");
                if (string.IsNullOrEmpty(modelRelPath))
                    header.AppendLine($"  {_modelInputFile}");
                if (string.IsNullOrEmpty(taskRelPath))
                    header.AppendLine($"  {_taskInputFile}");
            }

            header.AppendLine();
            header.AppendLine("================================================================================");
            header.AppendLine("All console output:");
            header.AppendLine();

            // Write to file only (not to console)
            _fileWriter.Write(header.ToString());
        }

        private string GetGitInfo(string gitCommand)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = gitCommand,
                        WorkingDirectory = Utilities.DevEnvironment.RepoDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                return string.IsNullOrEmpty(output) ? "N/A" : output;
            }
            catch
            {
                return "N/A";
            }
        }

        private string GetRelativePath(string repoRoot, string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                    return "";
                
                // Convert to absolute path if it's relative
                string absolutePath;
                if (!Path.IsPathRooted(filePath))
                {
                    // Relative path - resolve it from current directory
                    absolutePath = Path.GetFullPath(filePath);
                }
                else
                {
                    absolutePath = filePath;
                }
                
                // Check if the absolute path is within the repo
                if (!absolutePath.StartsWith(repoRoot))
                    return "";
                    
                string relativePath = absolutePath.Substring(repoRoot.Length);
                // Remove leading slash or backslash
                if (relativePath.StartsWith("/") || relativePath.StartsWith("\\"))
                    relativePath = relativePath.Substring(1);
                    
                return relativePath;
            }
            catch
            {
                return "";
            }
        }

        public override void Write(char value)
        {
            _originalOut.Write(value);
            _fileWriter?.Write(value);
        }

        public override void Write(string value)
        {
            _originalOut.Write(value);
            _fileWriter?.Write(value);
        }

        public override void WriteLine(string value)
        {
            _originalOut.WriteLine(value);
            _fileWriter?.WriteLine(value);
        }

        public override void WriteLine()
        {
            _originalOut.WriteLine();
            _fileWriter?.WriteLine();
        }

        public void StopLogging()
        {
            // Restore original console output
            Console.SetOut(_originalOut);
            
            // Close file writer
            _fileWriter?.Flush();
            _fileWriter?.Close();
            _fileWriter?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopLogging();
            }
            base.Dispose(disposing);
        }
    }
}

