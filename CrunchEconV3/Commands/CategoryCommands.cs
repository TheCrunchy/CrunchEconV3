using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Concurrent;
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
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CSharp;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRageMath;

namespace CrunchEconV3.Commands
{
    [Category("crunchecon")]
    public class CategoryCommands : CommandModule
    {
        [Command("toggle", "toggle paused state")]
        [Permission(MyPromoteLevel.Admin)]
        public void toggle()
        {
            Core.Paused = !Core.Paused;
            Context.Respond($"Toggled pause to {Core.Paused}");
        }

        [Command("reload", "example command usage !categorycommands example")]
        [Permission(MyPromoteLevel.Admin)]
        public void Reload()
        {
            StationHandler.BlocksContracts.Clear();
            StationHandler.ReadyForRefresh();
            StationHandler.MappedStations.Clear();
            Core.ReloadConfig();
            Core.StationStorage.LoadAll();

            foreach (var station in Core.StationStorage.GetAll())
            {
                station.SetFirstLoad(true);
            }

            Context.Respond("Reloaded and cleared existing contracts");
            Context.Respond("If changing scripts, use the compile command to apply changes");
        }

        [Command("createstation", "create a station")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddStation(string stationName, string factionOwnerTag)
        {
            var station = new StationConfig();
            station.FileName = stationName + ".json";
            station.LocationGPS = GPSHelper.CreateGps(Context.Player.GetPosition(), Color.Orange, "Station", "").ToString();
            station.Enabled = true;
            station.FactionTag = factionOwnerTag;
            station.Logics = new List<IStationLogic>();
            station.ContractFiles = new List<string>();

            Core.StationStorage.Save(station);
            Context.Respond("Station Saved, !crunchecon reload to load it");
        }

        [Command("addlogic", "addlogic to a station")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddLogic(string stationName, string logicType)
        {
            if (!stationName.EndsWith(".json"))
            {
                stationName += ".json";
            }
            var station = Core.StationStorage.GetAll().FirstOrDefault(x => x.FileName == stationName);
            if (station == null)
            {
                Context.Respond("Station not found");
                return;
            }
            var configs2 = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                           where t.IsClass && t.GetInterfaces().Contains(typeof(IStationLogic))
                           select t;

            if (configs2.Any(x => x.Name == logicType))
            {
                var logic = configs2.FirstOrDefault(x => x.Name == logicType);
                IStationLogic instance = (IStationLogic)Activator.CreateInstance(logic);
                instance.Setup();
                station.Logics.Add(instance);
                Core.StationStorage.Save(station);
                Context.Respond("Logic added to station, load it with !crunchecon reload");
            }
            else
            {
                Context.Respond("Logic type not found, see all available logics with !crunchecon list");
            }
        }
        [Command("addcontract", "addlogic to a station")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddContract(string contractFileName, string contractType)
        {
            foreach (var file in Directory.GetDirectories($"{Core.path}/Stations/"))
            {
                foreach (var actualFile in Directory.GetFiles(file))
                {
                    if (Path.GetFileNameWithoutExtension(actualFile) != contractFileName)
                    {
                        continue;
                    }
                    var configs2 = from t in Core.myAssemblies.Select(x => x)
                            .SelectMany(x => x.GetTypes())
                        where t.IsClass && t.GetInterfaces().Contains(typeof(IContractConfig))
                        select t;
                    var utils = new FileUtils();
                    var contracts =
                        utils.ReadFromJsonFile<List<IContractConfig>>(actualFile);

                    if (configs2.Any(x => x.Name == contractType))
                    {
                        var logic = configs2.FirstOrDefault(x => x.Name == contractType);
                        IContractConfig instance = (IContractConfig)Activator.CreateInstance(logic);
                        instance.Setup();
                        contracts.Add(instance);
                        utils.WriteToJsonFile(actualFile, contracts);
                        Context.Respond("Contract added to file, load it with !crunchecon reload");
                        return;
                    }
                    else
                    {
                        Context.Respond("Contract type not found, see all available logics with !crunchecon list");
                    }
                }
 
            }

            Context.Respond("Contract file not found");
            return;

        }

        [Command("list", "list all valid names from scripts")]
        [Permission(MyPromoteLevel.Admin)]
        public void List()
        {
            var configs = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                          where t.IsClass && t.GetInterfaces().Contains(typeof(IContractConfig))
                          select t;

            var configs2 = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                           where t.IsClass && t.GetInterfaces().Contains(typeof(IStationLogic))
                           select t;

            foreach (var config in configs)
            {
                Context.Respond($"CONTRACT CONFIG {config.Name}");
            }

            foreach (var config in configs2)
            {
                Context.Respond($"STATION LOGIC {config.Name}");
            }

            foreach (var file in Directory.GetDirectories($"{Core.path}/Stations/"))
            {
                foreach (var file2 in Directory.GetFiles(file))
                {
                    Context.Respond($"CONTRACT FILE {Path.GetFileNameWithoutExtension(file2)}");
                }
            }
            foreach (var file in Directory.GetFiles($"{Core.path}/Stations/"))
            {
                Context.Respond($"STATION FILE {Path.GetFileNameWithoutExtension(file)}");
            }
        }

        [Command("compile", "compile the .cs files")]
        [Permission(MyPromoteLevel.Admin)]
        public void Compile()
        {
            var patches = Core.Session.Managers.GetManager<PatchManager>();
            try
            {
                var typesWithPatchShimAttribute = Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                    .Where(type => type.IsClass && type.GetCustomAttributes(typeof(PatchShimAttribute), true).Length > 0);

                foreach (var type in typesWithPatchShimAttribute)
                {
                    MethodInfo method = type.GetMethod("UnPatch", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    if (method == null)
                    {
                        Core.Log.Error($"Patch shim type {type.FullName} doesn't have a static Patch method.");
                        continue;
                    }
                    ParameterInfo[] ps = method.GetParameters();
                    if (ps.Length != 1 || ps[0].IsOut || ps[0].IsOptional || ps[0].ParameterType.IsByRef ||
                        ps[0].ParameterType != typeof(PatchContext) || method.ReturnType != typeof(void))
                    {
                        Core.Log.Error($"Patch shim type {type.FullName} doesn't have a method with signature `void UnPatch(PatchContext)`");
                        continue;
                    }

                    var context = patches.AcquireContext();
                    method.Invoke(null, new object[] { context });
                    patches.Commit();
                }
 
                Context.Respond("PATCH DONE, restart server if old patch still works");
            }
            catch (Exception e)
            {
                Core.Log.Error($"patch compile error {e}");
                throw;
            }

            Core.myAssemblies.Clear();
            foreach (var item in Directory.GetFiles($"{Core.path}/Scripts/", "*", SearchOption.AllDirectories).Where(x => x.EndsWith(".cs")))
            {
                Compiler.Compile(item);
            }

            var configs2 = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                           where t.IsClass && t.GetInterfaces().Contains(typeof(IStationLogic))
                           select t;

            foreach (var config in configs2)
            {
                IStationLogic instance = (IStationLogic)Activator.CreateInstance(config);
                instance.Setup();
            }

            try
            {
                var typesWithPatchShimAttribute = Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                    .Where(type => type.IsClass && type.GetCustomAttributes(typeof(PatchShimAttribute), true).Length > 0);

                patches.AcquireContext();

                foreach (var type in typesWithPatchShimAttribute)
                {
                    MethodInfo method = type.GetMethod("Patch", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    if (method == null)
                    {
                        Core.Log.Error($"Patch shim type {type.FullName} doesn't have a static Patch method.");
                        continue;
                    }
                    ParameterInfo[] ps = method.GetParameters();
                    if (ps.Length != 1 || ps[0].IsOut || ps[0].IsOptional || ps[0].ParameterType.IsByRef ||
                        ps[0].ParameterType != typeof(PatchContext) || method.ReturnType != typeof(void))
                    {
                        Core.Log.Error($"Patch shim type {type.FullName} doesn't have a method with signature `void Patch(PatchContext)`");
                        continue;
                    }

                    var context = patches.AcquireContext();
                    method.Invoke(null, new object[] { context });
                }

                patches.Commit();
                Context.Respond("PATCH DONE, restart server if old patch still works");
            }

            catch (Exception e)
            {
                Core.Log.Error($"patch compile error {e}");
            }
            var commands = Core.Session.Managers.GetManager<CommandManager>();
            foreach (var item in Core.myAssemblies)
            {
                foreach (var obj in item.GetTypes())
                {
                    commands.RegisterCommandModule(obj);
                }

            }
            this.Reload();
            Context.Respond("done, check logs for any errors");
        }



        [Command("exportgrid", "export a grid to a sellable file")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportGrid()
        {

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> gridWithSubGrids = GridFinder.FindLookAtGridGroup(Context.Player.Character);
            List<MyCubeGrid> grids = new List<MyCubeGrid>();
            foreach (var item in gridWithSubGrids)
            {
                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in item.Nodes)
                {
                    MyCubeGrid grid = groupNodes.NodeData;

                    foreach (MyProjectorBase proj in grid.GetFatBlocks().OfType<MyProjectorBase>())
                    {
                        proj.Clipboard.Clear();
                    }
                    grids.Add(grid);
                    foreach (var block in grid.GetFatBlocks().OfType<MyCockpit>())
                    {
                        if (block.Pilot != null)
                        {
                            block.RemovePilot();
                        }
                    }

                }

            }
            if (grids.Count == 0)
            {
                Context.Respond("Could not find grid.");
                return;
            }

            string gridname = grids.First().GetBiggestGridInGroup().DisplayName;
            GridManager.SaveGridNoDelete($"{Core.path}\\Grids\\{gridname}.sbc", $"{gridname}.sbc", false, false, grids);
            Context.Respond($"Exported grid: {gridname}");
        }

    }
}
