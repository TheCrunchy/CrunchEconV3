using System.Collections.Generic;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace CrunchEconContractModels.Ship_Class_Stuff
{
    [PatchShim]
    public static class FunctionalBlockPatch
    {



        public static void Patch(PatchContext ctx)
        {
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

