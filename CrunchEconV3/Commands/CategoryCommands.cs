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
using CrunchEconV3.PlugAndPlay;
using CrunchEconV3.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CSharp;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Groups;
using VRage.Library.Collections;
using VRage.ObjectBuilders;
using VRage.Utils;
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
            if (Core.CompileFailed)
            {
                Context.Respond("Compile failed, files not reloaded.");
                return;
            }

            KeenStoreManagement.LoadStores();
            Core.GenerateDefaults();
            KeenStoreManagement.Update();
            Core.StationStorage = new JsonStationStorageHandler(Core.path);
            Core.PlayerStorage = new JsonPlayerStorageHandler(Core.path);
            Core.Session.Managers.GetManager<IMultiplayerManagerBase>().PlayerJoined -= Core.PlayerStorage.LoadLogin;
            Core.Session.Managers.GetManager<IMultiplayerManagerBase>().PlayerJoined += Core.PlayerStorage.LoadLogin;
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
            var station = Core.StationStorage.GetAll().FirstOrDefault(x => x.FileName.ToLower() == stationName.ToLower());
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
            Core.myAssemblies.Clear();
            Compiler.Compile($"{Core.path}/Scripts/");

            var configs2 = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                           where t.IsClass && t.GetInterfaces().Contains(typeof(IStationLogic))
                           select t;

            foreach (var config in configs2)
            {
                IStationLogic instance = (IStationLogic)Activator.CreateInstance(config);
                instance.Setup();
            }

            this.Reload();
            Context.Respond("done, check logs for any errors");
        }

        [Command("importgrid", "import a grid from a sellable file")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportGrid(string fileName)
        {
            var path = $"{Core.path}\\Grids\\{fileName.Replace(".sbc", "")}.sbc";
            if (!File.Exists(path))
            {
                Context.Respond("Grid file not found with that name.");
                return;
            }
            GridManager.LoadGrid(path, Context.Player.Character.PositionComp.GetPosition(), false, Context.Player.SteamUserId, fileName, true);
            Context.Respond($"Imported grid");
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

        [Command("addsub", "add a substation from current location to target station")]
        [Permission(MyPromoteLevel.Admin)]
        public void addsub(string existingStationName)
        {
            var generated = 0;

            if (Core.StationStorage.GetAll().Any(x => x.FileName.Replace(".json", "") == existingStationName))
            {
                var station = Core.StationStorage.GetAll()
                    .FirstOrDefault(x => x.FileName.Replace(".json", "") == existingStationName);
                var gps = new MyGps();
                gps.Coords = Context.Player.Character.GetPosition();
                gps.Name = "Station";
                station.SubstationGpsStrings.Add(gps.ToString());
                Core.StationStorage.Save(station);
                Context.Respond("Sub station added, !crunchecon reload to refresh stations.");
            }
        }

        [Command("generate", "generate stations using an existing station as a template")]
        [Permission(MyPromoteLevel.Admin)]
        public void GenerateStations(string ownerFactionTag, string existingStationName, int amountToGenerate, int minDistanceFromPlanet = 50000, int maxDistanceFromPlanet = 250000, int safezoneSize = 250)
        {
            var faction = MySession.Static.Factions.TryGetFactionByTag(ownerFactionTag);
            if (faction == null)
            {
                Context.Respond("Target faction null.");
                return;
            }
            var generated = 0;
            var planets = MyPlanets.GetPlanets();

            if (Core.StationStorage.GetAll().Any(x => x.FileName.Replace(".json", "") == existingStationName))
            {
                var station = Core.StationStorage.GetAll()
                    .FirstOrDefault(x => x.FileName.Replace(".json", "") == existingStationName);
                if (station.GetGrid() == null)
                {
                    Context.Respond("Station grid not found, run the command in the sector the station lives in.");
                    return;
                }

                var path = $"{Core.path}\\Grids\\{station.GetGrid().DisplayName}.sbc";
                if (!File.Exists(path))
                {
                    GridManager.SaveGridNoDelete(path, $"{station.GetGrid().DisplayName}.sbc", false, false, new List<MyCubeGrid>(){station.GetGrid()});
                }

                for (int i = 0; i < amountToGenerate; i++)
                {
                    var planetToUse = planets.GetRandomItemFromList();
                    var planetPosition = planetToUse.PositionComp.GetPosition();

                    if (minDistanceFromPlanet < planetToUse.AtmosphereRadius)
                    {
                        minDistanceFromPlanet = (int)(planetToUse.AtmosphereRadius + 25000);
                    }

                    // Generate a random direction vector
                    Vector3D randomDirection = MyUtils.GetRandomVector3Normalized();

                    // Generate a random distance within the specified range
                    double randomDistance = MyUtils.GetRandomDouble(minDistanceFromPlanet, maxDistanceFromPlanet);

                    // Calculate the new position by adding the random direction multiplied by the random distance
                    Vector3D newPosition = planetPosition + randomDirection * randomDistance;
                    var inGrav = MyGravityProviderSystem.IsPositionInNaturalGravity(newPosition);
                    var attempts = 0;
                    while (inGrav)
                    {
                        if (attempts >= 10)
                        {
                            inGrav = false;
                            continue;
                        }
                        attempts++;
                        minDistanceFromPlanet += 15000;
                        randomDistance = MyUtils.GetRandomDouble(minDistanceFromPlanet, maxDistanceFromPlanet);
                        newPosition = planetPosition + randomDirection * randomDistance;
                        inGrav = MyGravityProviderSystem.IsPositionInNaturalGravity(newPosition);
                    }

                    //spawn the station
                    var spawnedGrid = GridManagerUpdated.LoadGrid(path, newPosition, false,
                        (ulong)faction.Members.FirstOrDefault().Key,$"{station.GetGrid().DisplayName}  {i}",false);
                    var gps = new MyGps();
                    gps.Coords = newPosition;
                    gps.Name = "Station";
                    if (spawnedGrid.Any())
                    {
                        station.SubstationGpsStrings.Add(gps.ToString());
                        MyObjectBuilder_SafeZone objectBuilderSafeZone = new MyObjectBuilder_SafeZone();
                        objectBuilderSafeZone.PositionAndOrientation = new MyPositionAndOrientation?(new MyPositionAndOrientation(newPosition, Vector3.Forward, Vector3.Up));
                        objectBuilderSafeZone.PersistentFlags = MyPersistentEntityFlags2.InScene;
                        objectBuilderSafeZone.Shape = MySafeZoneShape.Sphere;
                        objectBuilderSafeZone.Radius = (float)safezoneSize;
                        objectBuilderSafeZone.Enabled = true;
                        objectBuilderSafeZone.DisplayName = $"Store Safezone";
                        objectBuilderSafeZone.AccessTypeGrids = MySafeZoneAccess.Blacklist;
                        objectBuilderSafeZone.AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist;
                        objectBuilderSafeZone.AccessTypeFactions = MySafeZoneAccess.Blacklist;
                        objectBuilderSafeZone.AccessTypePlayers = MySafeZoneAccess.Blacklist;
                        MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                        {
                            MyEntity ent =
                                Sandbox.Game.Entities.MyEntities.CreateFromObjectBuilderAndAdd(
                                    (MyObjectBuilder_EntityBase)objectBuilderSafeZone, true);
                        });
                    }
                    generated++;
                }
                Core.StationStorage.Save(station);
            }
            else
            {
                Context.Respond("No station of that name found.");
            }

            Context.Respond($"Generated {generated} stations.");
        }

        [Command("teststore", "test adding items to a keen NPC store")]
        [Permission(MyPromoteLevel.Admin)]
        public void TestStore()
        {
            if (!MyDefinitionId.TryParse("Ingot", "Iron", out MyDefinitionId id)) return;
            SerializableDefinitionId itemId = new SerializableDefinitionId(id.TypeId, "Iron");

            int price = 500;

            int amount = 50000;

            MyStoreItemData itemInsert =
                new MyStoreItemData(itemId, amount, price,
                    null, null);
            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> gridWithSubGrids = GridFinder.FindLookAtGridGroup(Context.Player.Character);
            List<MyCubeGrid> grids = new List<MyCubeGrid>();
            foreach (var item in gridWithSubGrids)
            {
                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in item.Nodes)
                {
                    MyCubeGrid grid = groupNodes.NodeData;
                    var station = MySession.Static.Factions.GetStationByGridId(grid.EntityId);
                    if (station != null)
                    {
                        var storeid = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.CONTRACT,
                            MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM) + Core.random.Next(1, 200);
                        MyStoreItem test = new MyStoreItem(storeid, itemId, amount, price, StoreItemTypes.Offer);
                        station.StoreItems.Add(test);

                    }

                }
            }
            Context.Respond($"Done");
        }
    }
}
