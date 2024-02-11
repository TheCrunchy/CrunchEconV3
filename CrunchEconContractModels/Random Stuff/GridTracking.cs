using System;
using System.Collections.Generic;
using CrunchEconV3;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class GridTracking
    {
        private static Guid parsedId = Guid.Parse("464edcf2-15d6-4ccc-96a3-e74fcf0ddcfc");

        public static void Patch(PatchContext ctx)
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd2;
            // Iterate through all existing grids when the mod initializes
            var grids = new List<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity is IMyCubeGrid grid)
                {
                    OnEntityAdd2(grid);
                }
                return false;
            });
        }
        public static void UnPatch(PatchContext ctx)
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd2;

            var grids = new List<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity is IMyCubeGrid grid)
                {
                    grid.OnBlockAdded -= OnBlockAdded2;
                }
                return false;
            });
        }
        private static void OnEntityAdd2(IMyEntity entity)
        {
            if (entity is IMyCubeGrid grid)
            {
                grid.OnBlockAdded += OnBlockAdded2;
                MyModStorageComponentBase storage = grid.Storage;
                if (storage == null)
                {
                    return;
                }


                if (storage.TryGetValue(parsedId, out string idToParse))
                {
                    Core.Log.Info($"Existing ID {idToParse}");
                }
            }
        }

        private static void OnBlockAdded2(IMySlimBlock block)
        {
            Core.Log.Info($"block added");
            MyModStorageComponentBase storage = block.CubeGrid.Storage;
            if (storage.TryGetValue(parsedId, out string idToParse))
            {
                Core.Log.Info($"Parsed ID {idToParse}");
            }
        }
    }
}

