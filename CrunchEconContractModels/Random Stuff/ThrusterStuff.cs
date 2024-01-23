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
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace CrunchEconContractModels.StationLogics
{
    [PatchShim]
    public static class FunctionalBlockPatch
    {



        public static void Patch(PatchContext ctx)
        {
            //Dictionary<string, Dictionary<string, Double>> IngotAndOres = new Dictionary<string, Dictionary<string, Double>>();
            //var subType = "yourSubtypeId";
            //var type = "yourTypeId";
            //var amount = 0;
            //if (IngotAndOres.TryGetValue(subType, out var values))
            //{
            //    values[type] += amount;
            //}
            //else
            //{
            //    IngotAndOres.Add(subType, new Dictionary<string, double>(){ {type, amount}});
            //}


            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;

            // Iterate through all existing grids when the mod initializes
            var grids = new List<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity is IMyCubeGrid grid)
                {
                    OnEntityAdd(grid);
                }
                return false;
            });
        }
        private static void OnEntityAdd(IMyEntity entity)
        {
            if (entity is IMyCubeGrid grid)
            {
                grid.OnBlockAdded += OnBlockAdded;
            }
        }

        private static void OnBlockAdded(IMySlimBlock block)
        {
            //if (block.BlockDefinition != null && block.BlockDefinition.Id.SubtypeName.Contains("Refinery"))
            //{
            //    var grid = block.CubeGrid as MyCubeGrid;

            //    grid.GridGeneralDamageModifier.ValidateAndSet(1.5f);
            //    Core.Log.Info("Setting modifier to 0.5f");
            //}
            MyBatteryBlock bat = null;
            bat.CurrentStoredPower -= 5;
            
            if (block.BlockDefinition != null && block.BlockDefinition.Id.SubtypeName.Contains("Epstein"))
            {
                IMyCubeGrid cubeGrid = block.CubeGrid;
                var epsteinThrusters = new List<IMySlimBlock>();
                cubeGrid.GetBlocks(epsteinThrusters, b => b.FatBlock is IMyThrust && b.BlockDefinition.Id.SubtypeName.Contains("Epstein"));
                foreach (var thruster in epsteinThrusters)
                {
                    if (!thruster.FatBlock.WorldMatrix.Forward.Equals(block.FatBlock.WorldMatrix.Forward))
                    {
                        block.CubeGrid.RemoveBlock(block);
                    }
                }
            }
        }
    }
}

