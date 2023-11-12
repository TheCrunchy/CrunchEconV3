using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRageMath;

namespace CrunchEconV3.Models.Contracts
{
    public class CrunchGasContract : ICrunchContract
    {
        public CrunchContractTypes ContractType { get; set; }
        public void Start()
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
            if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                return false;
            if (player.Character == null || player?.Controller.ControlledEntity is not MyCockpit controller)
                return false;
            float distance = Vector3.Distance(this.DeliverLocation, (Vector3)currentPosition);
            if (!(distance <= 500)) return false;



            return true;
        }

        public void FailContract()
        {
            if (this.ReputationLossOnAbandon != 0)
            {
                var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(this.AssignedPlayerIdentityId, this.FactionId);
                MySession.Static.Factions.SetReputationBetweenPlayerAndFaction(this.AssignedPlayerIdentityId, this.FactionId, rep.Item2 - this.ReputationLossOnAbandon);
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
            gpsRef.Name = $"Passenger Delivery Location";
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
