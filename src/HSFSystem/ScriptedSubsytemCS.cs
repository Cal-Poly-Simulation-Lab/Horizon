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
//using System.CodeDom.Compiler;
using Microsoft.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

// namespace HSFSystem
// {
//     public class ScriptedSubsytemCS : Subsystem
//     {
//         public Subsystem LoadedSubsystem { get; private set; }
//         public ScriptedSubsystemCS(JObject scriptedSubsystemJson, Asset asset)
//         {
            
//         }

//     }
// }

namespace HSFSystem
{ 
    public class ScriptedSubsystemCS : Subsystem
    {
        public Subsystem LoadedSubsystem { get; private set; }
        private readonly string src = "";
        private readonly string dll = ""; 
        private readonly string className = ""; 
        private readonly string dlldir = ""; 

        public ScriptedSubsystemCS(JObject scriptedSubsystemJson, Asset asset)//string dllPath, string typeName, string subsystemJson)
        {
           StringComparison stringCompare = StringComparison.CurrentCultureIgnoreCase;

            this.Asset = asset;
            //if(scriptedSubsystemJson.TryGetValue("name", stringCompare, out JToken nameJason))
            //    this.Name = this.Asset.Name.ToLower() + "." + nameJason.ToString().ToLower();
            //else
            //{
            //    Console.WriteLine($"Error loading subsytem of type {this.Type}, missing Name attribute");
            //    throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing Name attribute\"");
            //}

            if (scriptedSubsystemJson.TryGetValue("dll",stringCompare, out JToken dllJason))
            {
                this.dll = dllJason.ToString();
                if (!File.Exists(dll)) // Make it a relative (repo) path if the file doesn't exist given by src
                { this.dll = Path.Combine(Utilities.DevEnvironment.RepoDirectory, dll.Replace('\\','/')); }

            }
            else if (scriptedSubsystemJson.TryGetValue("src", stringCompare, out JToken srcJason))
            {
                this.src = srcJason.ToString();
                if (!File.Exists(src)) // Make it a relative (repo) path if the file doesn't exist given by src
                { this.src = Path.Combine(Utilities.DevEnvironment.RepoDirectory, src.Replace('\\', '/')); } //Replace backslashes with forward slashes, if applicable 
  
                this.dll = CompileDll(this.src,Path.Combine(Directory.GetParent(src).FullName,"bin"));

            }

            // else if (scriptedSubsystemJson.TryGetValue("fullpath", stringCompare, out JToken fullpathJason))
            // {
            // }
            else
            {
                Console.WriteLine($"Error loading subsytem of type {this.Type}, missing Src/dll attribute");
                throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing Src attribute");
            }

            if (scriptedSubsystemJson.TryGetValue("className", stringCompare, out JToken classNameJason))
                this.className = classNameJason.ToString();
            else
            {
                Console.WriteLine($"Error loading subsytem of type {this.Type}, missing ClassName attribute");
                throw new ArgumentException($"Error loading subsytem of type {this.Type}, missing ClassName attribute");
            }
            
            LoadedSubsystem = LoadSubsystemFromDll(dll, className, scriptedSubsystemJson);

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

            // Create a syntax tree from the source code
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

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
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Emit the compilation to a DLL
            EmitResult result;
            using (var fs = new FileStream(outputDllPath, FileMode.Create, FileAccess.Write))
            {
                result = compilation.Emit(fs);
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

        private Subsystem LoadSubsystemFromDll(string dllPath, params object[] constructorArgs)
        {
            // Ensure the DLL exists
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException($"DLL file not found: {dllPath}");
            }
            // Load the assembly
            Assembly assembly = Assembly.LoadFrom(dllPath);

            Type[] types = assembly.GetTypes();
            // Filter the types that inherit from Subsystem
            List<string> subsystemTypes = types
                .Where(t => typeof(Subsystem).IsAssignableFrom(t) && !t.IsAbstract)
                .Select(t => t.FullName) // String name
                //Make sure that the className is that same as it is in the input JOSN:
                .Where(t => t.Contains(className, StringComparison.OrdinalIgnoreCase)) 
                .ToList();
            // Get the the tpye name from the assembly qualified list contianing the "class"
            Type type = assembly.GetType(types[0].FullName);

            if (type == null)
            {
                throw new ArgumentException("Type not found in the assembly.", nameof(className));
            }

            // Ensure the type inherits from Subsystem
            if (!typeof(Subsystem).IsAssignableFrom(type))
            {
                throw new InvalidOperationException("Type does not inherit from Subsystem.");
            }

        ConstructorInfo constructor = type.GetConstructors()
                                          .FirstOrDefault(ctor =>
                                          {
                                              var parameters = ctor.GetParameters();
                                              return parameters.Length == (constructorArgs?.Length ?? 0) &&
                                                     parameters.Zip(constructorArgs, (p, a) => p.ParameterType.IsAssignableFrom(a.GetType())).All(b => b);
                                          });
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
        }


        private bool ContainsSubsystemType(Assembly assembly)
        {
            return assembly.GetTypes().Any(type => typeof(Subsystem).IsAssignableFrom(type));
        }


        // REGULAR SUBSYTEM METHODS :


        // Override any methods or properties as necessary to delegate to LoadedSubsystem
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            return LoadedSubsystem.CanPerform(proposedEvent, environment);
        }

        // Add more overrides as needed...
    }
}
