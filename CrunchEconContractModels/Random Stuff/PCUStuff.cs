using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class PCUStuff
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
            var grid = block.CubeGrid as MyCubeGrid;
            if (grid.BlocksPCU >= 50)
            {
                block.CubeGrid.RemoveBlock(block);
                return;
            }
        }
    }
}

