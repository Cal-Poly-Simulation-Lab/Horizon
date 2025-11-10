using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using MissionElements;
using UserModel;
using HSFUniverse;
using Utilities;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json.Linq;
using Microsoft.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Mono.Unix.Native;

namespace HSFSystem
{ 
    public class ScriptedSubsystemCS
    {
        public Subsystem LoadedSubsystem { get; private set; }
        private readonly string src = "";
        private readonly string Type = "scriptedcs"; // Make sure to change this default value ~IF~ name is changed in JSON input files
        private readonly string dll = ""; 
        private readonly string className = ""; 
        private readonly string dlldir = ""; 
        private readonly Type[] constructorArgTypes = [typeof(JObject), typeof(Asset)]; 
        private readonly object[] constructorArgs = [];
        private readonly Asset asset;

        private static readonly string CompiledFilesListPath = Path.Combine(Utilities.DevEnvironment.RepoDirectory, "user-compiled-subsystems.txt");

        public ScriptedSubsystemCS(JObject scriptedSubsystemJson, Asset asset)//string dllPath, string typeName, string subsystemJson)
        {
            this.asset = asset;
           StringComparison stringCompare = StringComparison.CurrentCultureIgnoreCase;

            // Before we can set any subsytem parameters, we must first load in the file...

            // Load in the dll / src file:
            if (scriptedSubsystemJson.TryGetValue("dll",stringCompare, out JToken dllJason))
            {
                this.dll = dllJason.ToString().Replace('\\','/');
                if (!File.Exists(dll)) // Make it a relative (repo) path if the file doesn't exist given by src
                { this.dll = Path.Combine(Utilities.DevEnvironment.RepoDirectory, dll); }

            }
            else if (scriptedSubsystemJson.TryGetValue("src", stringCompare, out JToken srcJason))
            {
                this.src = srcJason.ToString().Replace('\\','/');
                if (!File.Exists(src)) // Make it a relative (repo) path if the file doesn't exist given by src
                { this.src = Path.Combine(Utilities.DevEnvironment.RepoDirectory, src); } //Replace backslashes with forward slashes, if applicable 

                string executableDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                this.dll = CompileDll(this.src, executableDir);
                //this.dll = CompileDll(this.src,Path.Combine(Directory.GetParent(src).FullName,"bin"));

            }
            else
            {
                Console.WriteLine($"Error loading subsytem of type {this.Type}, missing Src/dll attribute");
                throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing Src attribute");
            }

            // Load in the className:
            if (scriptedSubsystemJson.TryGetValue("className", stringCompare, out JToken classNameJason))
            {
                this.className = classNameJason.ToString();
            }
            else
            {
                Console.WriteLine($"Error loading subsytem of type {this.Type}, missing ClassName attribute");
                throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing ClassName attribute");
            }


            // Load in the (optional) constructor args: 
            if (scriptedSubsystemJson.TryGetValue("constructorArgTypes", stringCompare, out JToken constructorArgTypesJason))
            {
                // Add fucntionality to parse all argtypes from input json file... -JB 7/31/24
                Console.WriteLine("Loading subsytem of type {this.Type}, constructorArgTypes not yet functional...");
                
                // For now: 
                this.constructorArgs = [scriptedSubsystemJson, this.asset]; // Use the JObject json and Asset by default.  Using default JObject tpye..."); 
            }
            else
            {
                this.constructorArgs = [scriptedSubsystemJson, this.asset]; // Use the JObject json and Asset by default. 
                Console.WriteLine($"Loading subsytem of type {this.Type}, using deafault {this.constructorArgTypes}...");
                
                //Optional so do not throw an error (assume that there is a JObject, Asset constructor); Error caught later...
            }

            // Now load the subsytem from the file and other (optional) arguments
            this.LoadedSubsystem = LoadSubsystemFromDll();
            
            // Override the Type to match the actual loaded class (not "scriptedcs")
            // The base Subsystem constructor set Type from JSON (which says "scriptedcs"), 
            // but the actual subsystem type should be the class name (e.g., "ADCS", "Power")
            LoadedSubsystem.Type = LoadedSubsystem.GetType().Name.ToLower();
            
            Console.WriteLine($"  Loaded subsystem: Name={LoadedSubsystem.Name}, Type={LoadedSubsystem.Type}, Asset={LoadedSubsystem.Asset?.Name ?? "null"}");
            
            LoadedSubsystem.Loader = this; // Could create an infinite loop? idk..
            
            // Finally, SubsystemFactory.cs will take the 'LoadedSubsystem' object out of this loader class as the 
            // subsystem is loaded into HSF.

        }
        
        public string CompileDll(string sourceFilePath, string outputDirectory)
        {
            if (!File.Exists(sourceFilePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string outputDllPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(sourceFilePath) + ".dll");

            // Read the source code
            string sourceCode = File.ReadAllText(sourceFilePath);
            string absoluteSourcePath = Path.GetFullPath(sourceFilePath);

            // Create a syntax tree from the source code with encoding
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, path: absoluteSourcePath, encoding: System.Text.Encoding.UTF8);

            // Algorithmic reference discovery using Basic.Reference.Assemblies
            // This provides the standard .NET reference assemblies needed for compilation
            var references = Basic.Reference.Assemblies.Net80.References.All.ToList();
            
            // Parse the source file for 'using' directives and add corresponding assemblies
            var root = syntaxTree.GetRoot();
            var usingDirectives = root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>()
                .Select(u => u.Name?.ToString())
                .Where(n => n != null)
                .Distinct()
                .ToList();
            
            Console.WriteLine($"  Found {usingDirectives.Count} using directives in source file");
            
            // Map common using directives to their assembly locations
            var namespaceToAssemblyMap = new Dictionary<string, string[]>
            {
                { "System", new[] { "System.Runtime.dll", "System.Console.dll" } },
                { "System.Collections.Generic", new[] { "System.Collections.dll" } },
                { "System.Linq", new[] { "System.Linq.dll" } },
                { "System.IO", new[] { "System.IO.FileSystem.dll" } },
                { "System.Xml", new[] { "System.Xml.ReaderWriter.dll" } },
                { "Newtonsoft.Json.Linq", new[] { "Newtonsoft.Json.dll" } },
                { "log4net", new[] { "log4net.dll" } },
                { "Microsoft.CSharp", new[] { "Microsoft.CSharp.dll" } }
            };
            
            // Add references for each using directive found
            string horizonBuildDir = Path.Combine(Utilities.DevEnvironment.RepoDirectory, "src/Horizon/bin/Debug/net8.0");
            foreach (var usingDirective in usingDirectives)
            {
                if (namespaceToAssemblyMap.TryGetValue(usingDirective, out var assemblyNames))
                {
                    foreach (var assemblyName in assemblyNames)
                    {
                        string assemblyPath = Path.Combine(horizonBuildDir, assemblyName);
                        if (File.Exists(assemblyPath))
                        {
                            try
                            {
                                references.Add(MetadataReference.CreateFromFile(assemblyPath));
                                Console.WriteLine($"  Added {assemblyName} for using {usingDirective}");
                            }
                            catch { }
                        }
                    }
                }
            }
            
            Console.WriteLine($"  Base references loaded: {references.Count}");
            
            // Add all currently loaded assemblies (includes our project DLLs)
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location));
            
            int addedCount = 0;
            foreach (var assembly in loadedAssemblies)
            {
                try
                {
                    references.Add(MetadataReference.CreateFromFile(assembly.Location));
                    addedCount++;
                }
                catch
                {
                    // Skip if already added or can't be referenced
                }
            }
            
            Console.WriteLine($"  Added loaded assemblies: {addedCount}");

            // Create the compilation with implicit usings enabled (matches .csproj setting)
            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
            syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions, path: absoluteSourcePath, encoding: System.Text.Encoding.UTF8);
            
            CSharpCompilation compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(outputDllPath),
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug)
                    .WithUsings("System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http", 
                                "System.Threading", "System.Threading.Tasks") // Implicit usings from .NET 6+
            );

            // Emit the compilation to a DLL
            EmitResult result;
            string pdbPath = Path.ChangeExtension(outputDllPath, ".pdb");
            using (var fs = new FileStream(outputDllPath, FileMode.Create, FileAccess.Write))
            using (var pdbStream = new FileStream(pdbPath, FileMode.Create, FileAccess.Write))
            {
                var emitOptions = new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb);
                result = compilation.Emit(fs, pdbStream, options: emitOptions);
            }

            if (result.Success)
            {
                // Add to compiled files list with timestamp
                string dllPath = outputDllPath;
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                // Remove old entries for the same files if they exist
                if (File.Exists(CompiledFilesListPath))
                {
                    var existingLines = File.ReadAllLines(CompiledFilesListPath).ToList();
                    var filteredLines = existingLines.Where(line => 
                        !line.Contains(dllPath) && !line.Contains(pdbPath)).ToList();
                    File.WriteAllLines(CompiledFilesListPath, filteredLines);
                }
                
                // Append to the compiled files list with timestamp
                File.AppendAllText(CompiledFilesListPath, $"[{timestamp}] {dllPath}\n");
                if (File.Exists(pdbPath))
                {
                    File.AppendAllText(CompiledFilesListPath, $"[{timestamp}] {pdbPath}\n");
                }
                
                return dllPath;
            }

            if (!result.Success)
            {
                IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                string errors = string.Join(Environment.NewLine, failures.Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}"));
                throw new InvalidOperationException($"Compilation failed: {errors}");
            }

            return outputDllPath;
        }
    
        // Not compatible with cross platform / net8.0
        // public string CompileDllOld(string sourceFilePath, string outputDirectory)
        // {
        //     if (!File.Exists(sourceFilePath)) { throw new FileNotFoundException($"Source file not found: {sourceFilePath}"); }
        //     if (!Directory.Exists(outputDirectory)) {  Directory.CreateDirectory(outputDirectory);}

        //     string outputDllPath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(sourceFilePath) + ".dll");

        //     using (CSharpCodeProvider codeProvider = new CSharpCodeProvider())
        //     {
        //         CompilerParameters parameters = new CompilerParameters
        //         {
        //             GenerateExecutable = false,
        //             OutputAssembly = outputDllPath,
        //             CompilerOptions = "/optimize"
        //         };

        //         //Example references from SubTest.cs:
        //         // using HSFUniverse;
        //         // using MissionElements;
        //         // using Newtonsoft.Json.Linq;
        //         // using System;
        //         // using System.Collections.Generic;
        //         // using System.Diagnostics.CodeAnalysis;
        //         // using System.Xml;
        //         // using Utilities;

        //         // Add necessary references
        //         parameters.ReferencedAssemblies.Add("System.dll");
        //         parameters.ReferencedAssemblies.Add("System.Collections.Generic");
        //         parameters.ReferencedAssemblies.Add("System.Diagnostics.CodeAnalysis");
        //         parameters.ReferencedAssemblies.Add("System.Newtonsoft.Json.Linq");
                
        //         // Add Project references:
        //         string relBuildDir = "src/HSFSystem/bin/Debug/net8.0"; // Currently hardcoded
        //         string buildDir = Path.Combine(Utilities.DevEnvironment.RepoDirectory,relBuildDir);
        //         string[] dllFiles = Directory.GetFiles(buildDir, "*.dll");
        //         foreach (var dllfp in dllFiles)
        //         {
        //             parameters.ReferencedAssemblies.Add(dllfp);
        //         }
                
        //         // Read the source code
        //         string sourceCode = File.ReadAllText(sourceFilePath);

        //         CompilerResults results = codeProvider.CompileAssemblyFromSource(parameters, sourceCode);

        //         if (results.Errors.HasErrors)
        //         {
        //             string errors = string.Join(Environment.NewLine, results.Errors.Cast<CompilerError>().Select(error => error.ToString()));
        //             throw new InvalidOperationException($"Compilation failed: {errors}");
        //         }

        //         return outputDllPath;
        //     }
        // }

        private Subsystem LoadSubsystemFromDll()
        {
            // Ensure the DLL exists
            if (!File.Exists(dll))
            {
                throw new FileNotFoundException($"DLL file not found: {dll}");
            }
            // Load the assembly
            Assembly assembly = Assembly.LoadFile(dll);

            Type[] types = assembly.GetTypes();
            // Filter the types that inherit from Subsystem
            List<string?> subsystemTypes = types
                .Where(t => typeof(Subsystem).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.FullName) // String name
                //Make sure that the className is that same as it is in the input JOSN:
                .Where(t => t.Contains(className, StringComparison.OrdinalIgnoreCase)) 
                .ToList();
            
            // Error handling
            if (types.Length == 0 || types == null)
            {
                throw new ArgumentException("Type(s) not found in the assembly.", nameof(className));
            }

            // Get the the tpye name from the assembly qualified list contianing the "class"
            Type? type = assembly.GetType(types[0].FullName); // Will there only ever be one?
            
            // Error handling
            if (type == null)
            {
                throw new ArgumentException("Type not found in the assembly.", nameof(className));
            }

            // Ensure the type inherits from Subsystem
            if (!typeof(Subsystem).IsAssignableFrom(type))
            {
                throw new InvalidOperationException("Type does not inherit from Subsystem.");
            }

        //Type[] constructorArgs = [typeof(JObject)];
        ConstructorInfo constructor = type.GetConstructor(constructorArgTypes);
        
            if (constructor == null)
            {
                Console.WriteLine($"[ERROR] Looking for constructor with args: {string.Join(", ", constructorArgTypes.Select(t => t.Name))}");
                Console.WriteLine($"[ERROR] Available constructors in {type.Name}:");
                foreach (var ctor in type.GetConstructors())
                {
                    var paramList = string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"  - {type.Name}({paramList})");
                }
                throw new InvalidOperationException("Matching constructor not found.");
            }



            // Create an instance of the type
            return (Subsystem)constructor.Invoke(constructorArgs);


            // this.LoadedSubsystem = (Subsystem)constructor.Invoke(constructorArgs);
            // return this.LoadedSubsystem; // This may cause infinite referencing issues...


            // Subsystem subsystem = (Subsystem)constructor.Invoke(constructorArgs);
            
            // //Set all properties
            // subsystem.ParentScriptedSubsystem = this;
            // subsystem.Name  = this.Name;
            // subsystem.Asset = this.Asset;
            // subsystem.Type  = this.Type; 

            // // This may cause infinite referencing issues...
            // this.LoadedSubsystem = subsystem; 
            
            // // Return the loaded subsystem:
            // return subsystem; 
            
        }
    }
}

