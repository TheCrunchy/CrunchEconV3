﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI;
using VRageMath;

namespace CrunchEconV3.Handlers
{
    public static class StationHandler
    {
        private static Dictionary<long, string> MappedContractBlocks = new Dictionary<long, string>();
        private static Dictionary<long, DateTime> RefreshAt = new Dictionary<long, DateTime>();

        public static Dictionary<long, List<ICrunchContract>> BlocksContracts = new Dictionary<long, List<ICrunchContract>>();

        public static bool NeedsRefresh(long blockId)
        {
            if (RefreshAt.TryGetValue(blockId, out var time))
            {
                return time <= DateTime.Now;
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
            RefreshAt.Add(blockId, DateTime.Now.AddMinutes(15));

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

        public static List<ICrunchContract> GenerateNewContracts(long blockId)
        {
            var stationName = GetStationNameForBlock(blockId);

            if (stationName == null)
            {
                return new List<ICrunchContract>();
            }
            var foundStation = Core.StationStorage.GetAll().FirstOrDefault(x => x.FileName == stationName);
            var location = MyAPIGateway.Entities.GetEntityById(blockId).PositionComp.GetPosition();
            if (foundStation == null) return null;
            BlocksContracts.Remove(blockId);
            List<ICrunchContract> NewContracts = new List<ICrunchContract>();
            Core.Log.Info($"{foundStation.FileName}");
            foreach (var contract in foundStation.Contracts)
            {
                var i = 0;
             
                while (i <= contract.AmountOfContractsToGenerate)
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
