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
        private readonly Type[] constructorArgTypes = [typeof(JObject)]; 
        private readonly object[] constructorArgs = [];

        private static readonly string CompiledFilesListPath = Path.Combine(Utilities.DevEnvironment.RepoDirectory, "user-compiled-subsystems.txt");

        public ScriptedSubsystemCS(JObject scriptedSubsystemJson, Asset asset)//string dllPath, string typeName, string subsystemJson)
        {
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
  
                this.dll = CompileDll(this.src,Path.Combine(Directory.GetParent(src).FullName,"bin"));

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
                this.constructorArgs = [scriptedSubsystemJson]; // Use the JObject json by default.  Using default JObject tpye..."); 
            }
            else
            {
                this.constructorArgs = [scriptedSubsystemJson]; // Use the JObject json by default. 
                Console.WriteLine($"Loading subsytem of type {this.Type}, using deafault {this.constructorArgTypes}...");
                
                //Optional so do not throw an error (assume that there is a JObject constructor); Error caught later...
            }

            // Now load the subsytem from the file and other (optional) arguments
            this.LoadedSubsystem = LoadSubsystemFromDll();
            
            // Set all necessary subsystem attributes...
            LoadedSubsystem.Asset = asset; 
            LoadedSubsystem.Type = Type;

            // Load in the (optional) subsystem name:
            if (scriptedSubsystemJson.TryGetValue("name", stringCompare, out JToken nameJason))
            {
                LoadedSubsystem.Name = nameJason.ToString();
            }
            else
            {
                // Fish fore the name of the subsystem using the filename (by default) 
                string fp = ""; 
                if (!dll.Equals("")) { fp = dll; }
                else { fp = src; }

                // Assign the subsystem the name of the file by default. 
                LoadedSubsystem.Name = Path.GetFileName(fp);
            }
            
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

            // Define references
            List<MetadataReference> references = new List<MetadataReference>
            {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Dictionary<,>).Assembly.Location), // Added reference for Dictionary
                    MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Collections.dll")), // Added System.Collections.dll
                    MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "System.Runtime.dll")), // Added System.Runtime.dll
                    MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "netstandard.dll")) // Added netstandard.dll
            };

            // Add necessary references
            references.Add(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.List<>).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Newtonsoft.Json.Linq.JObject).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Xml.XmlDocument).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly.Location));
            // Add project references
            string relBuildDir = "src/HSFSystem/bin/Debug/net8.0"; // Currently hardcoded
            string buildDir = Path.Combine(Utilities.DevEnvironment.RepoDirectory, relBuildDir);
            string[] dllFiles = Directory.GetFiles(buildDir, "*.dll");
            foreach (var dllfp in dllFiles)
            {
                references.Add(MetadataReference.CreateFromFile(dllfp));
            }

            // Create the compilation
            CSharpCompilation compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(outputDllPath),
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Debug));

            // Emit the compilation to a DLL
            EmitResult result;
            using (var fs = new FileStream(outputDllPath, FileMode.Create, FileAccess.Write))
            {
                result = compilation.Emit(fs);
            }

            if (result.Success)
            {
                // Add to compiled files list with timestamp
                string dllPath = outputDllPath;
                string pdbPath = Path.ChangeExtension(outputDllPath, ".pdb");
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
            Assembly assembly = Assembly.LoadFrom(dll);

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
        // ConstructorInfo constructor = type.GetConstructors()
        //                                   .FirstOrDefault(ctor =>
        //                                   {
        //                                       var parameters = ctor.GetParameters();
        //                                       return parameters.Length == (constructorArgs?.Length ?? 0) &&
        //                                              parameters.Zip(constructorArgs, (p, a) => p.ParameterType.IsAssignableFrom(a.GetType())).All(b => b);
        //                                   });
            // Find the appropriate constructor
            // ConstructorInfo constructor = type.GetConstructors()
            //                                   .FirstOrDefault(ctor =>
            //                                   {
            //                                       var parameters = ctor.GetParameters();
            //                                       return parameters.Length == constructorArgs.Length &&
            //                                              parameters.Select(p => p.ParameterType).SequenceEqual(constructorArgs.Select(a => a.GetType()));
            //                                   });

            if (constructor == null)
            {
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

