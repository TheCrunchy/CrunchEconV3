﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace CrunchEconV3.Models.Contracts
{
    public class CrunchMiningContract : ICrunchContract
    {
        public CrunchContractTypes ContractType { get; set; }
        public long ContractId { get; set; }
        public long BlockId { get; set; }
        public long AssignedPlayerIdentityId { get; set; }
        public ulong AssignedPlayerSteamId { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public long FactionId { get; set; }
        public long RewardMoney { get; set; }
        public Vector3 DeliverLocation { get; set; }

        public String OreSubTypeName { get; set; }
        public int MinedOreAmount { get; set; }
        public int AmountToMine { get; set; }

        public void FailContract()
        {
            if (this.ReputationLossOnAbandon != 0)
            {
                var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(this.AssignedPlayerIdentityId, this.FactionId);
                MySession.Static.Factions.SetReputationBetweenPlayerAndFaction(this.AssignedPlayerIdentityId, this.FactionId, rep.Item2 - this.ReputationLossOnAbandon);
            }
            Core.SendMessage("Contracts", $"{this.Name} failed, time expired.", Color.Red, this.AssignedPlayerSteamId);
        }

        public bool CanAutoComplete { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }
        public bool SpawnOreInStation { get; set; }
        public void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Deliver " + this.AmountToMine + " " + this.OreSubTypeName + " Ore.");
            sb.AppendLine("Contract Delivery Location.");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"Mining Delivery Location {this.OreSubTypeName}";
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

        public void Start()
        {
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
        }

        public int ReputationRequired { get; set; }

        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            try
            {
                if (MinedOreAmount < AmountToMine) return false;
                if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                    return false;
                if (player.Character == null || player?.Controller.ControlledEntity is not MyCockpit controller)
                    return false;
                float distance = Vector3.Distance(this.DeliverLocation, (Vector3)currentPosition);
                if (!(distance <= 500)) return false;
                Dictionary<MyDefinitionId, int> itemsToRemove = new Dictionary<MyDefinitionId, int>();
                var parseThis = "MyObjectBuilder_Ore/" + this.OreSubTypeName;
                if (MyDefinitionId.TryParse(parseThis, out MyDefinitionId id))
                {
                    itemsToRemove.Add(id, this.AmountToMine);
                }
                List<VRage.Game.ModAPI.IMyInventory> inventories = InventoriesHandler.GetInventoriesForContract(controller.CubeGrid);
                if (!InventoriesHandler.ConsumeComponents(inventories, itemsToRemove, player.Id.SteamId)) return false;

                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);
                if (this.ReputationGainOnComplete != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        this.FactionId, this.ReputationGainOnComplete, true);
                }
                inventories.Clear();
                if (this.SpawnOreInStation)
                {
                    var sphere = new BoundingSphereD(this.DeliverLocation, 1000 * 2);
                    var foundCargo = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                        .Where(x => x.BlocksCount > 0).ToList();
                    if (foundCargo.Any())
                    {
                        foreach (var cargo in foundCargo)
                        {
                      //      Core.Log.Info("this be a grid");
                            var owner = FacUtils.GetOwner(cargo);
                            var fac = MySession.Static.Factions.TryGetPlayerFaction(owner);
                      
                            if (fac != null && fac.FactionId == this.FactionId)
                            {
                              //  Core.Log.Info($"{fac.Name}");
                             //   Core.Log.Info("Finding some inventories");
                               inventories.AddRange(InventoriesHandler.GetInventoriesForContract(cargo));
                            }
                        }

                      //  Core.Log.Info($"{inventories.Count}");
                        InventoriesHandler.SpawnItems(id, this.AmountToMine, inventories);
                      //  Core.Log.Info("spawning?");
                    }
                    else
                    {
                     //   Core.Log.Info("No cargo found");
                    }
                }
            }
            catch (Exception e)
            {
                Core.Log.Error($"Mining try complete error {e}");
                return true;
            }
            return true;

        }
    }
}
