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

        public class ShipLimits : ICloneable
        {
            public Dictionary<string, BlockGroupLimit> PairNameToLimits { get; set; } = new Dictionary<string, BlockGroupLimit>();

            public (bool, BlockGroupLimit) CheckCanAddBlock(MyCubeBlockDefinition definition)
            {

                Core.Log.Info(PairNameToLimits.Count);
                if (PairNameToLimits.TryGetValue(definition.BlockPairName, out var limited))
                {
                    Core.Log.Info("Checking if pairnames contains it");
                    return limited.AddBlock(definition);
                }
                else
                {
                    Core.Log.Info("Checking if limited blocks contains it");
                    if (BuildBlockPatch.LimitedBlocks.Contains(definition.BlockPairName))
                    {
                        return (false, null);
                    }
                }

                Core.Log.Info("Returning true");
                return (true, null);
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

            public void Setup(MyCubeGrid cubeGrid)
            {
                List<MyCubeBlock>
                    BlocksToRemove = new List<MyCubeBlock>();
                foreach (var block in cubeGrid.GetFatBlocks())
                {
                    var (canAdd, blockGroupLimit) = CheckCanAddBlock(block.BlockDefinition);
                    if (!canAdd)
                    {
                        BlocksToRemove.Add(block);
                    }
                }

                foreach (var block in BlocksToRemove)
                {
                    cubeGrid.RemoveBlock(block.SlimBlock);
                }
            }

        }

        public class BlockGroupLimit : ICloneable
        {
            public List<string> AllowedBlocks = new List<string>();
            public int MaximumPCUForLimits = 5000;
            public int UsedPCU = 0;

            public (bool, BlockGroupLimit) AddBlock(MyCubeBlockDefinition definition)
            {
                if (UsedPCU + definition.PCU > MaximumPCUForLimits)
                {
                    return (false, this);
                }

                UsedPCU += definition.PCU;
                return (true, this);
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



        [PatchShim]
        public static class BuildBlockPatch
        {
            public static List<string> LimitedBlocks = new List<string>();

            public static Dictionary<string, int> LimitsByBeaconPairName { get; set; } = new Dictionary<string, int>();

            public static Dictionary<string, ShipLimits> LimitDefinitions { get; set; } =
                new Dictionary<string, ShipLimits>();

            public static Dictionary<long, ShipLimits> GridsClass { get; set; } = new Dictionary<long, ShipLimits>();


            public static void Patch(PatchContext ctx)
            {

                LimitsByBeaconPairName.Clear();
                LimitsByBeaconPairName.Add("Beacon", 35000);
                LimitsByBeaconPairName.Add("PairName2", 50000);
                LimitsByBeaconPairName.Add("PairName3", 50000);

                var limit = new BlockGroupLimit()
                {
                    AllowedBlocks = new List<string>() { "CWIS","C30mmRevolver", "C30mmSingleT", "VXM-08 Multi Launch Missile System" },
                    MaximumPCUForLimits = 2000,
                    UsedPCU = 0
                };
                var gridLimit = new ShipLimits()
                {
                    MaximumPCU = 35000,
                    BeaconPairName = "Beacon",
                    PairNameToLimits = new Dictionary<string, BlockGroupLimit>()

                };
                foreach (var block in limit.AllowedBlocks)
                {
                    gridLimit.PairNameToLimits.Add(block, limit);
                }

                LimitDefinitions.Add("Beacon", gridLimit);
                MethodInfo method = typeof(MyCubeGrid).GetMethod("BuildBlocksRequest", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                ctx.GetPattern((MethodBase)method).Prefixes.Add(typeof(BuildBlockPatch).GetMethod("BuildBlocksRequest", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic));
                // ctx.GetPattern((MethodBase)typeof(MyShipMergeBlock).GetMethod("UpdateBeforeSimulation10", BindingFlags.Instance | BindingFlags.Public)).Prefixes.Add(typeof(PCULimits).GetMethod("MergeCheck", BindingFlags.Static | BindingFlags.NonPublic));
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
                    Core.Log.Info("Ship class be null");
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
                        return false;
                    }
           
                }

                var addResult = shipClass.CheckCanAddBlock(definition);
                if (!addResult.Item1)
                {
                    if (addResult.Item2 != null)
                    {
                        Core.SendMessage("PCU Limit", $"Block Group PCU Limit reached. Limit of {addResult.Item2.MaximumPCUForLimits:##,###}", Color.Red, steamId);
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

                var beacons = grids.Cast<MyCubeGrid>().SelectMany(x => x.GetFatBlocks().OfType<MyBeacon>())
                    .Select(x => x.BlockDefinition.BlockPairName).Distinct();
                var shipClass = GetMaxLimitByBeaconPairName(beacons);
                if (shipClass.Key == null)
                {
                    Core.Log.Info("null key");
                    return null;
                }
                Core.Log.Info($"{LimitDefinitions.Count}");
                Core.Log.Info($"{shipClass.Key}");
                if (LimitDefinitions.TryGetValue(shipClass.Key, out var shipDefinition))
                {
                    Core.Log.Info("Trying to setup new definition");
                    var newClass = (ShipLimits)shipDefinition.Clone();
                    GridsClass[__instance.GetBiggestGridInGroup().EntityId] = newClass;
                    newClass.Setup(__instance.GetBiggestGridInGroup());
                    return newClass;
                }
                Core.Log.Info("Cant setup definition");
                return null;

                //find the class

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
                return beacons
                    .Select(beacon => new KeyValuePair<string, int>(beacon, LimitsByBeaconPairName.TryGetValue(beacon, out int limit) ? limit : 5000))
                    .OrderByDescending(pair => pair.Value)
                    .FirstOrDefault();
            }
        }
    }
}

