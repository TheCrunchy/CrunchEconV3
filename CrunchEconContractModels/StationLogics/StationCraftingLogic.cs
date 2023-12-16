using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders.Private;
using IMyTextPanel = Sandbox.ModAPI.Ingame.IMyTextPanel;

namespace CrunchEconContractModels.StationLogics
{
    public class StationCraftingLogic : IStationLogic
    {  //var cargos = new List<string>() { "Cargo1", "Cargo2" };
        //if (block.DisplayNameText != null && !cargos.Contains(block.DisplayNameText))
        //{
        //    continue;
        //}
        public static List<VRage.Game.ModAPI.IMyInventory> GetInventories(MyCubeGrid grid, string cargoNames = "")
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<VRage.Game.ModAPI.IMyInventory>();
            var gridOwnerFac = FacUtils.GetOwner(grid);
            
            foreach (var block in grid.GetFatBlocks().OfType<MyCargoContainer>().Where(x => x.OwnerId == gridOwnerFac))
            {
                if (cargoNames != "")
                {
                    if (!cargoNames.Contains($"{block.DisplayNameText}"))
                    {
                        continue;
                    }
                }
                for (int i = 0; i < block.InventoryCount; i++)
                {
                    VRage.Game.ModAPI.IMyInventory inv = ((VRage.Game.ModAPI.IMyCubeBlock)block).GetInventory(i);
                    inventories.Add(inv);
                }
            }
            return inventories;
        }
        public void Setup()
        {
            CraftableItems = new List<CraftedItem>()
            {
                new CraftedItem()
                {
                    amountPerCraft = 5,
                    chanceToCraft = 1,
                    Enabled = true,
                    RequriedItems = new List<RecipeItem>()
                    {
                        new RecipeItem() { amount = 1, subtypeid = "Stone", typeid = "Ore" },
                        new RecipeItem() { amount = 1, subtypeid = "Ice", typeid = "Ore" }
                    },
                    subtypeid = "Iron",
                    typeid = "Ingot",
                }
            };
        }

        public Task<bool> DoLogic(MyCubeGrid grid)
        {
            if (DateTime.Now >= NextRefresh)
            {
                NextRefresh = DateTime.Now.AddSeconds(SecondsBetweenRefresh);
            }
            else
            {
                return Task.FromResult(true);
            }
            var inventory = GetInventories(grid, CargoNamesSeperatedByCommas);

            foreach (CraftedItem item in this.CraftableItems.Where(x => x.Enabled))
            {
                double yeet = Core.random.NextDouble();
                if (!(yeet <= item.chanceToCraft)) continue;
                var comps = new Dictionary<MyDefinitionId, int>();

                if (!MyDefinitionId.TryParse("MyObjectBuilder_" + item.typeid, item.subtypeid,
                        out MyDefinitionId id)) continue;
                foreach (RecipeItem recipe in item.RequriedItems)
                {
                    if (MyDefinitionId.TryParse("MyObjectBuilder_" + recipe.typeid, recipe.subtypeid, out MyDefinitionId id2))
                    {
                        comps.Add(id2, recipe.amount);
                    }
                }

                //   GroupPlugin.Log.Info($"Checking {comps.Count}");

                if (!ConsumeComponents(inventory, comps)) continue;
                //     GroupPlugin.Log.Info("It consumed");
                SpawnItems(inventory, id, item.amountPerCraft);
                //      GroupPlugin.Log.Info("Checking 2");
                comps.Clear();

            }


            return Task.FromResult(true);

        }

        public bool SpawnItems(List<VRage.Game.ModAPI.IMyInventory> inventories,MyDefinitionId id, MyFixedPoint amount)
        {
            foreach (var cargo in inventories)
            {
                MyItemType itemType = new MyInventoryItemFilter(id.TypeId + "/" + id.SubtypeName).ItemType;
                if (cargo.CanItemsBeAdded(amount, itemType))
                {
                    cargo.AddItems(amount, (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializerKeen.CreateNewObject(id));
                    return true;
                }
            }

            return false;
        }
        public static bool ConsumeComponents(IEnumerable<VRage.Game.ModAPI.IMyInventory> inventories, IDictionary<MyDefinitionId, int> components)
        {
            List<MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, VRage.MyFixedPoint>> toRemove = new List<MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, VRage.MyFixedPoint>>();
            foreach (KeyValuePair<MyDefinitionId, int> c in components)
            {
                MyFixedPoint needed = CountComponentsTwo(inventories, c.Key, c.Value, toRemove);

                if (needed > 0)
                {
                    //   GroupPlugin.Log.Info("Not found components");
                    return false;
                }
            }
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                foreach (MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, MyFixedPoint> item in toRemove)
                    item.Item1.RemoveItemAmount(item.Item2, item.Item3);
            });
            return true;
        }
        public static MyFixedPoint CountComponentsTwo(IEnumerable<VRage.Game.ModAPI.IMyInventory> inventories, MyDefinitionId id, int amount, ICollection<MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, MyFixedPoint>> items)
        {
            MyFixedPoint targetAmount = amount;
            foreach (var inv in inventories)
            {
                var invItem = inv.FindItem(id);
                if (invItem == null) continue;
                if (invItem.Amount >= targetAmount)
                {
                    items.Add(new MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, MyFixedPoint>(inv, invItem, targetAmount));
                    targetAmount = 0;
                    break;
                }

                items.Add(new MyTuple<VRage.Game.ModAPI.IMyInventory, VRage.Game.ModAPI.IMyInventoryItem, MyFixedPoint>(inv, invItem, invItem.Amount));
                targetAmount -= invItem.Amount;
            }
            return targetAmount;
        }

        public DateTime NextRefresh { get; set; }
        public int SecondsBetweenRefresh = 600;
        public int Priority { get; set; }
        public string CargoNamesSeperatedByCommas { get; set; } = "";

        public List<CraftedItem> CraftableItems = new List<CraftedItem>();

        public class RecipeItem
        {
            public string typeid;
            public string subtypeid;
            public int amount;
        }

        public class CraftedItem
        {
            public bool Enabled = true;
            public string typeid;
            public string subtypeid;
            public double chanceToCraft = 0.5;
            public int amountPerCraft;
            public List<RecipeItem> RequriedItems = new List<RecipeItem>();
        }
    }
}
