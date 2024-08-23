using System.Collections.Generic;
using System.Linq;
using CrunchEconV3;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMyEntity = VRage.ModAPI.IMyEntity;
using IMySlimBlock = VRage.Game.ModAPI.IMySlimBlock;

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
            Core.UpdateCycle += Update;
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

        private static Stack<MyCubeGrid> Grids = new Stack<MyCubeGrid>();
        private static void Update()
        {
            if (Grids.Any())
            {
                var grid = Grids.Pop();
                if (MySession.Static.Factions.Any(x => x.Value.Stations.Any(z => z.StationEntityId == grid.EntityId)))
                {
                    Core.Log.Info("Keen Station spawned, clearing");
                    foreach (var block in grid.CubeBlocks)
                    {
                        if (block is IMyEntity inventory)
                        {
                            List<uint> deleteThese = new List<uint>();
                            for (int i = 0; i < inventory.InventoryCount; i++)
                            {
                                VRage.Game.ModAPI.IMyInventory inv = ((VRage.Game.ModAPI.IMyCubeBlock)block).GetInventory(i);
                                foreach (MyPhysicalInventoryItem invitem in inv.GetItems())
                                {
                                    if (ItemsToDelete.Contains($"{invitem.Content.TypeId}/{invitem.Content.SubtypeName}"))
                                    {
                                        Core.Log.Info($"Item to delete found {invitem.Content.TypeId}/{invitem.Content.SubtypeName}");
                                        deleteThese.Add(invitem.ItemId);
                                    }
                                    else
                                    {
                                        Core.Log.Info($"Item found {invitem.Content.TypeId}/{invitem.Content.SubtypeName}");
                                    }
                                }
                                foreach (uint id in deleteThese)
                                {
                                    inv.RemoveItems(id);
                                }
                            }

                        }
                    }
                }
            }
        }

        private static void OnEntityAdd(IMyEntity entity)
        {
            if (entity is IMyCubeGrid grid)
            {
                var asMyCube = grid as MyCubeGrid;
                Core.Log.Info("Checking grid");
                Grids.Push(asMyCube);
            }
        }
    }
}

