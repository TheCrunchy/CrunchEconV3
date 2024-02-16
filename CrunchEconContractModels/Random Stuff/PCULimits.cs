using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
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
    public static class PCULimits
    {

        public static Dictionary<string, int> LimitsByBeaconPairName { get; set; }
        public static Dictionary<long, string> GridsClass { get; set; }
        [PatchShim]
        public static class BuildBlockPatch
        {
            public static void Patch(PatchContext ctx)
            {
                MethodInfo method = typeof(MyCubeGrid).GetMethod("BuildBlocksRequest", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                ctx.GetPattern((MethodBase)method).Prefixes.Add(typeof(BuildBlockPatch).GetMethod("BuildBlocksRequest", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic));

                LimitsByBeaconPairName.Clear();
                LimitsByBeaconPairName.Add("LargeBlockBeacon", 50000);
                LimitsByBeaconPairName.Add("PairName2", 50000);
                LimitsByBeaconPairName.Add("PairName3", 50000);

            }

            private static bool BuildBlocksRequest(
                MyCubeGrid __instance,
                HashSet<MyCubeGrid.MyBlockLocation> locations)
            {
                if (__instance == null)
                {
                    return true;
                }

                var definition = MyDefinitionManager.Static.GetCubeBlockDefinition(
                    (MyDefinitionId)locations.FirstOrDefault<MyCubeGrid.MyBlockLocation>().BlockDefinition);
                if (definition == null)
                {
                    return true;
                }
                var grids = new List<IMyCubeGrid>();
                __instance.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(grids);

                var limit = 0;
                if (GridsClass.TryGetValue(__instance.GetBiggestGridInGroup().EntityId, out var foundClass))
                {
                    limit = LimitsByBeaconPairName[foundClass];
                }
                else
                {
                    var beacons = grids.Cast<MyCubeGrid>().SelectMany(x => x.GetFatBlocks().OfType<MyBeacon>()).Select(x => x.BlockDefinition.BlockPairName).Distinct();
                    var shipClass = GetMaxLimitByBeaconPairName(beacons);
                    GridsClass[__instance.GetBiggestGridInGroup().EntityId] = shipClass.Key;
                    limit = shipClass.Value;
                    //find the class
                }

                ulong steamId = MyEventContext.Current.Sender.Value;
                if (MySession.Static.Players.TryGetPlayerBySteamId(steamId, out var player))
                {

                    var pcu = grids.Cast<MyCubeGrid>().Sum(Grid => Grid.BlocksPCU);

                    if (pcu > limit || pcu + definition.PCU > limit)
                    {
                        Core.SendMessage("PCU Limit", $"Grid PCU Limit reached. Limit of {limit:##,###}", Color.Red, steamId);
                    }
                    return false;
                }

                return true;
            }

            public static KeyValuePair<string, int> GetMaxLimitByBeaconPairName(IEnumerable<string> beacons)
            {
                KeyValuePair<string, int> maxLimitPair = default(KeyValuePair<string, int>);
                int maxLimit = int.MinValue;

                foreach (var beacon in beacons)
                {
                    int limit;
                    if (LimitsByBeaconPairName.TryGetValue(beacon, out limit) && limit > maxLimit)
                    {
                        maxLimit = limit;
                        maxLimitPair = new KeyValuePair<string, int>(beacon, limit);
                    }
                }

                return maxLimitPair;
            }
        }
    }
}

