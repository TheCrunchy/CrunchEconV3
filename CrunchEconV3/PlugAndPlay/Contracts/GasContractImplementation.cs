using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRageMath;

namespace CrunchEconV3.PlugAndPlay.Contracts
{
    public class GasContractImplementation : ContractAbstract
    {
        public override string GetStatus()
        {
            return $"{this.Name} - {this.GasAmount:##,###}L {this.GasName}";
        }

        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"You must obtain and deliver {this.GasAmount:##,###}L {this.GasName} in none stockpile tanks.";
            return BuildUnassignedContract(contractDescription);
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

            //var playerTanks = TankHelper.MakeTankGroup(tanks, identityId, __instance.OwnerId, this.GasName);
            //if (playerTanks.GasInTanks < this.GasAmount)
            //{
            //    return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientSpace);
            //}

            if (this.CollateralToTake > 0)
            {
                EconUtils.takeMoney(identityId, this.CollateralToTake);
            }
            this.AssignedPlayerIdentityId = identityId;
            this.AssignedPlayerSteamId = playerData.PlayerSteamId;
            return Tuple.Create(true, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientSpace);
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
}
