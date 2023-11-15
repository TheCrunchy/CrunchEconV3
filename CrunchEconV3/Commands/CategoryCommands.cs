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


            //  var assem = Assembly.LoadFrom(outputName);
            //     assem.GetReferencedAssemblies();

            Context.Respond("done outputting");
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
