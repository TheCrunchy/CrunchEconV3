using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Managers;
using Torch.Managers.PatchManager;
using Torch.Utils;

namespace CrunchEconV3.Utils
{
    public static class Compiler
    {
        public static bool Compile(string folder)
        {
            bool success = CompileFromFile(folder);

            return success;
        }

        public static MetadataReference[] GetRequiredRefernces()
        {
            List<MetadataReference> metadataReferenceList = new List<MetadataReference>();
            foreach (Assembly assembly in ((IEnumerable<Assembly>)AppDomain.CurrentDomain.GetAssemblies()).Where<Assembly>((Func<Assembly, bool>)(a => !a.IsDynamic)))
            {
                if (!assembly.IsDynamic && assembly.Location != null & string.Empty != assembly.Location)
                    metadataReferenceList.Add((MetadataReference)MetadataReference.CreateFromFile(assembly.Location));
            }

           var pluginManager = Core.Session.Managers.GetManager<PluginManager>();
           var folder = pluginManager.PluginDir;

            var plugins = new List<String> { $"{folder}/CrunchEconV3.zip", $"{folder}/ad7fcfad-0ce0-4e1c-867d-4fe6edf533de.zip" };
            foreach (var plugin in plugins)
            {
                try
                {
                    using (var zipArchive = ZipFile.OpenRead(plugin))
                    {
                        foreach (var entry in zipArchive.Entries)
                        {
                            if (entry.Name.EndsWith(".dll", StringComparison.CurrentCultureIgnoreCase))
                            {
                                using (var stream = entry.Open())
                                {
                                    byte[] end = MiscExtensions.ReadToEnd(stream, (int)entry.Length);
                                    metadataReferenceList.Add((MetadataReference)MetadataReference.CreateFromImage(end));
                                }

                            }
                        }
                    }
                }
                catch (Exception e)
                {
                }
            }
     

            //foreach (var filePath in Directory.GetFiles($"{Core.basePath}/{Core.PluginName}/").Where(x => x.Contains(".dll")))
            //{
            //    using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            //    {
            //        metadataReferenceList.Add(MetadataReference.CreateFromStream(fileStream));
            //    }
            //}

            foreach (var filePath in Directory.GetFiles(Core.path).Where(x => x.Contains(".dll")))
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    metadataReferenceList.Add(MetadataReference.CreateFromStream(fileStream));
                }
            }

            return metadataReferenceList.ToArray();
        }
        private static bool CompileFromFile(string folder)
        {
            var patches = Core.Session.Managers.GetManager<PatchManager>();
            var commands = Core.Session.Managers.GetManager<CommandManager>();
            List<SyntaxTree> trees = new List<SyntaxTree>();

            try
            {
                foreach (var filePath in Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(x => x.EndsWith(".cs")))
                {
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (StreamReader streamReader = new StreamReader(fileStream))
                        {
                            string text = streamReader.ReadToEnd();
                            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(text);
                            trees.Add(syntaxTree);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                Core.Log.Error($"Compiler file error {e}");
            }
            var compilation = CSharpCompilation.Create("MyAssembly")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(GetRequiredRefernces()) // Add necessary references
                .AddSyntaxTrees(trees);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                var result = compilation.Emit(memoryStream);

                if (result.Success)
                {
                    Assembly assembly = Assembly.Load(memoryStream.ToArray());
                    Core.Log.Error("Compilation successful!");
                    Core.myAssemblies.Add(assembly);

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            MethodInfo method = type.GetMethod("Patch", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                            if (method == null)
                            {
                                continue;
                            }
                            ParameterInfo[] ps = method.GetParameters();
                            if (ps.Length != 1 || ps[0].IsOut || ps[0].IsOptional || ps[0].ParameterType.IsByRef ||
                                ps[0].ParameterType != typeof(PatchContext) || method.ReturnType != typeof(void))
                            {
                                continue;
                            }
                            var context = patches.AcquireContext();
                            method.Invoke(null, new object[] { context });
                        }
                        patches.Commit();
                        foreach (var obj in assembly.GetTypes())
                        {
                            commands.RegisterCommandModule(obj);
                        }
                    }
                    catch (Exception e)
                    {
                        Core.Log.Error($"{e}");
                    }
                }
                else
                {
                    Console.WriteLine("Compilation failed:");
                    Core.CompileFailed = true;
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        Core.Log.Error(diagnostic);
                    }

                    return true;
                }
            }
            Core.CompileFailed = false;
            return true;
        }
    }
}
