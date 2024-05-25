using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.Entities.Blocks;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.Abstracts
{
    public abstract class ContractConfigAbstract : IContractConfig
    {
        public virtual void Setup()
        {
            //this contract has no setup requirements, but say if you had a list of prefabs, you would populate with default values here
        }

        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            var contract = Generate(__instance, keenstation, idUsedForDictionary);
            var delivery = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = delivery.Item1;
            contract.FactionId = delivery.Item2;
            return contract;
        }

        public abstract ICrunchContract Generate(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary);

        public virtual Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            //if any custom delivery gpses assigned use these
            if (this.DeliveryGPSes != null && this.DeliveryGPSes.Any())
            {
                var random = this.DeliveryGPSes.GetRandomItemFromList();
                var GPS = GPSHelper.ScanChat(random);
                if (GPS != null)
                {
                    return Tuple.Create(GPS.Coords, 0l);
                }
            }

            //if its a keen station, get another keen station
            if (keenstation != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    //this will only pick stations from the same faction
                    //var found = StationHandler.KeenStations.Where(x => x.FactionId == keenstation.FactionId).ToList().GetRandomItemFromList();
                    var found = StationHandler.KeenStations.GetRandomItemFromList();
                    var foundFaction = MySession.Static.Factions.TryGetFactionById(found.FactionId);
                    if (foundFaction == null)
                    {
                        i++;
                        continue;
                    }

                    return Tuple.Create(found.Position, foundFaction.FactionId);
                }
            }

            //if its not a custom station, get a random keen one
            var thisStation = StationHandler.GetStationNameForBlock(idUsedForDictionary);
            if (thisStation == null)
            {
                var keenEndResult = StationHandler.KeenStations.GetRandomItemFromList();
                if (keenEndResult != null)
                {
                    var foundFaction = MySession.Static.Factions.TryGetFactionById(keenEndResult.FactionId);
                    if (foundFaction != null)
                    {
                        return Tuple.Create(keenEndResult.Position, foundFaction.FactionId);
                    }
                }
            }
            else
            {
                var station = Core.StationStorage.GetAll().Where(x => x.UseAsDeliveryLocation && !string.Equals(x.FileName, thisStation)).ToList().GetRandomItemFromList();
                var foundFaction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                var GPS = GPSHelper.ScanChat(station.LocationGPS);
                return Tuple.Create(GPS.Coords, foundFaction.FactionId);
            }

            return Tuple.Create(Vector3D.Zero, 0l);
        }

        public int AmountOfContractsToGenerate { get; set; }
        public long SecondsToComplete { get; set; }
        public int ReputationGainOnCompleteMin { get; set; }
        public int ReputationGainOnCompleteMax { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public int ReputationRequired { get; set; }
        public float ChanceToAppear { get; set; }
        public long CollateralMin { get; set; }
        public long CollateralMax { get; set; }
        public List<string> DeliveryGPSes { get; set; }
    }
}
