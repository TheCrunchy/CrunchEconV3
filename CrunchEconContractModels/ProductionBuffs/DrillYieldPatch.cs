using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.Weapons;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRageMath;

namespace CrunchEconContractModels.ProductionBuffs
{
    public static class DrillYieldPatch
    {
        internal static readonly MethodInfo updateHarvest =
            typeof(MyDrillBase).GetMethod("TryHarvestOreMaterial", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method TryHarvestOreMaterial");

        internal static readonly MethodInfo updatePatchHarvest =
            typeof(DrillYieldPatch).GetMethod(nameof(TryHarvestOreMaterial), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method 2 TryHarvestOreMaterial");
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Patching drill yields");
            ctx.GetPattern(updateHarvest).Prefixes.Add(updatePatchHarvest);
        }

        public static double GetBuff(long PlayerId, string ore)
        {
            double buff = 1;
            if (ProductionContractLogger.SetupPlayers.TryGetValue(PlayerId, out var data))
            {
              //  Core.Log.Info($"A1 {ore}");
                if (data.FinishedTypes.TryGetValue($"{ore} Mining Contract", out var minedAmount))
                {
                   // Core.Log.Info("A2");
                    if (ProductionContractLogger.DrillBuffs != null &&
                        ProductionContractLogger.DrillBuffs.Buffs.TryGetValue(ore, out var thresholds))
                    {
                     //   Core.Log.Info("A3");
                        foreach (var item in thresholds)
                        {
                         //   Core.Log.Info("A4");
                            if (minedAmount >= item.Amount)
                            {
                              // Core.Log.Info($"setting buff");
                                buff = item.Buff;
                            }
                        }
                    }
                }

            }
        //    Core.Log.Info($"{buff}");
            return (float)(buff);
        }

        public static void TryHarvestOreMaterial(MyDrillBase __instance,
            MyVoxelMaterialDefinition material,
            Vector3D hitPosition,
            ref int removedAmount,
            bool onlyCheck)
        {
            if (string.IsNullOrEmpty(material.MinedOre))
                return;
            if (onlyCheck)
                return;

            if (__instance.OutputInventory == null || __instance.OutputInventory.Owner == null) return;

            if (__instance.OutputInventory.Owner.GetBaseEntity() == null)
            {
                Core.Log.Info("Drill base entity is null");
                return;
            }

            if (!(__instance.OutputInventory.Owner.GetBaseEntity() is MyShipDrill shipDrill)) return;
            var buff = GetBuff(shipDrill.OwnerId, material.MinedOre);
          //  Core.Log.Info($"{removedAmount * buff}");
         //   Core.Log.Info($"Buffed from {removedAmount}");
            int newAmount = (int)(removedAmount * buff);
            removedAmount = newAmount;
        
         //   Core.Log.Info($"Buffed to {removedAmount}");
            return;
        }

    }
}
