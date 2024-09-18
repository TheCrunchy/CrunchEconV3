using CrunchEconV3;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Engine.Physics;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class RigidBodyPatch
    {
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Patching motion dynamic");
            ctx.GetPattern(buttonMethod).Prefixes.Add(buttonPatch);
        }

        internal static readonly MethodInfo buttonMethod =
            typeof(MyPhysicsBody).GetMethod("OnMotionDynamic", BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method OnMotionDynamic");

        internal static readonly MethodInfo buttonPatch =
            typeof(RigidBodyPatch).GetMethod(nameof(OnMotionPatch), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static bool OnMotionPatch(MyPhysicsBody __instance)
        {
            var body = __instance.RigidBody;
            if (body == null)
            {
                Core.Log.Error("Preventing a crash?");
                return false;
            }

            return true;
        }
    }
}
