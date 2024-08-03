using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
using Sandbox.Definitions;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;

namespace CrunchEconContractModels.ProductionBuffs
{
    [PatchShim]
    public static class RefineryPatch
    {
        internal static readonly MethodInfo update =
        typeof(MyRefinery).GetMethod("ChangeRequirementsToResults", BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo patch =
            typeof(RefineryPatch).GetMethod(nameof(ChangeRequirementsToResults), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static MethodInfo RemoveQueue;

        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Patching refinery");
            ctx.GetPattern(update).Prefixes.Add(patch);
        }

        public static double GetBuff(long PlayerId, MyRefinery Refinery, string ore)
        {
            double buff = 1;
            if (MiningContractLogger.SetupPlayers.TryGetValue(PlayerId, out var data))
            {
                if (data.FinishedTypes.TryGetValue($"{ore} Mining Contract", out var minedAmount))
                {
                    if (MiningContractLogger.OreBuffs != null &&
                        MiningContractLogger.OreBuffs.YieldBuffs.TryGetValue(ore, out var thresholds))
                    {
                        foreach (var item in thresholds)
                        {
                            if (minedAmount >= item.Amount)
                            {
                                buff = item.Buff;
                            }
                        }
                    }
                }
        
            }

  
            return (float)(buff);
        }

        public static double GetSpeedBuff(long PlayerId, MyRefinery Refinery, string ore)
        {
            double buff = 1;
            if (MiningContractLogger.SetupPlayers.TryGetValue(PlayerId, out var data))
            {
                if (data.FinishedTypes.TryGetValue($"{ore} Mining Contract", out var minedAmount))
                {
                    if (MiningContractLogger.OreBuffs != null &&
                        MiningContractLogger.OreBuffs.SpeedBuffs.TryGetValue(ore, out var thresholds))
                    {
                        foreach (var item in thresholds)
                        {
                            if (minedAmount >= item.Amount)
                            {
                                buff = item.Buff;
                            }
                        }
                    }
                }

            }
            return (buff);
        }

        public static Boolean ChangeRequirementsToResults(MyBlueprintDefinitionBase queueItem, MyFixedPoint blueprintAmount, MyRefinery __instance)
        {
            if (__instance.BlockDefinition as MyRefineryDefinition == null)
            {
                return false;
            }
            
            if (!Sync.IsServer || MySession.Static == null || (queueItem == null || queueItem.Prerequisites == null) || (__instance.OutputInventory == null || __instance.InputInventory == null || (queueItem.Results == null)))
                return false;
            if (!MySession.Static.CreativeMode)
                blueprintAmount = MyFixedPoint.Min(__instance.OutputInventory.ComputeAmountThatFits(queueItem), blueprintAmount);
            if (blueprintAmount == (MyFixedPoint)0)
                return false;

            var input = queueItem.Prerequisites.FirstOrDefault().Id.SubtypeId.ToString();
 
            var speedBuff = GetSpeedBuff(__instance.OwnerId, __instance, input);
            double buff = GetBuff(__instance.OwnerId, __instance, input);

            blueprintAmount *= (MyFixedPoint)speedBuff;

            foreach (var prerequisite in queueItem.Prerequisites)
            {
                if ((!(MyObjectBuilderSerializer.CreateNewObject((SerializableDefinitionId)prerequisite.Id) is
                        MyObjectBuilder_PhysicalObject newObject))) continue;

                __instance.InputInventory.RemoveItemsOfType((MyFixedPoint)((float)blueprintAmount * (float)prerequisite.Amount), newObject, false, false);
                var itemAmount = __instance.InputInventory.GetItemAmount(prerequisite.Id, MyItemFlags.None, false);
                if (itemAmount < (MyFixedPoint)0.01f)
                    __instance.InputInventory.RemoveItemsOfType(itemAmount, prerequisite.Id, MyItemFlags.None, false);
            }
            foreach (var result in queueItem.Results)
            {
                if ((!(MyObjectBuilderSerializer.CreateNewObject((SerializableDefinitionId)result.Id) is
                        MyObjectBuilder_PhysicalObject newObject))) continue;

                var def = __instance.BlockDefinition as MyRefineryDefinition;
                var num = (float)result.Amount * def.MaterialEfficiency * __instance.UpgradeValues["Effectiveness"];
                __instance.OutputInventory.AddItems((MyFixedPoint)((float)blueprintAmount * num * buff), (MyObjectBuilder_Base)newObject);
            }

            if (RemoveQueue == null)
            {
                Type change = __instance.GetType().Assembly.GetType("Sandbox.Game.Entities.Cube.MyProductionBlock");
                RemoveQueue = change.GetMethod("RemoveFirstQueueItemAnnounce", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            var MethodInput = new object[] { blueprintAmount, 0.0f };
            RemoveQueue?.Invoke(__instance, MethodInput);

            return false;
        }
    }
}
