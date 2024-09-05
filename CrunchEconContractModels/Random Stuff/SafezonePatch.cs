using CrunchEconV3;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class SafezonePatch
    {
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Patching safezone");
            ctx.GetPattern(buttonMethod).Prefixes.Add(buttonPatch);
        }

        internal static readonly MethodInfo buttonMethod =
            typeof(MySafeZone).GetMethod("UpdateBeforeSimulation", BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo buttonPatch =
            typeof(SafezonePatch).GetMethod(nameof(UpdateOnceBeforeFrame), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static void UpdateOnceBeforeFrame(MySafeZone __instance)
        {
            if (__instance.AllowedActions.HasFlag(VRage.Game.ObjectBuilders.Components.MySafeZoneAction.Shooting))
            {
                Core.Log.Info("removing shooting action");
                var builder = __instance.GetObjectBuilder() as MyObjectBuilder_SafeZone;
                var actionsWithout = builder.AllowedActions;
                actionsWithout &= ~VRage.Game.ObjectBuilders.Components.MySafeZoneAction.Shooting;

                builder.AllowedActions = actionsWithout;
                MySessionComponentSafeZones.UpdateSafeZone(builder, true);
            }
        }
    }
}
