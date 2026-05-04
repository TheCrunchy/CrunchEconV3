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
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
            if (File.Exists($"{Core.path}/CrunchEconV3.dll"))
            {
                File.Delete($"{Core.path}/CrunchEconV3.dll");
            }
            List<MetadataReference> metadataReferenceList = new List<MetadataReference>();
            foreach (Assembly assembly in ((IEnumerable<Assembly>)AppDomain.CurrentDomain.GetAssemblies()).Where<Assembly>((Func<Assembly, bool>)(a => !a.IsDynamic)))
            {
                if (!assembly.IsDynamic && assembly.Location != null & string.Empty != assembly.Location)
                    metadataReferenceList.Add((MetadataReference)MetadataReference.CreateFromFile(assembly.Location));
            }

           var pluginManager = Core.Session.Managers.GetManager<PluginManager>();
           var folder = pluginManager.PluginDir;

            var plugins = new List<String> { $"{folder}/CrunchEconV3.zip", $"{folder}/ad7fcfad-0ce0-4e1c-867d-4fe6edf533de.zip" , $"{folder}/CrunchGroupPlugin.zip" };
            foreach (var plugin in plugins)
            {
                try
                {
                    using (var zipArchive = ZipFile.OpenRead(plugin))
                    {
                        foreach (var entry in zipArchive.Entries)
                        {
                            if (entry.Name.EndsWith(".dll", StringComparison.CurrentCultureIgnoreCase) && entry.Name.Contains("Crunch"))
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

            foreach (var filePath in Directory.GetFiles(Core.path).Where(x => x.Contains(".dll") && !x.Contains("CrunchEconV3.dll")))
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
                    Core.Log.Error($"Compiling {filePath}");
                    using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader streamReader = new StreamReader(fileStream))
                    {
                        string text = streamReader.ReadToEnd();
                        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(text, path: filePath);
                        trees.Add(syntaxTree);
                    }
                }
            }
            catch (Exception e)
            {
                Core.Log.Error($"Compiler file error {e}");
            }

            var compilation = CSharpCompilation.Create("MyAssembly")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(GetRequiredRefernces())
                .AddSyntaxTrees(trees);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                var result = compilation.Emit(memoryStream);

                if (result.Success)
                {
                    var assembly = Assembly.Load(memoryStream.ToArray());
                    Core.Log.Error("Compilation successful!");
                    Core.myAssemblies.Add(assembly);

                    ExecuteAssembly(assembly, patches, commands);

                    Core.CompileFailed = false;
                    return true;
                }
                else
                {
                    var needsFix = result.Diagnostics.Any(d =>
                        d.Severity == DiagnosticSeverity.Error &&
                        d.GetMessage().Contains("does not implement interface member") &&
                        d.GetMessage().Contains("MyFactionStation"));

                    if (needsFix)
                    {
                        var rewriter = new FactionStationRewriter();
                        var newTrees = new List<SyntaxTree>();

                        var errorFiles = result.Diagnostics
                            .Where(d => d.Location.IsInSource)
                            .Select(d => d.Location.SourceTree.FilePath)
                            .ToHashSet();

                        foreach (var tree in trees)
                        {
                            if (!errorFiles.Contains(tree.FilePath))
                            {
                                newTrees.Add(tree);
                                continue;
                            }

                            var root = tree.GetCompilationUnitRoot();
                            var newRoot = (CompilationUnitSyntax)rewriter.Visit(root);
                            newRoot = EnsureUsing(newRoot);

                            newTrees.Add(CSharpSyntaxTree.Create(newRoot, path: tree.FilePath));
                        }

                        // Rebuild compilation
                        var retryCompilation = CSharpCompilation.Create("MyAssembly")
                            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                            .AddReferences(GetRequiredRefernces())
                            .AddSyntaxTrees(newTrees);

                        using (var retryStream = new MemoryStream())
                        {
                            var retryResult = retryCompilation.Emit(retryStream);

                            if (retryResult.Success)
                            {
                                var assembly = Assembly.Load(retryStream.ToArray());
                                Core.Log.Error("Recompilation successful after fix!");

                                Core.myAssemblies.Add(assembly);

                                ExecuteAssembly(assembly, patches, commands);

                                Core.CompileFailed = false;
                                return true;
                            }
                            else
                            {
                                Core.Log.Error("Recompilation still failed.");

                                LogDiagnostics(retryResult.Diagnostics);
                                Core.CompileFailed = true;
                                return true;
                            }
                        }
                    }

                    // No fix applied, log original errors
                    LogDiagnostics(result.Diagnostics);
                    Core.CompileFailed = true;
                    return true;
                }
            }
        }

        private static void ExecuteAssembly(Assembly assembly, PatchManager patches, CommandManager commands)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    MethodInfo method = type.GetMethod("Patch", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    if (method == null)
                        continue;

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

        private static void LogDiagnostics(IEnumerable<Diagnostic> diagnostics)
        {
            Console.WriteLine("Compilation failed:");

            foreach (var diagnostic in diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                var location = diagnostic.Location;
                string filePath = "unknown file";

                if (location.IsInSource)
                {
                    var syntaxTree = location.SourceTree;
                    filePath = syntaxTree?.FilePath ?? "unknown file";
                }

                var lineSpan = location.GetLineSpan().StartLinePosition;
                int line = lineSpan.Line + 1;
                int character = lineSpan.Character + 1;

                Core.Log.Error($"{filePath}({line},{character}): {diagnostic.Id}: {diagnostic.GetMessage()}");
            }
        }


        private static CompilationUnitSyntax EnsureUsing(CompilationUnitSyntax root)
        {
            if (!root.Usings.Any(u => u.Name.ToString() == "VRage.Game.ModAPI"))
            {
                root = root.AddUsings(
                    SyntaxFactory.UsingDirective(
                        SyntaxFactory.ParseName("VRage.Game.ModAPI")));
            }

            return root;
        }
    }
}
