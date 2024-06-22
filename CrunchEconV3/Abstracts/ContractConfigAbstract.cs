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
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
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
            if (contract == null)
            {
                return null;
            }
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

            if (contract.DeliverLocation == null || contract.DeliverLocation.Equals(Vector3.Zero))
            {
                return null;
            }
            return contract;
        }

        public abstract ICrunchContract GenerateTheRest(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary);

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

            List<Tuple<Vector3D, long>> availablePositions = new List<Tuple<Vector3D, long>>();

            // If it's not a custom station, get random keen ones
            var thisStation = StationHandler.GetStationNameForBlock(idUsedForDictionary);
            if (thisStation != null)
            {
                var stations = Core.StationStorage.GetAll()
                    .Where(x => x.UseAsDeliveryLocation && !string.Equals(x.FileName, thisStation))
                    .ToList();

                foreach (var station in stations)
                {
                    var foundFaction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                    var GPS = GPSHelper.ScanChat(station.LocationGPS);
                    availablePositions.Add(Tuple.Create(GPS.Coords, foundFaction.FactionId));
                }
            }

            if (MySession.Static.Settings.EnableEconomy)
            {
                var positions = MySession.Static.Factions.GetNpcFactions()
                    .Where(x => x.Stations.Any())
                    .SelectMany(x => x.Stations)
                    .Where(x => x.StationEntityId != keenstation.StationEntityId)
                    .Select(x => Tuple.Create(x.Position, x.FactionId))
                    .ToList();
                availablePositions.AddRange(positions);
            }


            return availablePositions.GetRandomItemFromList() ?? Tuple.Create(Vector3D.Zero, 0l);
        }

        public virtual int AmountOfContractsToGenerate { get; set; }
        public virtual long SecondsToComplete { get; set; }
        public virtual int ReputationGainOnCompleteMin { get; set; }
        public virtual int ReputationGainOnCompleteMax { get; set; }
        public virtual int ReputationLossOnAbandon { get; set; }
        public virtual int ReputationRequired { get; set; }
        public virtual float ChanceToAppear { get; set; }
        public virtual long CollateralMin { get; set; }
        public virtual long CollateralMax { get; set; }
        public virtual List<string> DeliveryGPSes { get; set; }
    }
}
