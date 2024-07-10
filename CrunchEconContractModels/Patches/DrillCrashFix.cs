using System;
using System.Reflection;
using CrunchEconV3;
using Sandbox.Game.Weapons;
using Sandbox.Game.Weapons.Guns;
using Sandbox.Game.WorldEnvironment;
using Sandbox.Game.WorldEnvironment.Modules;
using Torch.Managers.PatchManager;

using VRage.Utils;

namespace CrunchEconContractModels.Patches
{
    [PatchShim]
    public static class DrillCrashFix
    {
        private static int patchCount = 0;
        public static void Patch(PatchContext ctx)
        {
      
            patchCount++;
            if (patchCount > 1)
            {
                return;
            }
            Core.Log.Info("Patching button for grid sales");
            ctx.GetPattern(methodToPatch).Prefixes.Add(patchMethod);
        }



        internal static readonly MethodInfo methodToPatch =
            typeof(MyDrillBase).GetMethod("DrillEnvironmentSector",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo patchMethod =
            typeof(DrillCrashFix).GetMethod(nameof(DrillEnvironment), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static bool DrillEnvironment(MyDrillBase __instance, MyDrillSensorBase.DetectionInfo entry,
            float speedMultiplier,
            out MyStringHash targetMaterial)
        {
            targetMaterial = MyStringHash.GetOrCompute("Wood");
            if (__instance.OutputInventory != null && __instance.OutputInventory.Owner is MyShipDrill)
            {
                Core.Log.Info("Prevented a crash by denying tree kill by ship drill");
                return false;
            }
       
            if (patchCount >= 1)
            {
                return true;
            }

            if (entry.Entity == null)
            {
                Core.Log.Info("Entry Entity is null");
                return false;
            }

            if (entry.Entity as MyEnvironmentSector == null)
            {
                Core.Log.Info("Entity as MyEnvironmentSector is null");
                return false;
            }
            MyBreakableEnvironmentProxy module = (entry.Entity as MyEnvironmentSector).GetModule<MyBreakableEnvironmentProxy>();
            if (module == null)
            {
                Core.Log.Info("Module is null");
                return false;
            }

            return true;
        }
    }
}
