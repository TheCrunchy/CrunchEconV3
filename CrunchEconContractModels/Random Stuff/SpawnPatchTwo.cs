using System;
using SpaceEngineers.Game.World;
using System.Reflection;
using Sandbox.Game.SessionComponents;
using Torch.Managers.PatchManager;
using VRageMath;

namespace HardcodedRespawn
{
    [PatchShim]
    public static class SpawnPatchTwo
    {
        internal static readonly MethodInfo flee =
            typeof(MySpaceRespawnComponent).GetMethod("GetSpawnPositionNearPlanet",
                BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo patchFlee =
            typeof(SpawnPatchTwo).GetMethod(nameof(GetSpawnPositionNearPlanet),
                BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(flee).Suffixes.Add(patchFlee);
        }

        public static void GetSpawnPositionNearPlanet(
            SpawnInfo info,
            ref Vector3D position,
            ref Vector3 forward,
            ref Vector3 up)
        {
            position = new Vector3D(1198529.39, -1187423, 1264082.45);
            forward = new Vector3(0, 0, 0);
            up = new Vector3(0, 0, 0);

        }
    }
}