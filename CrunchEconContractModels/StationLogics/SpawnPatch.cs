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
using Sandbox.Game.GameSystems;
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
            forward = Vector3.Forward;
            up = Vector3.CalculatePerpendicularVector(forward);
            var amountOfVectors = 50;
            var max = 500;
            List<Vector3> randomVectors = GenerateRandomVectors(amountOfVectors, max);

            foreach (var item in randomVectors)
            {
                var tempposition = Sandbox.Game.Entities.MyEntities.FindFreePlace(item, 2000, 20, 5, 1f);
                if (tempposition != null)
                {
                    position = tempposition.Value;
                    return;
                }
            }
        }


        static List<Vector3> GenerateRandomVectors(int numberOfVectors, float maxDistance)
        {
            List<Vector3> vectors = new List<Vector3>();

            for (int i = 0; i < numberOfVectors; i++)
            {
                float x = (float)(Core.random.NextDouble() - 0.5) * 2 * maxDistance;
                float y = (float)(Core.random.NextDouble() - 0.5) * 2 * maxDistance;
                float z = (float)(Core.random.NextDouble() - 0.5) * 2 * maxDistance;

                Vector3 vector = new Vector3(x, y, z);
                if (!MyGravityProviderSystem.IsPositionInNaturalGravity(vector))
                {
                    vectors.Add(vector);
                }
               
            }

            return vectors;
        }
    }
}

