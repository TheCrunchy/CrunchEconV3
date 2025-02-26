using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
using CrunchEconV3.Handlers;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.GameServices;
using VRage.ModAPI;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class TradeOperatorsStationFakerTwo
    {
        public static HashSet<String> TOCFacTags = new HashSet<String>() { "TRADE", "TAG2" };
        public static List<string> BannedItems = new List<string>()
        {
            "MyObjectBuilder_Component/AdminPlate", "MyObjectBuilder_Component/AiEnabled_Comp_CombatBotMaterial",
            "MyObjectBuilder_Component/AiEnabled_Comp_CrewBotMaterial", "MyObjectBuilder_Component/AiEnabled_Comp_RepairBotMaterial",
            "MyObjectBuilder_Component/AiEnabled_Comp_ScavengerBotMaterial", "MyObjectBuilder_Component/EEMPilotSoul", "MyObjectBuilder_Component/MESThrust",
            "MyObjectBuilder_Component/ProprietaryTech", "MyObjectBuilder_Component/PrototechCapacitor", "MyObjectBuilder_Component/PrototechCircuitry", "MyObjectBuilder_Component/PrototechCoolingUnit",
            "MyObjectBuilder_Component/PrototechFrame", "MyObjectBuilder_Component/PrototechMachinery", "MyObjectBuilder_Component/PrototechPanel", "MyObjectBuilder_Component/PrototechPropulsionUnit"
        };

        public static void Patch(PatchContext ctx)
        {
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity is IMyCubeGrid grid)
                {
                    OnEntityAdd(entity);
                }
                return false;
            });

            ctx.GetPattern(insert).Prefixes.Add(insertPatch);
        }

        private static void OnEntityAdd(IMyEntity entity)
        {
            if (entity is MyCubeGrid grid)
            {
                if (!grid.IsStatic)
                {
                    return;
                }
                if (grid.EntityId != grid.GetBiggestGridInGroup().EntityId)
                {
                    return;
                }

                var owner = FacUtils.GetOwner((Sandbox.Game.Entities.MyCubeGrid)grid);
                var faction = FacUtils.GetPlayersFaction(owner);
                if (faction != null && TOCFacTags.Contains(faction.Tag))
                {
                    var stores = grid.GetFatBlocks().OfType<MyStoreBlock>();

                    foreach (var store in stores)
                    {
                        var offersToRemove = new List<MyStoreItem>();
                        foreach (var offer in store.PlayerItems.Where(x => x.StoreItemType == StoreItemTypes.Offer))
                        {
                            if (BannedItems.Contains($"{offer?.Item?.TypeIdString.Replace("MyObjectBuilder_", "")}{offer?.Item?.SubtypeId}"))
                            {
                                Core.Log.Info("Not allowing item");
                                var itemsToRemove = new Dictionary<MyDefinitionId, int>();
                                itemsToRemove.Add(offer.Item.Value, offer.Amount);
                                offersToRemove.Add(offer);
                                InventoriesHandler.ConsumeComponents(InventoriesHandler.GetInventoriesForContract(grid), itemsToRemove, 0l);
                            }
                        }

                        foreach (var removeMe in offersToRemove)
                        {
                            store.RemoveStoreItem(removeMe);
                        }
                    }
                }
            }
        }

        internal static readonly MethodInfo insert =
       typeof(MyStoreBlock).GetMethod("InsertStoreItem", BindingFlags.Instance | BindingFlags.Public) ??
       throw new Exception("Failed to find patch method InsertStoreItem");

        internal static readonly MethodInfo insertPatch =
            typeof(TradeOperatorsStationFakerTwo).GetMethod(nameof(StorePatchMethod), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static Boolean StorePatchMethod(MyStoreBlock __instance, IMyStoreItem item)
        {
            var owner = FacUtils.GetOwner(__instance.CubeGrid);
            var owningFac = FacUtils.GetPlayersFaction(owner);
            if (owningFac == null)
            {
                return true;
            }

            if (TOCFacTags.Contains(owningFac.Tag))
            {
                MyStoreItem storeItem = (MyStoreItem)item;
                if (storeItem.IsCustomStoreItem || storeItem.StoreItemType == StoreItemTypes.Order)
                {
                    return true;
                }

                if (BannedItems.Contains($"{storeItem?.Item?.TypeIdString.Replace("MyObjectBuilder_", "")}{storeItem?.Item?.SubtypeId}"))
                {
                    Core.Log.Info("Not allowing item");
                    var itemsToRemove = new Dictionary<MyDefinitionId, int>();
                    itemsToRemove.Add(storeItem.Item.Value, storeItem.Amount);
                    InventoriesHandler.ConsumeComponents(InventoriesHandler.GetInventoriesForContract(__instance.CubeGrid), itemsToRemove, 0l);

                    return false;
                }

                return true;
            }

            return true;
        }
    }
}
