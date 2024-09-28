﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Handlers;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using EmptyKeys.UserInterface.Generated.PlayerTradeView_Bindings;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.ObjectBuilders;
using VRage.ObjectBuilders.Private;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.PlugAndPlay.Contracts
{
    public class MiningContractImplementation : ContractAbstract
    {
        public override MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"You must go mine {this.AmountToMine - this.MinedOreAmount:##,###} {this.OreSubTypeName} using a ship drill, then return here.";
            return BuildUnassignedContract(contractDescription);
        }
        public override string GetStatus()
        {
            return $"{this.Name} - {this.OreSubTypeName} {this.MinedOreAmount}/{this.AmountToMine}";
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
            if (current.Count >= 3)
            {
                return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
            }

            if (this.CollateralToTake > 0)
            {
                EconUtils.takeMoney(identityId, this.CollateralToTake);
            }
            this.AssignedPlayerIdentityId = identityId;
            this.AssignedPlayerSteamId = playerData.PlayerSteamId;
            return Tuple.Create(true, MyContractResults.Success);
        }
        public override void SendDeliveryGPS()
        {
            if (!ReadyToDeliver)
            {
                return;
            }
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
            gpsRef.DiscardAt = new TimeSpan?();
            gpsRef.Description = sb.ToString();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
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

        public override bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            try
            {
                if (MinedOreAmount < AmountToMine) return false;
                if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                    return false;
                float distance = Vector3.Distance(this.DeliverLocation, (Vector3)currentPosition);
                if (!(distance <= 500)) return false;
                Dictionary<MyDefinitionId, int> itemsToRemove = new Dictionary<MyDefinitionId, int>();
                var parseThis = "MyObjectBuilder_Ore/" + this.OreSubTypeName;
                if (MyDefinitionId.TryParse(parseThis, out MyDefinitionId id))
                {
                    itemsToRemove.Add(id, this.AmountToMine);
                }
                var sphere = new BoundingSphereD(this.DeliverLocation, 1000 * 2);
                var playersGrids = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                    .Where(x => x.BlocksCount > 0 && FacUtils.IsOwnerOrFactionOwned(x, this.AssignedPlayerIdentityId, true)).ToList();
                List<VRage.Game.ModAPI.IMyInventory> inventories = new List<IMyInventory>();
                foreach (var grid in playersGrids)
                {
                    inventories.AddRange(InventoriesHandler.GetInventoriesForContract(grid));
                }

                if (!InventoriesHandler.ConsumeComponents(inventories, itemsToRemove, player.Id.SteamId)) return false;

                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);
                if (this.ReputationGainOnComplete != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        this.FactionId, this.ReputationGainOnComplete, ReputationChangeReason.Contract, true);
                }
                inventories.Clear();
                if (this.SpawnOreInStation)
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
                                inventories.AddRange(InventoriesHandler.GetInventoriesForContract(cargo));
                            }
                        }

                        InventoriesHandler.SpawnItems(id, this.AmountToMine, inventories);
                    }
                }
            }
            catch (Exception e)
            {
                CrunchEconV3.Core.Log.Error($"Mining try complete error {e}");
                return true;
            }
            return true;

        }
        public String OreSubTypeName { get; set; }
        public int MinedOreAmount { get; set; }
        public int AmountToMine { get; set; }
        public bool SpawnOreInStation { get; set; }
    }

    [PatchShim]
    public static class DrillPatch
    {

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(update).Suffixes.Add(updatePatch);
        }

        public static Dictionary<ulong, DateTime> messageCooldown = new Dictionary<ulong, DateTime>();

        internal static readonly MethodInfo update =
            typeof(MyDrillBase).GetMethod("OnDrillResults", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

    
        internal static readonly MethodInfo updatePatch =
            typeof(DrillPatch).GetMethod(nameof(PatchResults), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");
       
        public static Type drill = null;
        public static void PatchResults(MyDrillBase __instance,
            Dictionary<MyVoxelMaterialDefinition, int> materials,
            Vector3D hitPosition,
            bool collectOre,
            Action<bool> OnDrillingPerformed = null)
        {
            if (Core.CompileFailed)
            {
                return;
            }
            if (!collectOre)
            {
                return;
            }

            if (materials == null)
            {
                Core.Log.Info("Materials are null");
                return;
            }

            if (__instance.OutputInventory == null || __instance.OutputInventory.Owner == null) return;

            if (__instance.OutputInventory.Owner.GetBaseEntity() == null)
            {
                Core.Log.Info("Drill base entity is null");
                return;
            }

            if (!(__instance.OutputInventory.Owner.GetBaseEntity() is MyShipDrill shipDrill)) return;
            if (drill == null)
            {
                drill = __instance.GetType();
            }

            if (drill == null)
            {
                Core.Log.Info("Drill reflection still null");
                return;
            }

            var owner = shipDrill.OwnerId;
            var data = Core.PlayerStorage.GetData(MySession.Static.Players.TryGetSteamId(owner));
            if (data == null)
            {
                Core.Log.Info("player data null");
                return;
            }
            var name = "MiningContractImplementation";
            if (data != null && data.PlayersContracts.All(x => !x.Value.ContractType.Equals(name)))
            {
                return;
            }

            Dictionary<string, int> MinedAmount = new Dictionary<string, int>();
            foreach (var material in materials)
            {
                if (material.Key == null)
                {
                    Core.Log.Info("material key null");
                    continue;
                }
                if (string.IsNullOrEmpty(material.Key.MinedOre))
                    continue;

                if (material.Value <= 0)
                {
                    continue;
                }
                MyObjectBuilder_Ore newObject = MyObjectBuilderSerializerKeen.CreateNewObject<MyObjectBuilder_Ore>(material.Key.MinedOre);
                newObject.MaterialTypeName = new MyStringHash?(material.Key.Id.SubtypeId);
                float num = (float)(material.Value / (double)byte.MaxValue * 1.0) * __instance.VoxelHarvestRatio * material.Key.MinedOreRatio;
                if (!MySession.Static.AmountMined.ContainsKey(material.Key.MinedOre))
                    MySession.Static.AmountMined[material.Key.MinedOre] = (MyFixedPoint)0;
                MySession.Static.AmountMined[material.Key.MinedOre] += (MyFixedPoint)num;
                MyPhysicalItemDefinition physicalItemDefinition = MyDefinitionManager.Static.GetPhysicalItemDefinition((MyObjectBuilder_Base)newObject);
                MyFixedPoint amountItems1 = (MyFixedPoint)(num / physicalItemDefinition.Volume);
                MyFixedPoint maxAmountPerDrop = (MyFixedPoint)(float)(0.150000005960464 / (double)physicalItemDefinition.Volume);

                MyFixedPoint collectionRatio = (MyFixedPoint)drill.GetField("m_inventoryCollectionRatio", BindingFlags.NonPublic | BindingFlags.Instance).GetValue((object)__instance);
                MyFixedPoint b = amountItems1 * ((MyFixedPoint)1 - collectionRatio);
                MyFixedPoint amountItems2 = MyFixedPoint.Min(maxAmountPerDrop * 10 - (MyFixedPoint)0.001, b);
                MyFixedPoint totalAmount = amountItems1 * collectionRatio - amountItems2;
                if (totalAmount > 0)
                {
                    if (MinedAmount.ContainsKey(material.Key.MinedOre))
                    {
                        MinedAmount[material.Key.MinedOre] += totalAmount.ToIntSafe();
                    }
                    else
                    {
                        MinedAmount.Add(material.Key.MinedOre, totalAmount.ToIntSafe());
                    }
                }
            }

            foreach (var mined in MinedAmount)
            {
                var contracts = data.PlayersContracts.Where(x => x.Value != null && x.Value.ContractType == name);
                foreach (var contract in contracts)
                {
                    var mining = contract.Value as MiningContractImplementation;
                    if (mining == null)
                    {
                        Core.Log.Info("Mining contract was null? the fuck");
                        continue;
                    }
                    if (mining.OreSubTypeName != mined.Key) continue;
                    if (mining.MinedOreAmount >= mining.AmountToMine) continue;
                    mining.MinedOreAmount += mined.Value;
                    data.PlayersContracts[mining.ContractId] = mining;
                    if (mining.MinedOreAmount >= mining.AmountToMine)
                    {
                        mining.ReadyToDeliver = true;
                        mining.SendDeliveryGPS();
                        CrunchEconV3.Core.SendMessage("Contracts",
                            "Contract Ready to be completed, Deliver " + $"{mining.AmountToMine:##,###}" + " " + mining.OreSubTypeName +
                            " to the delivery GPS.", Color.Gold,
                            MySession.Static.Players.TryGetSteamId(owner));
                        messageCooldown.Remove(MySession.Static.Players.TryGetSteamId(owner));
                        messageCooldown.Add(MySession.Static.Players.TryGetSteamId(owner),

                            DateTime.Now.AddSeconds(0.5));
                        Task.Run(async () =>
                        {
                            CrunchEconV3.Core.PlayerStorage.Save(data);
                        });
                        return;
                    }
                    if (messageCooldown.TryGetValue(MySession.Static.Players.TryGetSteamId(owner),
                            out DateTime time))
                    {
                        if (DateTime.Now < time) return;
                        CrunchEconV3.Core.SendMessage("Contracts",
                            "Progress: " + mined.Key + " " +
                            $"{mining.MinedOreAmount:##,###}" + " / " +
                            $"{mining.AmountToMine:##,###}", Color.Gold,
                            MySession.Static.Players.TryGetSteamId(owner));
                        messageCooldown[MySession.Static.Players.TryGetSteamId(owner)] =
                            DateTime.Now.AddSeconds(0.5);


                    }
                    else
                    {
                        CrunchEconV3.Core.SendMessage("Contracts",
                            "Progress: " + mined.Key + " " + $"{mining.MinedOreAmount:##,###}" + " / " + $"{mining.AmountToMine:##,###}", Color.Gold,
                            MySession.Static.Players.TryGetSteamId(owner));
                        messageCooldown.Add(MySession.Static.Players.TryGetSteamId(owner),
                            DateTime.Now.AddSeconds(0.5));
                    }

                    Task.Run(async () =>
                    {
                        CrunchEconV3.Core.PlayerStorage.Save(data);
                    });

                    return;
                }
            }

        }
    }


}
