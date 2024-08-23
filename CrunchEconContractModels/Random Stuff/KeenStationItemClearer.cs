using System.Collections.Generic;
using System.Linq;
using CrunchEconV3;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class KeenStationItemClearer
    {
        public static List<string> ItemsToDelete = new List<string>()
        {
            "MyObjectBuilder_Component/Tech2x",
            "MyObjectBuilder_Component/Tech4x",
            "MyObjectBuilder_Component/Tech8x",
        };

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
                var asMyCube = grid as MyCubeGrid;
                if (MySession.Static.Factions.Any(x => x.Value.Stations.Any(z => z.StationEntityId == grid.EntityId)))
                {
                    Core.Log.Info("Keen Station spawned, clearing");
                    foreach (var block in asMyCube.GetFatBlocks())
                    {
                        if (block.HasInventory)
                        {
                            List<uint> deleteThese = new List<uint>();
                            foreach (MyPhysicalInventoryItem invitem in block.GetInventory().GetItems())
                            {
                                if (ItemsToDelete.Contains($"{invitem.Content.TypeId}/{invitem.Content.SubtypeName}"))
                                {
                                    Core.Log.Info($"Item to delete found {invitem.Content.TypeId}/{invitem.Content.SubtypeName}");
                                    deleteThese.Add(invitem.ItemId);
                                }
                            }
                            foreach (uint id in deleteThese)
                            {
                                block.GetInventory().RemoveItems(id);
                            }
                        }
                    }
                }
            }
        }
    }
}

