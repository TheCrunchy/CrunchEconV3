using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
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
    public static class PCULimits
    {
        public static List<string> LimitedBlocks = new List<string>();

        public class ShipLimits : ICloneable
        {
            public Dictionary<string, BlockGroupLimit> PairNameToLimits { get; set; } = new Dictionary<string, BlockGroupLimit>();

            public bool CheckCanAddBlock(MyCubeBlockDefinition definition)
            {

                if (PairNameToLimits.TryGetValue(definition.BlockPairName, out var limited))
                {
                    return limited.AddBlock(definition);
                }

                return true;
            }
            public int MaximumPCU { get; set; }
            public string BeaconPairName { get; set; }

            public object Clone()
            {
                ShipLimits clonedLimits = new ShipLimits();
                clonedLimits.MaximumPCU = this.MaximumPCU;
                clonedLimits.BeaconPairName = this.BeaconPairName;

                // Clone PairNameToLimits dictionary and its contents
                foreach (var pair in this.PairNameToLimits)
                {
                    clonedLimits.PairNameToLimits.Add(pair.Key, (BlockGroupLimit)pair.Value.Clone());
                }

                return clonedLimits;
            }
        }

        public class BlockGroupLimit : ICloneable
        {
            public List<string> AllowedBlocks = new List<string>();
            public int MaximumPCUForLimits = 5000;
            public int UsedPCU = 0;

            public bool AddBlock(MyCubeBlockDefinition definition)
            {
                if (UsedPCU + definition.PCU > MaximumPCUForLimits)
                {
                    return false;
                }

                UsedPCU += definition.PCU;
                return true;
            }

            public object Clone()
            {
                return new BlockGroupLimit
                {
                    AllowedBlocks = new List<string>(this.AllowedBlocks),
                    MaximumPCUForLimits = this.MaximumPCUForLimits,
                    UsedPCU = this.UsedPCU
                };
            }
        }

        public static Dictionary<string, int> LimitsByBeaconPairName { get; set; }
        public static Dictionary<string, ShipLimits> LimitDefinitions { get; set; }
        public static Dictionary<long, ShipLimits> GridsClass { get; set; }


        [PatchShim]
        public static class BuildBlockPatch
        {
            public static void Patch(PatchContext ctx)
            {
                MethodInfo method = typeof(MyCubeGrid).GetMethod("BuildBlocksRequest", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                ctx.GetPattern((MethodBase)method).Prefixes.Add(typeof(BuildBlockPatch).GetMethod("BuildBlocksRequest", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic));
                ctx.GetPattern((MethodBase)typeof(MyShipMergeBlock).GetMethod("UpdateBeforeSimulation10", BindingFlags.Instance | BindingFlags.Public)).Prefixes.Add(typeof(PCULimits).GetMethod("MergeCheck", BindingFlags.Static | BindingFlags.NonPublic));
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


                var shipClass = GetGridsClass(__instance, grids);
                if (shipClass == null)
                {
                    return true;
                }
                var limit = shipClass.MaximumPCU;
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

            private static ShipLimits GetGridsClass(MyCubeGrid __instance, List<IMyCubeGrid> grids)
            {
                if (GridsClass.TryGetValue(__instance.GetBiggestGridInGroup().EntityId, out var foundClass))
                {
                    return foundClass;
                }
                else
                {
                    var beacons = grids.Cast<MyCubeGrid>().SelectMany(x => x.GetFatBlocks().OfType<MyBeacon>())
                        .Select(x => x.BlockDefinition.BlockPairName).Distinct();
                    var shipClass = GetMaxLimitByBeaconPairName(beacons);
                    if (LimitDefinitions.TryGetValue(shipClass.Key, out var shipDefinition))
                    {
                        var newClass = (ShipLimits)shipDefinition.Clone();
                        GridsClass[__instance.GetBiggestGridInGroup().EntityId] = newClass;

                        return newClass;
                    }

                    return null;

                    //find the class
                }

            }

            private static bool MergeCheck(MyShipMergeBlock __instance)
            {
                if (__instance?.Other == null || __instance.IsLocked || (!__instance.IsFunctional || !__instance.Other.IsFunctional))
                    return true;
                var grids = new List<IMyCubeGrid>();

                __instance.CubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(grids);

                var shipClass = GetGridsClass(__instance.CubeGrid, grids);
                if (shipClass == null)
                {
                    return true;
                }

                var limit = shipClass.MaximumPCU;

                var pcu = grids.Cast<MyCubeGrid>().Sum(Grid => Grid.BlocksPCU);

                var targetGrids = new List<IMyCubeGrid>();

                __instance.Other.CubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(targetGrids);
                var targetpcu = grids.Cast<MyCubeGrid>().Sum(Grid => Grid.BlocksPCU);

                if (pcu > limit || pcu + targetpcu > limit)
                {
                    __instance.Enabled = false;
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

