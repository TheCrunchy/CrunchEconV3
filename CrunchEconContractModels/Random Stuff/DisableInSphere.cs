using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
using CrunchEconV3.Utils;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using VRage.Audio;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRageMath;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class DisableInSphere
    {
        public static void Patch(PatchContext ctx)
        {
            MethodInfo method = typeof(MyCubeGrid).GetMethod("BuildBlocksRequest",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            ctx.GetPattern(method).Prefixes.Add(typeof(DisableInSphere).GetMethod("BuildBlocksRequest", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic));
        }

        private static Dictionary<Vector3, int> Positions = new Dictionary<Vector3, int>()
        {
            { new Vector3(0, 0, 0), 50000 },
            { new Vector3(10, 10, 10), 50000 },
        };

        private static bool BuildBlocksRequest(MyCubeGrid __instance, HashSet<MyCubeGrid.MyBlockLocation> locations)
        {
            if (__instance == null || !locations.Any())
                return true;

            var definition = MyDefinitionManager.Static.GetCubeBlockDefinition(locations.First().BlockDefinition);
            if (definition == null)
                return true;

            var grids = new List<IMyCubeGrid>();
          

            foreach (var Position in Positions)
            {
                var distance = Vector3D.Distance(__instance.PositionComp.GetPosition(), Position.Key);
                if (distance <= Position.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }

}