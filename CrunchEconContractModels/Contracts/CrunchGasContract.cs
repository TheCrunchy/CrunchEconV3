using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
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

namespace CrunchEconV3.Models.Contracts
{
    public class CrunchGasContract : ICrunchContract
    {
        public string ContractType { get; set; } = "GasHauling";
        public MyObjectBuilder_Contract BuildUnassignedContract(string descriptionOverride = "")
        {
            throw new NotImplementedException();
        }

        public MyObjectBuilder_Contract BuildAssignedContract()
        {
            throw new NotImplementedException();
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
        }

        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            throw new NotImplementedException();
        }

        public long GasAmount { get; set; }
        public string GasName { get; set; }
        public int ReputationRequired { get; set; }
        public long ContractId { get; set; }
        public long BlockId { get; set; }
        public long AssignedPlayerIdentityId { get; set; }
        public ulong AssignedPlayerSteamId { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public long FactionId { get; set; }
        public long RewardMoney { get; set; }
        public long DistanceReward { get; set; }
        public Vector3 DeliverLocation { get; set; }
        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
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
                    && FacUtils.GetPlayersFaction(FacUtils.GetOwner(x as MyCubeGrid)).FactionId == this.FactionId).ToList();
                var tanks = new List<IMyGasTank>();
                var storeTanks = new List<IMyGasTank>();
                foreach (var grid in playersGrids)
                {
                    Core.Log.Info("Player tanks");
                    tanks.AddRange(grid.GetFatBlocks<IMyGasTank>());
                }
                foreach (var grid in storeGrid)
                {
                    Core.Log.Info("store tanks");
                    storeTanks.AddRange(grid.GetFatBlocks<IMyGasTank>());
                }
                var tankGroup = TankHelper.MakeTankGroup(tanks, this.AssignedPlayerIdentityId, 0, this.GasName);
                var storeTankGroup = TankHelper.MakeTankGroup(storeTanks, storeTanks.FirstOrDefault()?.OwnerId ?? 0, 0, this.GasName);
                Core.Log.Info($"{tankGroup.GasInTanks}");
                if (tankGroup.GasInTanks >= this.GasAmount)
                {
                    EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);

                    TankHelper.RemoveGasFromTanksInGroup(tankGroup, this.GasAmount);
                    TankHelper.AddGasToTanksInGroup(storeTankGroup, this.GasAmount);
                    if (this.ReputationGainOnComplete != 0)
                    {
                        MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                            this.FactionId, this.ReputationGainOnComplete, true);
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

        public void FailContract()
        {
            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1);
            }

            Core.SendMessage("Contracts",
                DateTime.Now > ExpireAt ? $"{this.Name} failed, time expired." : $"{this.Name} failed.", Color.Red,
                this.AssignedPlayerSteamId);
        }

        public bool CanAutoComplete { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }
        public void SendDeliveryGPS()
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

        public void DeleteDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            gpscol.SendDeleteGpsRequest(this.AssignedPlayerIdentityId, GpsId);
        }

        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
    }
}
