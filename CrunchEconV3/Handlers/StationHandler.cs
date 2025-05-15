using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace CrunchEconV3.Handlers
{
    public static class StationHandler
    {
        private static Dictionary<long, string> MappedContractBlocks = new Dictionary<long, string>();
        private static Dictionary<long, DateTime> RefreshAt = new Dictionary<long, DateTime>();
        public static Dictionary<long, MyStation> MappedStations = new Dictionary<long, MyStation>();
        public static List<MyStation> KeenStations = new List<MyStation>();
        public static Dictionary<long, List<ICrunchContract>> BlocksContracts = new Dictionary<long, List<ICrunchContract>>();
        public static List<IContractConfig> DefaultAvailables = new List<IContractConfig>();

        public static void ReadyForRefresh()
        {
            RefreshAt.Clear();
        }

        public static DateTime NextSave = DateTime.Now;
        public static void DoStationLoop()
        {
            foreach (var station in Core.StationStorage.GetAll())
            {
                //first find the station ingame
                DoDebugMessage($"{station.FileName} loop");
                MyCubeGrid grid = station.GetGrid();
                //MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                //{
                //    Core.Log.Info(station.GridEntityId);
                //    MyAPIGateway.Entities.TryGetEntityById(station.GridEntityId, out var thing);
                //    grid = (MyCubeGrid)thing;
                //});

                var faction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                if (faction == null)
                {
                    Core.Log.Error($"{station.FileName} faction not found");
                    continue;
                }

                if (grid == null && station.IsFirstLoad())
                {
                    station.SetFirstLoad(false);

                    var gps = GPSHelper.ScanChat(station.LocationGPS);
                    if (gps == null)
                    {
                        continue;
                    }
                    var sphere = new BoundingSphereD(gps.Coords, 2000);

                    var storeGrid = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>().Where(x => !x.Closed
                        && FacUtils.GetPlayersFaction(FacUtils.GetOwner(x as MyCubeGrid)) != null
                        && FacUtils.GetPlayersFaction(FacUtils.GetOwner(x as MyCubeGrid)).FactionId == faction.FactionId).ToList();
                    if (storeGrid.Any())
                    {
                        grid = storeGrid.FirstOrDefault(x => x.GetFatBlocks().OfType<MyStoreBlock>().Any()) ?? storeGrid.FirstOrDefault(x => x.GetFatBlocks().OfType<MyContractBlock>().Any());
                    }

                }

                if (grid == null)
                {
                    DoDebugMessage($"{station.FileName} grid not found within 2km of GPS.");
                    //   Core.Log.Error($"{station.FileName} grid not found");
                    continue;
                }
        
                station.SetGrid(grid);
                if (station.Logics != null && station.Logics.Any())
                {
             
                    foreach (var logic in station.Logics.OrderBy(x => x.Priority))
                    {
                        DoDebugMessage($"{station.FileName} Running logic {logic.GetType().Name}.");
                        try
                        {
                            var ShouldNextOneRun = logic.DoLogic((MyCubeGrid)grid).Result;
                            if (!ShouldNextOneRun)
                            {
                                break;
                            }
                        }
                        catch (Exception e)
                        {
                            Core.Log.Error($"Station logic error {e}");
                        }
                    }


                }
                if (DateTime.Now >= NextSave)
                {
                    Core.StationStorage.Save(station);
                }
            }

            if (DateTime.Now >= NextSave)
            {
                NextSave = DateTime.Now.AddMinutes(5);
            }
        }

        public static bool SetNPCNeedsRefresh(long blockId, DateTime time)
        {
            RefreshAt[blockId] = time;
            return true;
        }

        public static bool NPCNeedsRefresh(long blockId)
        {

            if (RefreshAt.TryGetValue(blockId, out var time))
            {
                if (time >= DateTime.Now)
                {
                    return false;
                }
            }

            RefreshAt.Remove(blockId);
            RefreshAt.Add(blockId, DateTime.Now.AddSeconds(Core.config.KeenNPCSecondsBetweenRefresh));
            return true;
        }
        public static bool NeedsRefresh(long blockId)
        {
            if (RefreshAt.TryGetValue(blockId, out var time))
            {
                if (time >= DateTime.Now)
                {
                    return false;
                }
            }

            var stationName = GetStationNameForBlock(blockId);
            if (stationName == null)
            {
                Core.Log.Info($"Station name not found {blockId}");
                return false;
            }
            DoDebugMessage($"A station name was found {stationName}");
            var foundStation = Core.StationStorage.GetAll().FirstOrDefault(x => x.FileName == stationName);
            if (foundStation == null)
            {
                Core.Log.Info($"FoundStation name not found {blockId}");
                return false;
            }
            DoDebugMessage($"Refreshing contracts for {stationName}");

            RefreshAt.Remove(blockId);
            RefreshAt.Add(blockId, DateTime.Now.AddSeconds(foundStation.SecondsBetweenContractRefresh));
            return true;
        }

        public static string GetStationNameForBlock(long blockId)
        {
            if (MappedContractBlocks.TryGetValue(blockId, out var stationName)) return stationName;
            MyContractBlock location = (MyContractBlock)MyAPIGateway.Entities.GetEntityById(blockId);
            if (location == null)
            {
                DoDebugMessage($"Couldnt get the contract blocks entity by its Id");
                return null;
            }
            var factionOwner = FacUtils.GetPlayersFaction(FacUtils.GetOwner(location.CubeGrid));
            if (factionOwner == null)
            {
                Core.Log.Info($"{blockId} cannot find a valid faction owner.");
                return null;
            }
            DoDebugMessage($"Checking {Core.StationStorage.GetAll().Count} Stations");
            foreach (var station in Core.StationStorage.GetAll())
            {
                DoDebugMessage($"Checking stationGetGrid for {station.LocationGPS}");
                if (station.GetGrid() != null && station.GetGrid().EntityId == location.CubeGrid.GetBiggestGridInGroup().EntityId)
                {
                    if (location.OwnerId == FacUtils.GetOwner(station.GetGrid()))
                    {
                        MappedContractBlocks.Add(blockId, station.FileName);
                        return station.FileName;
                    }
                }
                DoDebugMessage("Scanning GPS");
                var gps = GPSHelper.ScanChat(station.LocationGPS);
                if (gps == null)
                {
                    DoDebugMessage($"GPS Invalid, skipping {station.LocationGPS}");
                    continue;
                }
                DoDebugMessage($"Searching for contract blocks owned by {station.FactionTag} within 2km");
                var sphere = new BoundingSphereD(gps.Coords, 1000 * 2);
                if (MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyContractBlock>()
                    .Where(x => !x.Closed).Any(block => block.EntityId == blockId && factionOwner.Tag == station.FactionTag))
                {
                    MappedContractBlocks.Add(blockId, station.FileName);
                    return station.FileName;
                }
                DoDebugMessage($"No blocks found.");

                var test = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyContractBlock>()
                    .Where(x => !x.Closed).FirstOrDefault(block => block.EntityId == blockId);
                if (test != null)
                {
                    Core.Log.Error($"{test.GetOwnerFactionTag()} is not the expected tag of {station.FactionTag}");
                }

            }
            return null;
        }

        public static void DoDebugMessage(string message)
        {
            if (Core.config.DebugMode)
            {
                Core.Log.Error($"ECON DEBUG {message}");
            }
        }

        private static MethodInfo GetByStationId;
        public static List<ICrunchContract> GenerateNewContracts(long blockId)
        {
            BlocksContracts.Remove(blockId);
            //  Core.Log.Info(1);
            List<ICrunchContract> NewContracts = new List<ICrunchContract>();
            if (GetByStationId == null)
            {
                GetByStationId = MySession.Static.Factions.GetType().GetMethod("GetStationByStationId", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            //    Core.Log.Info(2);
            if (!MappedStations.ContainsKey(blockId) && !MappedContractBlocks.ContainsKey(blockId))
            {

                MyStation stat = null;
                object[] MethodInput = new object[] { blockId };
                var result = GetByStationId.Invoke(MySession.Static.Factions, MethodInput);
                if (result != null)
                {
                    stat = (MyStation)result;
                }

                if (stat != null)
                {
                    MappedStations.Add(blockId, stat);
                }
            }
            //    Core.Log.Info(3);
            if (MappedStations.TryGetValue(blockId, out var station))
            {
                //       Core.Log.Info(3.5);
                if (Core.config.UseDefaultSetup)
                {
                    foreach (var contract in DefaultAvailables)
                    {
                        try
                        {
                            var i = 0;

                            while (i < contract.AmountOfContractsToGenerate)
                            {
                                if (contract.ChanceToAppear < 1)
                                {
                                    var random = Core.random.NextDouble();
                                    if (random > contract.ChanceToAppear)
                                    {
                                        i++;
                                        continue;
                                    }
                                }

                                var generated = contract.GenerateFromConfig(null, station, station.Id);
                                if (generated == null) continue;
                                NewContracts.Add(generated);
                                i++;
                            }
                        }
                        catch (Exception e)
                        {
                            Core.Log.Error(e);
                        }
                    }

                    return NewContracts;
                }
                var faction = MySession.Static.Factions.TryGetFactionById(station.FactionId);
                foreach (var contract in Core.StationStorage.GetForKeen(faction.Tag))
                {
                    var i = 0;

                    while (i < contract.AmountOfContractsToGenerate)
                    {
                        if (contract.ChanceToAppear < 1)
                        {
                            var random = Core.random.NextDouble();
                            if (random > contract.ChanceToAppear)
                            {
                                i++;
                                continue;
                            }
                        }

                        var generated = contract.GenerateFromConfig(null, station, station.Id);
                        if (generated == null) continue;
                        NewContracts.Add(generated);
                        i++;
                    }
                }

                return NewContracts;
            }
            //   Core.Log.Info(4);
            var stationName = GetStationNameForBlock(blockId);
            if (stationName == null)
            {
                return NewContracts;
            }
            //   Core.Log.Info(5);
            var foundStation = Core.StationStorage.GetAll().FirstOrDefault(x => x.FileName == stationName);
            var location = MyAPIGateway.Entities.GetEntityById(blockId) as MyContractBlock;
            if (foundStation == null) return null;
            //  Core.Log.Info(6);
            if (foundStation.GetUsesDefault())
            {
                foreach (var contract in DefaultAvailables)
                {
                    try
                    {
                        var i = 0;

                        while (i < contract.AmountOfContractsToGenerate)
                        {
                            if (contract.ChanceToAppear < 1)
                            {
                                var random = Core.random.NextDouble();
                                if (random > contract.ChanceToAppear)
                                {
                                    i++;
                                    continue;
                                }
                            }

                            var generated = contract.GenerateFromConfig(location, null, blockId);
                            if (generated == null) continue;
                            NewContracts.Add(generated);
                            i++;
                        }
                    }
                    catch (Exception e)
                    {
                        Core.Log.Error(e);
                    }
                }

                return NewContracts;
            }
            foreach (var contract in foundStation.GetConfigs())
            {
                var i = 0;

                while (i < contract.AmountOfContractsToGenerate)
                {
                    i++;
                    var generated = contract.GenerateFromConfig(location, null, blockId);
                    if (generated == null) continue;
                    NewContracts.Add(generated);

                }
            }
            //    Core.Log.Info(7);
            return NewContracts;
        }
    }
}
