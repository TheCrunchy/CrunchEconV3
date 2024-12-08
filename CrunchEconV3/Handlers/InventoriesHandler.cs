using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders.Private;
using VRageMath;
using IMyCargoContainer = Sandbox.ModAPI.IMyCargoContainer;

namespace CrunchEconV3.Handlers
{
    public static class InventoriesHandler
    {
        public static List<VRage.Game.ModAPI.IMyInventory> GetInventoriesForContract(MyCubeGrid grid)
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<VRage.Game.ModAPI.IMyInventory>();

            foreach (var block in grid.GetFatBlocks())
            {
                if (block is MyReactor reactor)
                {
                    continue;
                }

                for (int i = 0; i < block.InventoryCount; i++)
                {

                    VRage.Game.ModAPI.IMyInventory inv = ((VRage.Game.ModAPI.IMyCubeBlock)block).GetInventory(i);
                    inventories.Add(inv);
                }

            }
            return inventories;
        }
        public static MyFixedPoint CountComponents(IEnumerable<VRage.Game.ModAPI.IMyInventory> inventories, MyDefinitionId id)
        {
            MyFixedPoint targetAmount = 0;
            foreach (VRage.Game.ModAPI.IMyInventory inv in inventories)
            {

                VRage.Game.ModAPI.IMyInventoryItem invItem = inv.FindItem(id);

                if (invItem != null)
                {
                    targetAmount += invItem.Amount;
                }
            }


            return targetAmount;
        }

        public static bool ConsumeComponents(IEnumerable<VRage.Game.ModAPI.IMyInventory> inventories, IDictionary<MyDefinitionId, int> components, ulong steamid)
        {
            List<MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, VRage.MyFixedPoint>> toRemove = new List<MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, VRage.MyFixedPoint>>();
            foreach (KeyValuePair<MyDefinitionId, int> c in components)
            {
                MyFixedPoint needed = CountComponentsTwo(inventories, c.Key, c.Value, toRemove);
                if (needed > 0)
                {
                    return false;
                }
            }


            foreach (MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, MyFixedPoint> item in toRemove)
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    item.Item1.RemoveItemAmount(item.Item2, item.Item3);
                });
            return true;
        }
        public static Dictionary<MyDefinitionId, int> CountAllComponents(IEnumerable<VRage.Game.ModAPI.IMyInventory> inventories)
        {
            var itemCounts = new Dictionary<MyDefinitionId, int>();

            foreach (var inv in inventories)
            {
                var items = new List<MyInventoryItem>();
                inv.GetItems(items); // Get all items in the inventory

                foreach (var item in items)
                {
                    var itemId = MyDefinitionId.Parse($"{item.Type.TypeId}/{item.Type.SubtypeId}");
                    var amount = (int)item.Amount;

                    if (itemCounts.TryGetValue(itemId, out var storedAmount))
                    {
                        storedAmount += amount;
                    }
                    else
                    {
                        itemCounts[itemId] = amount;
                    }
                }
            }

            return itemCounts;
        }

        public static MyFixedPoint CountComponentsTwo(IEnumerable<VRage.Game.ModAPI.IMyInventory> inventories, MyDefinitionId id, int amount, ICollection<MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, MyFixedPoint>> items)
        {
            MyFixedPoint targetAmount = amount;
            foreach (VRage.Game.ModAPI.IMyInventory inv in inventories)
            {
                VRage.Game.ModAPI.IMyInventoryItem invItem = inv.FindItem(id);
                if (invItem != null)
                {
                    if (invItem.Amount >= targetAmount)
                    {
                        items.Add(new MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, MyFixedPoint>(inv, invItem, targetAmount));
                        targetAmount = 0;
                        break;
                    }
                    else
                    {
                        items.Add(new MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, MyFixedPoint>(inv, invItem, invItem.Amount));
                        targetAmount -= invItem.Amount;
                    }
                }
            }
            return targetAmount;
        }

        public static bool SpawnItems(MyDefinitionId id, MyFixedPoint amount, List<VRage.Game.ModAPI.IMyInventory> inventories)
        {
            try
            {
                foreach (var inv in inventories)
                {
                    if (inv.CanItemsBeAdded(amount, id))
                    {
                        var obj = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializerKeen.CreateNewObject(id);
                        if (obj == null)
                        {
                            return false;
                        }
                        inv.AddItems(amount, obj);
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Core.Log.Error(e);
                return false;
            }
            return false;
        }
    }
}
