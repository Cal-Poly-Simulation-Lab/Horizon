using IronPython.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics; 
using Utilities;

namespace HSFSystem
{
    public class ScriptedSubsystemHelper
    {
        public dynamic PythonInstance { get; set; }

        // // Relative pathing set up:
        // public static string executablePath = Process.GetCurrentProcess().MainModule.FileName; // {get; set;}
        // public static string executableDirectory = Environment.CurrentDirectory; // Might not be executable directory{get; set;}
        // public static string srcDirectory = Path.GetDirectoryName(executableDirectory); // This grabs the directory of the Horizon project (ie "Horizon/src/") {get; set; }
        // public static string? repoDirectory = Path.GetDirectoryName(srcDirectory); // {get; set;}

        // Start main method: 
        public ScriptedSubsystemHelper()
        {
            var engine = Python.CreateEngine();
            var scope = engine.CreateScope();
            var ops = engine.Operations;
            // Search paths are for importing modules from python scripts, not for executing python subsystem files
            var p = engine.GetSearchPaths();
            p.Add(DevEnvironment.repoDirectory);
            p.Add(Path.Combine(DevEnvironment.repoDirectory,"samples/PythonSubs"));
            p.Add(Path.Combine(DevEnvironment.repoDirectory,"tools"));
            
            engine.SetSearchPaths(p);
            engine.ExecuteFile(Path.Combine(DevEnvironment.repoDirectory,"tools/HSF_Helper.py"), scope);
            var pythonType = scope.GetVariable("HSFHelper");
            PythonInstance = ops.CreateInstance(pythonType);
        }
    }
}
