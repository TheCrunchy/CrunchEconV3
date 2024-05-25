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
            DeliveryGPSes = new List<string>() { "Optional, not required, but put a gps here if you want" };
        }

        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            var contract = GenerateTheRest(__instance, keenstation, idUsedForDictionary);
            var delivery = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            if (keenstation != null)
            {
                contract.FactionId = keenstation.FactionId;
            }
            contract.ContractType = contract.GetType().Name;
            contract.DeliverLocation = delivery.Item1;
            contract.DeliveryFactionId = delivery.Item2;
            if (this.ChanceToAppear < 1)
            {
                var random = CrunchEconV3.Core.random.NextDouble();
                if (random > this.ChanceToAppear)
                {
                    return null;
                }
            }

            contract.BlockId = idUsedForDictionary;
            contract.ReputationGainOnComplete = Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.ReputationRequired = this.ReputationRequired;

            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));

            return contract;
        }

        public abstract ICrunchContract GenerateTheRest(MyContractBlock __instance, MyStation keenstation,
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
                var random = MySession.Static.Factions.GetNpcFactions()
                    .Where(x => x.Stations.Any()).SelectMany(x => x.Stations)
                    .Where(x => x.StationEntityId != keenstation.StationEntityId).ToList().GetRandomItemFromList();
                return Tuple.Create(random.Position, random.FactionId);

            }

            //if its not a custom station, get a random keen one
            var thisStation = StationHandler.GetStationNameForBlock(idUsedForDictionary);
            if (thisStation == null)
            {
                var random = MySession.Static.Factions.GetNpcFactions()
                    .Where(x => x.Stations.Any()).SelectMany(x => x.Stations)
                    .Where(x => x.StationEntityId != keenstation.StationEntityId).ToList().GetRandomItemFromList();
                return Tuple.Create(random.Position, random.FactionId);
            }
            else
            {
                var station = Core.StationStorage.GetAll().Where(x => x.UseAsDeliveryLocation && !string.Equals(x.FileName, thisStation)).ToList().GetRandomItemFromList();
                var foundFaction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                var GPS = GPSHelper.ScanChat(station.LocationGPS);
                return Tuple.Create(GPS.Coords, foundFaction.FactionId);
            }
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
