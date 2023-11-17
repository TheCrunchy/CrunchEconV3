using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using NLog.Fluent;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;

namespace CrunchEconContractModels.StationLogics
{
    public class StoreManagementLogic : IStationLogic
    {
        public void Setup()
        {
            StoreItemsHandler.GetByBlockName("INIT THE LIST");
        }
        public static List<VRage.Game.ModAPI.IMyInventory> ClearInventories(MyCubeGrid grid)
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<VRage.Game.ModAPI.IMyInventory>();
            var gridOwnerFac = FacUtils.GetOwner(grid);
            foreach (var block in grid.GetFatBlocks().Where(x => x.OwnerId == gridOwnerFac))
            {
                if (block is MyReactor)
                {
                    continue;
                }

                for (int i = 0; i < block.InventoryCount; i++)
                {
                    VRage.Game.ModAPI.IMyInventory inv = ((VRage.Game.ModAPI.IMyCubeBlock)block).GetInventory(i);
                    inv.Clear();
                }
            }
            return inventories;
        }

        public void ClearStoreOfPlayersBuyingOffers(MyStoreBlock store)
        {

            List<MyStoreItem> yeet = new List<MyStoreItem>();
            foreach (MyStoreItem item in store.PlayerItems)
            {
                if (item.StoreItemType == StoreItemTypes.Offer)
                {
                    yeet.Add(item);
                }
            }
            foreach (MyStoreItem item in yeet)
            {
                store.CancelStoreItem(item.Id);
            }
        }

        public void ClearStoreOfPlayersSellingOrders(MyStoreBlock store)
        {
            List<MyStoreItem> yeet = new List<MyStoreItem>();
            foreach (MyStoreItem item in store.PlayerItems)
            {
                if (item.StoreItemType == StoreItemTypes.Order)
                {

                    yeet.Add(item);
                }
            }
            foreach (MyStoreItem item in yeet)
            {
                store.CancelStoreItem(item.Id);
            }
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

            var gridOwnerFac = FacUtils.GetOwner(grid);
            foreach (var store in grid.GetFatBlocks().OfType<MyStoreBlock>().Where(x => x.OwnerId == gridOwnerFac))
            {
                //clear existing stuff in the store block
                ClearStoreOfPlayersBuyingOffers(store);
                ClearStoreOfPlayersSellingOrders(store);

                var items = StoreItemsHandler.GetByBlockName(store.DisplayNameText);
                foreach (var item in items)
                {
                    try
                    {
                        DoBuy(item, store);
                        DoSell(item,store);
                    }
                    catch (Exception e)
                    {
                        CrunchEconV3.Core.Log.Error(e);
                    }
                }
            }

            return Task.FromResult(true);
        }

        public static void DoBuy(StoreEntryModel item, MyStoreBlock store)
        {
            var skip = false;
            if (!item.BuyFromPlayers) return;
            if (item.BuyFromChanceToAppear < 1)
            {
                var chance = CrunchEconV3.Core.random.NextDouble();
                if (chance > item.BuyFromChanceToAppear)
                {
                    return;
                }
            }

            if (MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id))
            {
                SerializableDefinitionId itemId = new SerializableDefinitionId(id.TypeId, item.Subtype);
                int price = CrunchEconV3.Core.random.Next((int)item.BuyFromPlayerPriceMin, (int)item.BuyFromPlayerPriceMax);
              
                int amount = CrunchEconV3.Core.random.Next((int)item.AmountToBuyMin,
                    (int)item.AmountToBuyMax);
                MyStoreItemData itemInsert =
                    new MyStoreItemData(itemId, amount, price,
                        null, null);
                MyStoreInsertResults result =
                    store.InsertOrder(itemInsert,
                        out long notUsingThis);
                if (result != MyStoreInsertResults.Success)
                {
                    CrunchEconV3.Core.Log.Error($"Unable to insert this order into store {item.Type} {item.Subtype} {itemInsert.PricePerUnit} {result.ToString()}");
                }
            }
        }

        public static void DoSell(StoreEntryModel item, MyStoreBlock store)
        {
            var skip = false;
            if (!item.SellToPlayers) return;
            if (item.SellToChanceToAppear < 1)
            {
                var chance = CrunchEconV3.Core.random.NextDouble();
                if (chance > item.SellToChanceToAppear)
                {
                    return;
                }
            }

        }

        public int Priority { get; set; }
        public bool DeleteItemsPeriodically { get; set; } = true;
        public DateTime NextDelete = DateTime.Now;
        public int MinutesBetweenDeletes = 60 * 12;

        public DateTime NextRefresh { get; set; }
        public int SecondsBetweenRefresh = 600;
    }

    public class StoreEntryModel
    {
        public string Type { get; set; } = "MyObjectBuilder_Ingot/";
        public string Subtype { get; set; } = "Iron";
        public bool BuyFromPlayers { get; set; } = true;
        public long BuyFromPlayerPriceMin { get; set; } = 1;
        public long BuyFromPlayerPriceMax { get; set; } = 3;
        public int AmountToBuyMin { get; set; } = 50;
        public int AmountToBuyMax { get; set; } = 60;
        public float BuyFromChanceToAppear = 1;

        public bool SellToPlayers { get; set; } = true;
        public long SellToPlayerPriceMin { get; set; } = 1;
        public long SellToPlayerPriceMax { get; set; } = 3;
        public int AmountToSellMin { get; set; } = 10;
        public int AmountToSellMax { get; set; } = 15;
        public float SellToChanceToAppear = 1;
        public bool SpawnItemsIfMissing { get; set; } = true;
        public int SpawnIfBelowThisQuantity { get; set; } = 5;
    }
    public static class StoreItemsHandler
    {
        public static Dictionary<string, List<StoreEntryModel>> MappedBlockNames = new Dictionary<string, List<StoreEntryModel>>();


        public static List<StoreEntryModel> GetByBlockName(string blockname)
        {
            if (!MappedBlockNames.Any())
            {
                //populate the dictionary
            }

            if (MappedBlockNames.TryGetValue(blockname, out var items))
            {
                return items;
            }

            return new List<StoreEntryModel>();
        }

    }
}
