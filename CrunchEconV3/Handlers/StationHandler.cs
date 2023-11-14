using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI;
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

            MyStation station = null;
            if (!MappedStations.ContainsKey(blockId) && !MappedContractBlocks.ContainsKey(blockId))
            {
                object[] MethodInput = new object[] { blockId };
                var result = GetByStationId.Invoke(MySession.Static.Factions, MethodInput);
                if (result != null)
                {
                    station = (MyStation)result;
                }
                MappedStations.Add(blockId, station);
            }
            if (station != null)
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

                        var generated = ContractGenerator.GenerateContract(contract, station.Position, blockId);
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
            var location = MyAPIGateway.Entities.GetEntityById(blockId).PositionComp.GetPosition();
            if (foundStation == null) return null;

            foreach (var contract in foundStation.GetConfigs())
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

                    var generated = ContractGenerator.GenerateContract(contract, location, blockId);
                    if (generated == null) continue;
                    NewContracts.Add(generated);
                    i++;
                }
            }
            return NewContracts;
        }
    }
}
