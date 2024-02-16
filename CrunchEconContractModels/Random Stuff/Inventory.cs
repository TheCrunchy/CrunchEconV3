using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using NLog.Fluent;
using Sandbox.Game;
using VRage;
using VRage.Game;

namespace CrunchEconContractModels.Random_Stuff
{
    using System.Reflection;
    using Torch.Managers.PatchManager;

    [PatchShim]
    public static class Inventory
    {
        public static MethodInfo TargetMethod = typeof(MyInventory).GetMethod("GetItemAmount", new[] { typeof(MyDefinitionId), typeof(MyItemFlags), typeof(bool) });

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(TargetMethod).Prefixes.Add(typeof(Inventory).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic));
        }

        private static bool Prefix(ref MyFixedPoint __result, MyInventory __instance, MyDefinitionId contentId, MyItemFlags flags = MyItemFlags.None, bool substitute = false)
        {
            __result = (MyFixedPoint)0;
            Core.Log.Info($"{contentId.TypeId}/{contentId.SubtypeId}");
            Core.Log.Info($"inventory at {__instance.Owner.PositionComp.GetPosition()}");
            return true; // Returning false to ensure original method execution continues
        }
    }

}
