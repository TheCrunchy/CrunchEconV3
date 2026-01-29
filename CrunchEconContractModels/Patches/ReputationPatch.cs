using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Patches
{
    public static class ReputationPatch
    {
        private static int patchCount = 0;
        public static void Patch(PatchContext ctx)
        {

            patchCount++;
            if (patchCount > 1)
            {
                return;
            }

            Core.Log.Info("Patching rep damage");
            ctx.GetPattern(update).Prefixes.Add(updatePatch);
        }

        internal static readonly MethodInfo update =
        typeof(MyFactionCollection).GetMethod("DamageFactionPlayerReputation", BindingFlags.Instance | BindingFlags.Public) ??
        throw new Exception("Failed to find patch method");
        internal static readonly MethodInfo updatePatch =
                typeof(ReputationPatch).GetMethod(nameof(DamageFactionPlayerReputation), BindingFlags.Static | BindingFlags.Public) ??
                throw new Exception("Failed to find patch method");

        public static Boolean DamageFactionPlayerReputation(
            long playerIdentityId,
            long attackedIdentityId,
            MyReputationDamageType repDamageType, float damageAmount = 0.0f)
        {
            var identity = MySession.Static.Players.TryGetIdentity(playerIdentityId);

            var player = identity.Character;
            if (player != null)
            {
                foreach (var safezone in MySessionComponentSafeZones.SafeZones)
                    if (MySessionComponentSafeZones.IsInSafezone(player.EntityId,
                            safezone))
                    {
                        return false;
                    }
            }


            return true;
        }
    }
}
