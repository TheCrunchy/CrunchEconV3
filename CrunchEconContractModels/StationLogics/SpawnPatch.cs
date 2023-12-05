using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.SessionComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace CrunchEconContractModels.StationLogics
{
    [PatchShim]
    public static class SpawnPatch
    {
        internal static readonly MethodInfo flee =
            typeof(MySpaceRespawnComponent).GetMethod("GetSpawnPositionInSpace",
                BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo patchFlee =
            typeof(SpawnPatch).GetMethod(nameof(GetSpawnPositionInSpace),
                BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static void Patch(PatchContext ctx)
        {

            ctx.GetPattern(flee).Suffixes.Add(patchFlee);
        }

        public static void GetSpawnPositionInSpace(
            SpawnInfo info,
            ref Vector3D position,
            ref Vector3 forward,
            ref Vector3 up)
        {
            Vector3D vector3D1 = new Vector3D(0, 0, 0);
            forward = Vector3.Forward;
            up = Vector3.CalculatePerpendicularVector(forward);
            position = Sandbox.Game.Entities.MyEntities.FindFreePlace(vector3D1, 5000, 20, 5, 1f) ?? vector3D1;
            Core.Log.Info("Test");
        }
    }
}

