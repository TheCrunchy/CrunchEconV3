using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CSharp;
using NLog;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace CrunchEconV3.Commands
{
    [Category("newcontract")]
    public class CategoryCommands : CommandModule
    {
        private static List<Assembly> myAssemblies { get; set; } = new List<Assembly>();
        [Command("reload", "example command usage !categorycommands example")]
        [Permission(MyPromoteLevel.Admin)]
        public void Example()
        {
            StationHandler.BlocksContracts.Clear();
            StationHandler.ReadyForRefresh();
            StationHandler.MappedStations.Clear();
            Core.ReloadConfig();
            Core.StationStorage.LoadAll();
     

            Context.Respond("Reloaded and cleared existing contracts");
        }

        [Command("test", "example command usage !categorycommands example")]
        [Permission(MyPromoteLevel.Admin)]
        public void OutputShit()
        {
          //  FileInfo sourceFile = new FileInfo();
            string outputName = string.Format($"{Core.path}/Example.dll");

            bool success = Compile( new CompilerParameters()
            {
                GenerateExecutable = false, // compile as library (dll)
                OutputAssembly = outputName,
                GenerateInMemory = false, // as a physical file
            });

            //  var assem = Assembly.LoadFrom(outputName);
            //     assem.GetReferencedAssemblies();

            if (!success)
            {
                Context.Respond("Didnt work");
            }
            var q = from t in Assembly.GetExecutingAssembly().GetTypes()
                where t.IsClass && t.Namespace == "CrunchEconV3.Models.Contracts" && t.GetInterfaces().Contains(typeof(ICrunchContract))
                    select t;

            foreach (var c in q)
            {
                Context.Respond(c.FullName);
            }

            try
            {
                IEnumerable<Type> _commands = myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                    .Where(t => t.GetInterfaces().Contains(typeof(ICrunchContract)));

                foreach (Type _type in _commands)
                {
                    Context.Respond(_type.FullName);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    // Log or inspect the loaderException to get specific details about the loading issue
                    Core.Log.Error(loaderException.Message);
                }
            }
            Context.Respond("done outputting");
        }
        public static MetadataReference[] GetRequiredRefernces()
        {
            List<MetadataReference> metadataReferenceList = new List<MetadataReference>();
            foreach (Assembly assembly in ((IEnumerable<Assembly>)AppDomain.CurrentDomain.GetAssemblies()).Where<Assembly>((Func<Assembly, bool>)(a => !a.IsDynamic)))
            {
                if (!assembly.IsDynamic && assembly.Location != null & string.Empty != assembly.Location)
                    metadataReferenceList.Add((MetadataReference)MetadataReference.CreateFromFile(assembly.Location));
            }

            metadataReferenceList.Add(MetadataReference.CreateFromFile(@$"C:\Users\Cameron\Documents\4 Torch Server\Instance\CrunchEconV3\CrunchEconV3.dll"));

            return metadataReferenceList.ToArray();
        }
        private static bool Compile(CompilerParameters options)
        {
            var text = File.ReadAllText($"{Core.path}/test.cs");
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(text);

            var compilation = CSharpCompilation.Create("MyAssembly")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(GetRequiredRefernces()) // Add necessary references
                .AddSyntaxTrees(syntaxTree);
            
            using (MemoryStream memoryStream = new MemoryStream())
            {
                var result = compilation.Emit(memoryStream);

                if (result.Success)
                {
                    Assembly assembly = Assembly.Load(memoryStream.ToArray());
                    Core.Log.Error("Compilation successful!");
                    myAssemblies.Add(assembly);
                    // Use the compiled assembly as needed
                }
                else
                {
                    Console.WriteLine("Compilation failed:");
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        Core.Log.Error(diagnostic);
                    }
                }
            }
            //CodeDomProvider provider = new CSharpCodeProvider();
            //foreach (Assembly @assembly in AppDomain.CurrentDomain.GetAssemblies())
            //{
            //    if (!assembly.IsDynamic && !assembly.GlobalAssemblyCache)
            //    {
            //        csu0.ReferencedAssemblies.Add($"{assembly.Location}");
            //        Core.Log.Info($"{assembly.Location}");
            //    }

            //}
#pragma warning disable CS0012
            //  csu0.ReferencedAssemblies.Add(@$"C:\Users\Cameron\Documents\4 Torch Server\Instance\CrunchEconV3\CrunchEconV3.dll");
            ////  csu0.ReferencedAssemblies.Add("System.Runtime, Version=4.1.2.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            //  //  csu0.ReferencedAssemblies.Add(@$"C:\Users\Cameron\Documents\4 Torch Server\System.Runtime.dll");
            //  CompilerResults results = provider.CompileAssemblyFromDom(new CompilerParameters() { GenerateInMemory = true }, csu0);


            //CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp 9");

            //CompilerResults results = provider.CompileAssemblyFromFile(options, sourceFile.FullName);

            //if (results.Errors.Count > 0)
            //{

            //    foreach (CompilerError error in results.Errors)
            //    {
            //        Core.Log.Error($"{error.ToString()}");
            //    }
            //    return false;
            //}
            //else
            //{
            //    return true;
            //}

            return true;
        }



        public static CodeCompileUnit BuildHelloWorldGraph()
        {
            
            CodeCompileUnit compileUnit = new CodeCompileUnit();
            
            CodeNamespace samples = new CodeNamespace("CrunchEconV3.Models.Contracts");
   
            compileUnit.Namespaces.Add(samples);

            var text = File.ReadAllText($"{Core.path}/test.cs");
            CodeSnippetExpression literalExpression =
                new CodeSnippetExpression(text);

            CodeEntryPointMethod start = new CodeEntryPointMethod();

            start.Statements.Add(literalExpression);

            return compileUnit;
        }

        public static bool CompileCSharpCode(string sourceFile, string exeFile)
        {
            CSharpCodeProvider provider = new CSharpCodeProvider();

            // Build the parameters for source compilation.
            CompilerParameters cp = new CompilerParameters();
  
            // Add an assembly reference.
            foreach (Assembly @assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !assembly.GlobalAssemblyCache)
                {
                    cp.ReferencedAssemblies.Add($"{assembly.Location}");
                }

                Core.Log.Info($"{assembly.FullName}");
            }
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            cp.ReferencedAssemblies.Add(currentAssembly.Location);
            // Generate an executable instead of
            // a class library.
            cp.GenerateExecutable = false;
       
            // Set the assembly file name to generate.
            cp.OutputAssembly = exeFile;

            // Save the assembly as a physical file.
            cp.GenerateInMemory = true;

            // Invoke compilation.
            CompilerResults cr = provider.CompileAssemblyFromFile(cp, sourceFile);

            if (cr.Errors.Count > 0)
            {
                // Display compilation errors.
                Core.Log.Error("Errors building {0} into {1}",
                    sourceFile, cr.PathToAssembly);
                foreach (CompilerError ce in cr.Errors)
                {
                    Core.Log.Error(ce.ToString());
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("Source {0} built into {1} successfully.",
                    sourceFile, cr.PathToAssembly);
            }

            // Return the results of compilation.
            if (cr.Errors.Count > 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
