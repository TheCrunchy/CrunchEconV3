using System;
using System.Collections.Generic;
using System.Linq;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Handlers;
using CrunchEconV3.PlugAndPlay.Contracts.Configs;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRageMath;

namespace CrunchEconV3.PlugAndPlay.Contracts
{
    public class ItemHaulingContractImplementation : ContractAbstract
    {
        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"You must obtain and go deliver {this.ItemToDeliver.AmountToDeliver:##,###} {this.ItemToDeliver.TypeId.Replace("MyObjectBuilder_", "")} {this.ItemToDeliver.SubTypeId}";

            contractDescription += ($" ||| Distance bonus applied {this.DistanceReward:##,###}");

            return BuildUnassignedContract(contractDescription);
        }
        public List<VRage.Game.ModAPI.IMyInventory> GetStationInventories(MyCubeGrid grid)
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<VRage.Game.ModAPI.IMyInventory>();
            var gridOwnerFac = FacUtils.GetOwner(grid);

            foreach (var block in grid.GetFatBlocks().Where(x => x.OwnerId == gridOwnerFac))
            {
                if (block.DisplayNameText != null && !this.CargoNames.Contains(block.DisplayNameText))
                {
                    continue;
                }

                for (int i = 0; i < block.InventoryCount; i++)
                {
                    VRage.Game.ModAPI.IMyInventory inv = ((VRage.Game.ModAPI.IMyCubeBlock)block).GetInventory(i);
                    inventories.Add(inv);
                }
            }
            return inventories;
        }

        public override bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                return false;

            float distance = Vector3.Distance(this.DeliverLocation, (Vector3)currentPosition);
            if (!(distance <= 500)) return false;

            var sphere = new BoundingSphereD(this.DeliverLocation, 1000 * 2);
            var playersGrids = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                .Where(x => x.BlocksCount > 0 && FacUtils.IsOwnerOrFactionOwned(x, this.AssignedPlayerIdentityId, true)).ToList();


            Dictionary<MyDefinitionId, int> itemsToRemove = new Dictionary<MyDefinitionId, int>();
            var parseThis = $"{ItemToDeliver.TypeId}/" + this.ItemToDeliver.SubTypeId;
            if (MyDefinitionId.TryParse(parseThis, out MyDefinitionId id))
            {
                itemsToRemove.Add(id, this.ItemToDeliver.AmountToDeliver);
            }

            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<IMyInventory>();
            foreach (var grid in playersGrids)
            {
                inventories.AddRange(InventoriesHandler.GetInventoriesForContract(grid));
            }

            if (!InventoriesHandler.ConsumeComponents(inventories, itemsToRemove, player.Id.SteamId)) return false;


            EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney + this.DistanceReward);
            if (this.ReputationGainOnComplete != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                    this.FactionId, this.ReputationGainOnComplete, ReputationChangeReason.Contract, true);
            }

            inventories.Clear();

            if (this.PlaceItemsInTargetStation)
            {
                var foundCargo = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                    .Where(x => x.BlocksCount > 0).ToList();
                if (foundCargo.Any())
                {
                    foreach (var cargo in foundCargo)
                    {

                        var owner = FacUtils.GetOwner(cargo);
                        var fac = MySession.Static.Factions.TryGetPlayerFaction(owner);

                        if (fac != null && fac.FactionId == this.DeliveryFactionId)
                        {
                            inventories.AddRange(GetStationInventories(cargo));
                        }
                    }

                    InventoriesHandler.SpawnItems(id, this.ItemToDeliver.AmountToDeliver, inventories);
                }
            }

            return true;
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

        public ItemToDeliver ItemToDeliver { get; set; }
        public bool PlaceItemsInTargetStation { get; set; }
        public List<string> CargoNames = new List<string>();
    }
}
