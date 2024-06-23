using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrunchEconV3;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Contracts
{
    public class CrunchGasContractImplementation : ContractAbstract
    {
        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"You must deliver {this.GasAmount:##,###}L {this.GasName} in none stockpile tanks.";
            return BuildUnassignedContract(contractDescription);
        }

        public override Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            if (this.DeliverLocation.Equals(Vector3.Zero))
            {
                Core.Log.Error("Error getting a delivery point for this contract");
                return Tuple.Create(false, MyContractResults.Error_InvalidData);
            }
            if (this.ReputationRequired != 0)
            {
                var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                if (faction != null)
                {
                    var reputation =
                        MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(identityId, faction.FactionId);
                    if (this.ReputationRequired > 0)
                    {
                        if (reputation.Item2 < this.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                    else
                    {
                        if (reputation.Item2 > this.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                }
            }
            if (this.CollateralToTake > 0)
            {
                if (EconUtils.getBalance(identityId) < this.CollateralToTake)
                {
                    return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientFunds);
                }
            }

            var current = playerData.GetContractsForType(this.ContractType);
            if (current.Count >= 1)
            {
                return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
            }
            var test = __instance.CubeGrid.GetGridGroup(GridLinkTypeEnum.Physical);
            var grids = new List<IMyCubeGrid>();
            var tanks = new List<IMyGasTank>();


            test.GetGrids(grids);
            foreach (var gridInGroup in grids)
            {
                tanks.AddRange(gridInGroup.GetFatBlocks<IMyGasTank>());
            }

            var playerTanks = TankHelper.MakeTankGroup(tanks, identityId, __instance.OwnerId, this.GasName);
            if (playerTanks.GasInTanks < this.GasAmount)
            {
                return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientSpace);
            }

            if (this.CollateralToTake > 0)
            {
                EconUtils.takeMoney(identityId, this.CollateralToTake);
            }
            this.AssignedPlayerIdentityId = identityId;
            this.AssignedPlayerSteamId = playerData.PlayerSteamId;
            return Tuple.Create(true, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientSpace);
        }

        public override bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (DateTime.Now > ExpireAt)
            {
                FailContract();
                return true;
            }

            return false;
        }
        public override void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Deliver {GasAmount}L of {GasName} to");
            sb.AppendLine("Contract Delivery Location.");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"Deliver {GasAmount:##,###}L of {GasName} to";
            gpsRef.GPSColor = Color.Orange;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = TimeSpan.FromSeconds(6000);
            gpsRef.Description = sb.ToString();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
        }

        public override bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            try
            {
                if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                    return false;

                float distance = Vector3.Distance(this.DeliverLocation, (Vector3)currentPosition);
                if (!(distance <= 500)) return false;

                var sphere = new BoundingSphereD(this.DeliverLocation, 1000 * 2);
                var playersGrids = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<IMyCubeGrid>()
                    .Where(x => !x.Closed && FacUtils.IsOwnerOrFactionOwned(x as MyCubeGrid, this.AssignedPlayerIdentityId, true)).ToList();

                var storeGrid = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<IMyCubeGrid>().Where(x => !x.Closed
                    && FacUtils.GetPlayersFaction(FacUtils.GetOwner(x as MyCubeGrid)) != null
                    && FacUtils.GetPlayersFaction(FacUtils.GetOwner(x as MyCubeGrid)).FactionId == this.DeliveryFactionId).ToList();
                var tanks = new List<IMyGasTank>();
                var storeTanks = new List<IMyGasTank>();
                foreach (var grid in playersGrids)
                {
                    tanks.AddRange(grid.GetFatBlocks<IMyGasTank>());
                }
                foreach (var grid in storeGrid)
                {

                    storeTanks.AddRange(grid.GetFatBlocks<IMyGasTank>());
                }
                var tankGroup = TankHelper.MakeTankGroup(tanks, this.AssignedPlayerIdentityId, 0, this.GasName);
                var storeTankGroup = TankHelper.MakeTankGroup(storeTanks, storeTanks.FirstOrDefault()?.OwnerId ?? 0, 0, this.GasName);
                if (tankGroup.GasInTanks >= this.GasAmount)
                {
                    EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);

                    TankHelper.RemoveGasFromTanksInGroup(tankGroup, this.GasAmount);
                    TankHelper.AddGasToTanksInGroup(storeTankGroup, this.GasAmount);
                    if (this.ReputationGainOnComplete != 0)
                    {
                        MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                            this.FactionId, this.ReputationGainOnComplete, ReputationChangeReason.Contract, true);
                    }
                    return true;
                }
            }
            catch (Exception e)
            {
                Core.Log.Error($"Gas try complete error {e}");
                return true;
            }

            return true;
        }

        public long GasAmount { get; set; }
        public string GasName { get; set; }
    }

    public class GasContractConfig : IContractConfig
    {
        //check the discord for documentation on what each thing in the interface does 
        //https://discord.gg/cQFJeKvVAA
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
        }

        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            if (this.ChanceToAppear < 1)
            {
                var random = CrunchEconV3.Core.random.NextDouble();
                if (random > this.ChanceToAppear)
                {
                    return null;
                }
            }
            var description = new StringBuilder();
            var contract = new CrunchGasContractImplementation();
            contract.GasAmount = CrunchEconV3.Core.random.Next((int)this.AmountInLitresMin, (int)this.AmountInLitresMax);
            contract.RewardMoney = contract.GasAmount * (CrunchEconV3.Core.random.Next((int)this.PricePerLitreMin, (int)this.PricePerLitreMax));
            contract.ContractType = "CrunchGasHauling";
            contract.BlockId = idUsedForDictionary;
            contract.GasName = this.GasSubType;
            contract.ReputationGainOnComplete = CrunchEconV3.Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Deliver";
            contract.Name = $"{contract.GasName} Delivery";
            contract.ReputationRequired = this.ReputationRequired;
            contract.ReadyToDeliver = true;
            contract.CollateralToTake = (CrunchEconV3.Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            description.AppendLine($"You must deliver {contract.GasAmount:##,###}L {contract.GasName} in none stockpile tanks.");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }
            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            if (contract.DeliverLocation == null || contract.DeliverLocation.Equals(Vector3.Zero))
            {
                return null;
            }
            contract.Description = description.ToString();
            return contract;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation,
            long idUsedForDictionary)
        {
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


            if (this.DeliveryGPSes != null && this.DeliveryGPSes.Any())
            {
                var random = this.DeliveryGPSes.GetRandomItemFromList();
                var GPS = GPSHelper.ScanChat(random);
                if (GPS != null)
                {
                    return Tuple.Create(GPS.Coords, 0l);
                }
            }

            var thisStation = StationHandler.GetStationNameForBlock(idUsedForDictionary);
            if (thisStation == null)
            {
                return Tuple.Create(Vector3D.Zero, 0l);
            }
            for (int i = 0; i < 10; i++)
            {
                var station = Core.StationStorage.GetAll().Where(x => x.UseAsDeliveryLocation).ToList().GetRandomItemFromList();
                if (station.FileName == thisStation)
                {
                    i++;
                    continue;
                }
                var foundFaction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                var GPS = GPSHelper.ScanChat(station.LocationGPS);
                return Tuple.Create(GPS.Coords, foundFaction.FactionId);
            }
            var keenEndResult = StationHandler.KeenStations.GetRandomItemFromList();
            if (keenEndResult != null)
            {
                var foundFaction = MySession.Static.Factions.TryGetFactionById(keenEndResult.FactionId);
                if (foundFaction != null)
                {
                    return Tuple.Create(keenEndResult.Position, foundFaction.FactionId);
                }
            }
            return Tuple.Create(Vector3D.Zero, 0l);
        }

        public int AmountOfContractsToGenerate { get; set; } = 2;
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;
        public int ReputationRequired { get; set; } = 0;
        public float ChanceToAppear { get; set; } = 1;
        public long CollateralMin { get; set; } = 1000;
        public long CollateralMax { get; set; } = 5000;
        public List<string> DeliveryGPSes { get; set; }
        public string GasSubType { get; set; } = "Hydrogen";
        public long AmountInLitresMin { get; set; } = 200 * 1000;
        public long AmountInLitresMax { get; set; } = 480 * 1000;
        public long PricePerLitreMin { get; set; } = 50;
        public long PricePerLitreMax { get; set; } = 75;
    }
}
