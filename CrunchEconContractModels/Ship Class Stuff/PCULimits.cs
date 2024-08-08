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
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.Network;
using VRageMath;

namespace CrunchEconContractModels.Ship_Class_Stuff
{
    [PatchShim]
    public static class PCULimits
    {

        public class ShipLimits : ICloneable
        {
            public bool Enabled { get; set; }
            public float GridDamageModifier { get; set; } = 1;
            private Dictionary<string, BlockGroupLimit> PairNameToLimits { get; set; } =
                new Dictionary<string, BlockGroupLimit>();

            public List<BlockGroupLimit> Limits = new List<BlockGroupLimit>();
            public (bool, BlockGroupLimit) CheckCanAddBlock(MyCubeBlockDefinition definition)
            {
                if (PairNameToLimits.TryGetValue(definition.BlockPairName, out var limited))
                {
                    return limited.AddBlock(definition);
                }
                else
                {
                    if (BuildBlockPatch.LimitedBlocks.Contains(definition.BlockPairName))
                    {
                        return (false, null);
                    }
                }

                return (true, null);
            }

            public void SetupPairNamesToLimits()
            {
                foreach (var limit in Limits)
                {
                    foreach (var block in limit.AllowedBlocks)
                    {
                        PairNameToLimits.Add(block, limit);
                    }

                }
            }

            public int MaximumPCU { get; set; }
            public string BeaconPairName { get; set; }

            public object Clone()
            {
                ShipLimits clonedLimits = new ShipLimits();
                clonedLimits.MaximumPCU = this.MaximumPCU;
                clonedLimits.BeaconPairName = this.BeaconPairName;
                Dictionary<BlockGroupLimit, BlockGroupLimit> clonedBlockGroupLimits = new Dictionary<BlockGroupLimit, BlockGroupLimit>();

                foreach (var pair in this.PairNameToLimits)
                {
                    // Clone the BlockGroupLimit instance if it hasn't been cloned yet
                    if (!clonedBlockGroupLimits.TryGetValue(pair.Value, out var clonedLimit))
                    {
                        clonedLimit = (BlockGroupLimit)pair.Value.Clone();
                        clonedBlockGroupLimits.Add(pair.Value, clonedLimit);
                    }

                    // Add the cloned BlockGroupLimit instance to the PairNameToLimits dictionary
                    clonedLimits.PairNameToLimits.Add(pair.Key, clonedLimit);
                }
                return clonedLimits;
            }

            public void Setup(MyCubeGrid cubeGrid)
            {
                var blocksToRemove = cubeGrid.GetFatBlocks()
                    .Where(block => !CheckCanAddBlock(block.BlockDefinition).Item1)
                    .ToList();

                foreach (var block in blocksToRemove)
                {
                    cubeGrid.RemoveBlock(block.SlimBlock);
                }
                cubeGrid.OnBlockRemoved += HandleBlockRemoved;
                cubeGrid.GridGeneralDamageModifier.ValidateAndSet(GridDamageModifier);
            }

            private void HandleBlockRemoved(IMySlimBlock removedBlock)
            {
                var slim = removedBlock as MySlimBlock;
                var definition = slim.BlockDefinition;
                if (definition == null)
                    return;

                var blockGroupLimit = GetBlockGroupLimitForGrid(removedBlock.CubeGrid.EntityId);
                if (blockGroupLimit != null && blockGroupLimit.PairNameToLimits.TryGetValue(definition.BlockPairName, out var limit))
                {
                    limit.UsedPCU -= definition.PCU;
                }
            }

            private PCULimits.ShipLimits GetBlockGroupLimitForGrid(long entityId)
            {
                if (BuildBlockPatch.GridsClass.TryGetValue(entityId, out var shipLimits))
                {
                    return shipLimits;
                }
                return null;
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
            Core.Log.Info($"{UsedPCU}");
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
        public static List<string> LimitedBlocks { get; } = new List<string>();

        public static Dictionary<string, PCULimits.ShipLimits> LimitDefinitions { get; set; } =
            new Dictionary<string, PCULimits.ShipLimits>();

        public static Dictionary<long, PCULimits.ShipLimits> GridsClass = new Dictionary<long, PCULimits.ShipLimits>();

        public static void Patch(PatchContext ctx)
        {
            SetupLimitsByBeaconPairName();
            SetupLimitDefinitions();

            MethodInfo method = typeof(MyCubeGrid).GetMethod("BuildBlocksRequest",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        //    ctx.GetPattern(method).Prefixes.Add(typeof(BuildBlockPatch).GetMethod("BuildBlocksRequest",
          //      BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic));
        }

        private static void SetupLimitsByBeaconPairName()
        {
            // No setup required, dictionary initialized with values.
        }

        private static void SetupLimitDefinitions()
        {
            var limit = new BlockGroupLimit()
            {
                AllowedBlocks = new List<string>()
                //AllowedBlocks = new List<string>() { "CWIS", "C30mmRevolver", "C30mmSingleT", "VXM-08 Multi Launch Missile System" },
                //MaximumPCUForLimits = 2000,
                //UsedPCU = 0
            };
            var gridLimit = new PCULimits.ShipLimits()
            {
                MaximumPCU = 35000,
                BeaconPairName = "Beacon",
                Limits = new List<BlockGroupLimit>() { limit }
            };

          //  gridLimit.SetupPairNamesToLimits();
         //   LimitDefinitions.Add("Beacon", gridLimit);
            gridLimit.Enabled = false;

            string jsonFilePath = Path.Combine($"{CrunchEconV3.Core.path}/ShipClasses/", "Example.json");
            Directory.CreateDirectory($"{CrunchEconV3.Core.path}/ShipClasses/");
            FileUtils utils = new FileUtils();
            utils.WriteToJsonFile(jsonFilePath, gridLimit, false);
            string[] filePaths = Directory.GetFiles($"{CrunchEconV3.Core.path}/ShipClasses/");
            foreach (string filePath in filePaths)
            {
        
                PCULimits.ShipLimits shipLimit = utils.ReadFromJsonFile<PCULimits.ShipLimits>(filePath);
                if (shipLimit.Enabled)
                {
                    shipLimit.SetupPairNamesToLimits();
                    LimitDefinitions.Add(shipLimit.BeaconPairName, shipLimit);
                }
            
            }
        }
        private static bool BuildBlocksRequest(MyCubeGrid __instance, HashSet<MyCubeGrid.MyBlockLocation> locations)
        {
            if (__instance == null || !locations.Any())
                return true;

            var definition = MyDefinitionManager.Static.GetCubeBlockDefinition(locations.First().BlockDefinition);
            if (definition == null)
                return true;

            var grids = new List<IMyCubeGrid>();
            __instance.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(grids);

            var shipClass = GetGridsClass(__instance, grids);
            if (shipClass == null)
            {
                Core.Log.Info("Failed to retrieve ship class.");
                return true;
            }

            if (!CheckPCULimit(__instance, shipClass, definition))
                return false;

            var addResult = shipClass.CheckCanAddBlock(definition);
            if (!addResult.Item1)
            {
                Core.SendMessage("PCU Limit",
                    $"Block Group PCU Limit reached. Limit of {addResult.Item2.MaximumPCUForLimits:##,###}", Color.Red,
                    MyEventContext.Current.Sender.Value);
                return false;
            }

            return true;
        }

        private static bool CheckPCULimit(MyCubeGrid grid, PCULimits.ShipLimits shipClass,
            MyCubeBlockDefinition definition)
        {
            ulong steamId = MyEventContext.Current.Sender.Value;

            if (MySession.Static.Players.TryGetPlayerBySteamId(steamId, out var player))
            {
                var pcu = grid.BlocksPCU;

                if (pcu > shipClass.MaximumPCU || pcu + definition.PCU > shipClass.MaximumPCU)
                {
                    Core.SendMessage("PCU Limit", $"Grid PCU Limit reached. Limit of {shipClass.MaximumPCU:##,###}",
                        Color.Red, steamId);
                    return false;
                }
            }

            return true;
        }

        private static PCULimits.ShipLimits GetGridsClass(MyCubeGrid grid, List<IMyCubeGrid> grids)
        {
            if (GridsClass.TryGetValue(grid.GetBiggestGridInGroup().EntityId, out var foundClass))
                return foundClass;

            var beacons = grids.OfType<MyCubeGrid>().SelectMany(x => x.GetFatBlocks().OfType<MyBeacon>())
                .Select(x => x.BlockDefinition.BlockPairName)
                .Distinct();
            var shipClass = GetMaxLimitByBeaconPairName(beacons);
            if (shipClass.Key == null)
            {
                Core.Log.Info("Failed to retrieve ship class key.");
                return null;
            }

            if (LimitDefinitions.TryGetValue(shipClass.Key, out var shipDefinition))
            {
                Core.Log.Info("Setting up new ship definition.");
                var newClass = (PCULimits.ShipLimits)shipDefinition.Clone();
                GridsClass[grid.GetBiggestGridInGroup().EntityId] = newClass;
                newClass.Setup(grid.GetBiggestGridInGroup());
                return newClass;
            }

            Core.Log.Info("Failed to setup ship definition.");
            return null;
        }

        public static KeyValuePair<string, int> GetMaxLimitByBeaconPairName(IEnumerable<string> beacons)
        {
            return beacons
                .Select(beacon =>
                {
                    if (LimitDefinitions.TryGetValue(beacon, out var shipLimit))
                    {
                        return new KeyValuePair<string, int>(beacon, shipLimit.MaximumPCU);
                    }
                    return new KeyValuePair<string, int>(beacon, 5000); // Default value if beacon not found
                })
                .OrderByDescending(pair => pair.Value)
                .FirstOrDefault();
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
    }

}