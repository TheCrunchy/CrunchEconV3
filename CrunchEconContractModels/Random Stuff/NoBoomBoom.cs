using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.Entities;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class NoBoomBoom
    {
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(detonateMethod).Prefixes.Add(detonatePatch);
        }

        internal static readonly MethodInfo detonateMethod =
            typeof(MyCubeBlock).GetMethod("CalculateStoredExplosiveDamage",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo detonatePatch =
            typeof(NoBoomBoom).GetMethod(nameof(NoBoom), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static bool NoBoom(MyCubeBlock __instance)
        {
            return false;
        }
    }
}
