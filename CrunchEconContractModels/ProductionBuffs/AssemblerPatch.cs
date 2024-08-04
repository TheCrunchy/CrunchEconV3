using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Definitions;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.ProductionBuffs
{
    [PatchShim]
    public static class AssemblerPatch
    {
        internal static readonly MethodInfo CalculateBlueprintProductionTimeMethod =
            typeof(MyAssembler).GetMethod("CalculateBlueprintProductionTime", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find CalculateBlueprintProductionTime method");

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(CalculateBlueprintProductionTimeMethod).Prefixes.Add(typeof(AssemblerPatch).GetMethod(nameof(PrefixCalculateBlueprintProductionTime), BindingFlags.Static | BindingFlags.NonPublic));
        }

        private static float GetBuff(long playerId)
        {
            double buff = 1;
            if (ProductionContractLogger.SetupPlayers.TryGetValue(playerId, out var data))
            {
                //  Core.Log.Info($"A1 {ore}");
                if (data.FinishedTypes.TryGetValue($"Item Delivery", out var minedAmount))
                {
                    // Core.Log.Info("A2");
                    if (ProductionContractLogger.AssemblerBuffs != null &&
                        ProductionContractLogger.AssemblerBuffs.Buffs.TryGetValue("Item Delivery", out var thresholds))
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
            return (float)buff;
        }

        private static bool PrefixCalculateBlueprintProductionTime(MyAssembler __instance, MyBlueprintDefinitionBase currentBlueprint, ref float __result)
        {
            var buff = GetBuff(__instance.OwnerId);
         //   Core.Log.Info($"{__result}");
            var speed = (((MyAssemblerDefinition)__instance.BlockDefinition).AssemblySpeed + __instance.UpgradeValues["Productivity"]) * buff;
            __result = (float)Math.Round(currentBlueprint.BaseProductionTimeInSeconds * 1000.0 / (MySession.Static.AssemblerSpeedMultiplier * speed));
        //    Core.Log.Info($"{__result}");
            return false; // Skip the original method
        }
    }
}
