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

        public static void ReadyForRefresh()
        {
            RefreshAt.Clear();
        }

        public static DateTime NextSave = DateTime.Now;
        public static async Task DoStationLoop()
        {
            foreach (var station in Core.StationStorage.GetAll())
            {
                //first find the station ingame
                if (station.Logics != null && station.Logics.Any())
                {
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
                            grid = storeGrid.FirstOrDefault(x => x.GetFatBlocks().OfType<MyStoreBlock>().Any());
                        }
                    }

                    if (grid == null)
                    {
                        Core.Log.Error($"{station.FileName} grid not found");
                        continue;
                    }

                    station.SetGrid(grid);
                    foreach (var logic in station.Logics.OrderBy(x => x.Priority))
                    {
                        try
                        {
                            var ShouldNextOneRun = await logic.DoLogic((MyCubeGrid)grid);
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

                    if (DateTime.Now >= NextSave)
                    {
                        Core.StationStorage.Save(station);
                    }
                }
            }

            if (DateTime.Now >= NextSave)
            {
                NextSave = DateTime.Now.AddMinutes(5);
            }
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
                Core.Log.Info("Station name not found");
                return false;
            }
            var foundStation = Core.StationStorage.GetAll().FirstOrDefault(x => x.FileName == stationName);
            if (foundStation == null)
            {
                Core.Log.Info("FoundStation name not found");
                return false;
            }

            RefreshAt.Remove(blockId);
            RefreshAt.Add(blockId, DateTime.Now.AddSeconds(foundStation.SecondsBetweenContractRefresh));
            return true;
        }

        public static string GetStationNameForBlock(long blockId)
        {
            if (MappedContractBlocks.TryGetValue(blockId, out var stationName)) return stationName;

            foreach (var station in Core.StationStorage.GetAll())
            {
                var gps = GPSHelper.ScanChat(station.LocationGPS);
                if (gps == null)
                {
                    continue;
                }
                var sphere = new BoundingSphereD(gps.Coords, 1000 * 2);
                if (MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyContractBlock>()
                    .Where(x => !x.Closed).Any(block => block.EntityId == blockId && block.GetOwnerFactionTag() == station.FactionTag))
                {
                    MappedContractBlocks.Add(blockId, station.FileName);
                    return station.FileName;
                }

                var test = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyContractBlock>()
                    .Where(x => !x.Closed).FirstOrDefault(block => block.EntityId == blockId);
                if (test != null)
                {
                    Core.Log.Error($"{test.GetOwnerFactionTag()} is not the expected tag of {station.FactionTag}");
                }
                
            }
            return null;
        }

        private static MethodInfo GetByStationId;
        public static List<ICrunchContract> GenerateNewContracts(long blockId)
        {
            BlocksContracts.Remove(blockId);

            List<ICrunchContract> NewContracts = new List<ICrunchContract>();
            if (GetByStationId == null)
            {
                GetByStationId = MySession.Static.Factions.GetType().GetMethod("GetStationByStationId", BindingFlags.Instance | BindingFlags.NonPublic);
            }

            if (!MappedStations.ContainsKey(blockId) && !MappedContractBlocks.ContainsKey(blockId))
            {

                MyStation stat = null;
                object[] MethodInput = new object[] { blockId };
                var result = GetByStationId.Invoke(MySession.Static.Factions, MethodInput);
                if (result != null)
                {
                    stat = (MyStation)result;
                }
                MappedStations.Add(blockId, stat);
            }

            if (MappedStations.TryGetValue(blockId, out var station))
            {
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

            var stationName = GetStationNameForBlock(blockId);
            if (stationName == null)
            {
                return NewContracts;
            }
            var foundStation = Core.StationStorage.GetAll().FirstOrDefault(x => x.FileName == stationName);
            var location = MyAPIGateway.Entities.GetEntityById(blockId) as MyContractBlock;
            if (foundStation == null) return null;

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
            return NewContracts;
        }
    }
}
