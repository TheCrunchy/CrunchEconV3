﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using NLog.Fluent;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Groups;
using VRage.ObjectBuilders;
using VRageMath;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace CrunchEconContractModels.StationLogics
{
    public class ExampleCommand : CommandModule
    {
        [Command("exportstore", "export the orders and offers in a store block to store file")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportStore()
        {
            var items = new Dictionary<string, Dictionary<string, StoreEntryModel>>();
            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> gridWithSubGrids = GridFinder.FindLookAtGridGroup(Context.Player.Character);
            List<MyCubeGrid> grids = new List<MyCubeGrid>();
            foreach (var item in gridWithSubGrids)
            {
                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in item.Nodes)
                {
                    MyCubeGrid grid = groupNodes.NodeData;
                    foreach (var store in grid.GetFatBlocks().OfType<MyStoreBlock>())
                    {
                        foreach (var thing in store.PlayerItems)
                        {
                            if (items.TryGetValue(store.DisplayNameText, out var savedItems))
                            {
                                if (savedItems.TryGetValue($"{thing?.Item?.TypeIdString}/{thing?.Item?.SubtypeId}", out var stored))
                                {
                                    stored = MapItem(thing, stored);
                                }
                                else
                                {
                                    var mapped = MapItem(thing, new StoreEntryModel());
                                    savedItems[$"{thing?.Item?.TypeIdString}/{thing?.Item?.SubtypeId}"] = mapped;
                                }
                            }
                            else
                            {
                                savedItems = new Dictionary<string, StoreEntryModel>();
                                var mapped = MapItem(thing, new StoreEntryModel());
                                savedItems[$"{thing?.Item?.TypeIdString}/{thing?.Item?.SubtypeId}"] = mapped;

                                items[store.DisplayNameText] = savedItems;
                            }
                        }
                    }

                }
            }

            foreach (var item in items)
            {
                var path = $"{CrunchEconV3.Core.path}/StoreConfigs/{item.Key}.json";
                if (File.Exists(path))
                {
                    Context.Respond($"File name {item.Key} existed, overwriting");
                }
                FileUtils utils = new FileUtils();
                var values = item.Value.Select(x => x.Value).ToList();

                utils.WriteToJsonFile(path, values);
            }


            Context.Respond("Store block exported!");
        }

        private static StoreEntryModel MapItem(MyStoreItem thing, StoreEntryModel stored)
        {
            if (thing.StoreItemType == StoreItemTypes.Offer)
            {
                stored.AmountToBuyMax = thing.Amount;
                stored.AmountToBuyMin = thing.Amount;
                stored.Type = thing.Item.Value.TypeIdString;
                stored.Subtype = thing.Item.Value.SubtypeId;
                stored.BuyFromPlayerPriceMax = thing.PricePerUnit;
                stored.BuyFromPlayerPriceMin = thing.PricePerUnit;
                stored.SpawnIfBelowThisQuantity = thing.Amount;
                stored.SpawnItemsIfMissing = true;
                stored.BuyFromChanceToAppear = 1;
                stored.BuyFromPlayers = true;
            }

            if (thing.StoreItemType == StoreItemTypes.Order)
            {
                stored.AmountToSellMax = thing.Amount;
                stored.AmountToSellMin = thing.Amount;
                stored.Type = thing.Item.Value.TypeIdString;
                stored.Subtype = thing.Item.Value.SubtypeId;
                stored.SellToPlayerPriceMax = thing.PricePerUnit;
                stored.SellToPlayerPriceMin = thing.PricePerUnit;
                stored.SpawnIfBelowThisQuantity = thing.Amount;
                stored.BuyFromChanceToAppear = 1;
                stored.SellToPlayers = true;
            }

            return stored;
        }
    }
    public class StoreManagementLogic : IStationLogic
    {
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Patching buy prefab");
            MethodInfo method = typeof(MyStoreBlock).GetMethod("BuyPrefabInternal", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo patchMethod = typeof(StoreManagementLogic).GetMethod(nameof(BuyPrefabInternalPatch), BindingFlags.NonPublic | BindingFlags.Static);
            ctx.GetPattern(method).Prefixes.Add(patchMethod);
        }

        public static Dictionary<long, long> Safezones = new Dictionary<long, long>();
        private static bool BuyPrefabInternalPatch(
            MyStoreBlock __instance,
            MyStoreItem storeItem,
            int amount,
            MyPlayer player,
            MyFaction faction,
            Vector3D storePosition,
           ref long safezoneId,
            MyStationTypeEnum stationType,
            MyEntity entity,
            long totalPrice)
        {
            Core.Log.Info(safezoneId);
            if (safezoneId == 0l)
            {
                if (Safezones.TryGetValue(__instance.EntityId, out var foundZone))
                {
                    safezoneId = foundZone;
                    return true;
                }


                BoundingSphereD sphere = new BoundingSphereD(storePosition, 1000);

                foreach (MySafeZone zone in MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere)
                             .OfType<MySafeZone>())
                {
                    Safezones[__instance.EntityId] = zone.EntityId;
                    safezoneId = zone.EntityId;
                    return true;
                }
            }
            return true;
        }

        public void Setup()
        {
            StoreItemsHandler.LoadTheFiles();
        }
        

        public static List<VRage.Game.ModAPI.IMyInventory> GetInventories(MyCubeGrid grid, string cargoNames = "")
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<VRage.Game.ModAPI.IMyInventory>();
            var gridOwnerFac = FacUtils.GetOwner(grid);
            
            foreach (var block in grid.GetFatBlocks().OfType<MyCargoContainer>().Where(x => x.OwnerId == gridOwnerFac))
            {
                if (cargoNames != "")
                {
                    if (block.DisplayNameText != null && !cargoNames.Contains(block.DisplayNameText))
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
        public static List<VRage.Game.ModAPI.IMyInventory> ClearInventories(MyCubeGrid grid, string cargoNames = "")
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = GetInventories(grid, cargoNames);

            foreach (var inv in inventories)
            {
                inv.Clear();
            }
            return inventories;
        }

        public void ClearStoreOfPlayersBuyingOffers(MyStoreBlock store)
        {

            List<MyStoreItem> yeet = new List<MyStoreItem>();
            foreach (MyStoreItem item in store.PlayerItems)
            {
                yeet.Add(item);
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

            if (this.DeleteItemsPeriodically)
            {
                if (DateTime.Now >= NextDelete)
                {
                    NextDelete = DateTime.Now.AddMinutes(MinutesBetweenDeletes);
                    ClearInventories(grid);
                }

            }
            var Station = Core.StationStorage.GetAll().FirstOrDefault(x => x.GetGrid() == grid);
            if (Station == null)
            {
                return Task.FromResult(true);
            }

            var gridOwnerFac = FacUtils.GetOwner(grid);
            foreach (var store in grid.GetFatBlocks().OfType<MyStoreBlock>().Where(x => x.OwnerId == gridOwnerFac))
            {

                ClearStoreOfPlayersBuyingOffers(store);
                var items = StoreItemsHandler.GetByBlockName(store.DisplayNameText);
                foreach (var item in items)
                {
                    if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id))
                    {
                        CrunchEconV3.Core.Log.Error($"{item.Type} {item.Subtype} not a valid id");
                        continue;
                    };
                    var inventories = GetInventories(grid, CargoNamesSeperatedByCommas);
                    var quantity = CrunchEconV3.Handlers.InventoriesHandler.CountComponents(inventories, id);
                    try
                    {
                        DoBuy(item, store, quantity, inventories);
                        DoSell(item, store, quantity, inventories);
                    }
                    catch (Exception e)
                    {
                        CrunchEconV3.Core.Log.Error(e);
                    }

                }
            }

            return Task.FromResult(true);
        }

        public static void DoBuy(StoreEntryModel item, MyStoreBlock store, MyFixedPoint quantityInGrid, List<IMyInventory> gridInventories)
        {
            if (!item.BuyFromPlayers) return;
            if (item.BuyFromChanceToAppear < 1 && CrunchEconV3.Core.random.NextDouble() > item.BuyFromChanceToAppear)
            {
                return;
            }

            if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id)) return;

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


            //    long newid2 = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
            //   MyStoreItem myStoreItem3 = new MyStoreItem(newid2, amount, 50, StoreItemTypes.Order, ItemTypes.Hydrogen);
            //    myStoreItem3.IsCustomStoreItem = true;
            //   myStoreItem3.PrefabName = "TestDestroyer";
            //  long newid3 = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
            //  MyStoreItem myStoreItem2 = new MyStoreItem(newid3, amount, 50, StoreItemTypes.Order, ItemTypes.Grid);

            // myStoreItem2.IsCustomStoreItem = true;
            //  myStoreItem2.PrefabName = "TestDestroyer";
            //   store.PlayerItems.Add(myStoreItem3);
            //   store.PlayerItems.Add(myStoreItem2);

            //  station.StoreItems.Add(myStoreItem);
            if (result != MyStoreInsertResults.Success)
            {
                if (result == MyStoreInsertResults.Fail_PricePerUnitIsLessThanMinimum)
                {
                    long newid = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                    MyStoreItem myStoreItem = new MyStoreItem(newid, amount, price, StoreItemTypes.Order, ItemTypes.PhysicalItem);
                    myStoreItem.Item = itemId;
                    store.PlayerItems.Add(myStoreItem);
                }
                else
                {
                    CrunchEconV3.Core.Log.Error($"Unable to insert this order into store {item.Type} {item.Subtype} Amount:{itemInsert.Amount} Price:{itemInsert.PricePerUnit} {result.ToString()}");
                }

            }
        }

        public static void DoSell(StoreEntryModel item, MyStoreBlock store, MyFixedPoint quantityInGrid, List<IMyInventory> gridInventories)
        {
            var skip = false;
            if (!item.SellToPlayers) return;
            if (item.SellToChanceToAppear < 1 && CrunchEconV3.Core.random.NextDouble() > item.SellToChanceToAppear)
            {
                return;
            }

            if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id)) return;

            SerializableDefinitionId itemId = new SerializableDefinitionId(id.TypeId, item.Subtype);

            int price = CrunchEconV3.Core.random.Next((int)item.SellToPlayerPriceMin, (int)item.SellToPlayerPriceMax);

            int amount = CrunchEconV3.Core.random.Next((int)item.AmountToSellMin,
                (int)item.AmountToSellMax);
            if (quantityInGrid < amount)
            {
                if (item.SpawnItemsIfMissing && quantityInGrid < item.SpawnIfBelowThisQuantity)
                {
                    var amountToSpawn = amount - quantityInGrid;
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        if (!CrunchEconV3.Handlers.InventoriesHandler.SpawnItems(id, amountToSpawn, gridInventories))
                        {
                            CrunchEconV3.Core.Log.Error(
                                $"Unable to spawn items for offer in grid {item.Type} {item.Subtype}");
                        }
                    });
                }
                else
                {
                    amount = quantityInGrid.ToIntSafe();
                }
            }
            else
            {
                amount = quantityInGrid.ToIntSafe();
            }

            if (amount <= 0)
            {
                return;
            }

            if (!item.IsPrefab)
            {
                MyStoreItemData itemInsert =
                    new MyStoreItemData(itemId, amount, price,
                        null, null);

                MyStoreInsertResults result =
                    store.InsertOffer(itemInsert,
                        out long notUsingThis);
                if (result != MyStoreInsertResults.Success)
                {
                    if (result == MyStoreInsertResults.Fail_PricePerUnitIsLessThanMinimum)
                    {
                        long newid = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                        MyStoreItem myStoreItem = new MyStoreItem(newid, amount, price, StoreItemTypes.Offer, ItemTypes.PhysicalItem);
                        myStoreItem.Item = itemId;
                        store.PlayerItems.Add(myStoreItem);
                    }
                    else
                    {
                        CrunchEconV3.Core.Log.Error($"Unable to insert this order into store {item.Type} {item.Subtype} Amount:{itemInsert.Amount} Price:{itemInsert.PricePerUnit} {result.ToString()}");
                    }

                }
            }
            else
            {
                long newid = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                MyStoreItem myStoreItem2 = new MyStoreItem(newid, amount, price, StoreItemTypes.Offer, ItemTypes.Grid);
                myStoreItem2.IsCustomStoreItem = true;
                myStoreItem2.PrefabName = item.PrefabSubType;

                store.PlayerItems.Add(myStoreItem2);
            }


            //long newid2 = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
            /// MyStoreItem myStoreItem3 = new MyStoreItem(newid2, amount, 50, StoreItemTypes.Offer, ItemTypes.Hydrogen);
            //  myStoreItem3.IsCustomStoreItem = true;
            //        myStoreItem3.PrefabName = "TestDestroyer";

            //MyStoreItem myStoreItem2 = new MyStoreItem(newid, amount, 50, StoreItemTypes.Order, ItemTypes.Hydrogen);
            //     var prefab = MyDefinitionManager.Static.GetPrefabDefinition("TestDestroyer");
            // store.PlayerItems.Add(myStoreItem3);
            // store.PlayerItems.Add(myStoreItem2);


        }
        public string CargoNamesSeperatedByCommas { get; set; } = "";
        public int Priority { get; set; }
        public bool DeleteItemsPeriodically { get; set; } = true;
        public DateTime NextDelete = DateTime.Now;
        public int MinutesBetweenDeletes = 60 * 12;

        public DateTime NextRefresh { get; set; }
        public int SecondsBetweenRefresh = 600;
    }

    public class StoreEntryModel
    {
        public bool IsPrefab = false;
        public string PrefabSubType = "Subtypehere";
        public string Type { get; set; } = "MyObjectBuilder_Ingot";
        public string Subtype { get; set; } = "Iron";
        public bool BuyFromPlayers { get; set; } = false;
        public long BuyFromPlayerPriceMin { get; set; } = 1;
        public long BuyFromPlayerPriceMax { get; set; } = 3;
        public int AmountToBuyMin { get; set; } = 50;
        public int AmountToBuyMax { get; set; } = 60;
        public float BuyFromChanceToAppear = 1;

        public bool SellToPlayers { get; set; } = false;
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

        public static void LoadTheFiles()
        {
            MappedBlockNames.Clear();
            FileUtils utils = new FileUtils();
            if (!Directory.Exists($"{CrunchEconV3.Core.path}/StoreConfigs/"))
            {
                Directory.CreateDirectory($"{CrunchEconV3.Core.path}/StoreConfigs/");
                var list = new List<StoreEntryModel>();
                list.Add(new StoreEntryModel
                {
                    Type = "MyObjectBuilder_Ore",
                    Subtype = "Iron",
                    BuyFromPlayers = true,
                    BuyFromPlayerPriceMin = 5000,
                    BuyFromPlayerPriceMax = 7500,
                    AmountToBuyMin = 2000,
                    AmountToBuyMax = 5000,
                    BuyFromChanceToAppear = 1,
                    SellToPlayers = false,
                    SellToPlayerPriceMin = 0,
                    SellToPlayerPriceMax = 0,
                    AmountToSellMin = 0,
                    AmountToSellMax = 0,
                    SellToChanceToAppear = 0,
                    SpawnItemsIfMissing = false,
                    SpawnIfBelowThisQuantity = 0
                });
                list.Add(new StoreEntryModel
                {
                    Type = "MyObjectBuilder_Ingot",
                    Subtype = "Iron",
                    BuyFromPlayers = false,
                    BuyFromPlayerPriceMin = 5000,
                    BuyFromPlayerPriceMax = 7500,
                    AmountToBuyMin = 2000,
                    AmountToBuyMax = 5000,
                    BuyFromChanceToAppear = 1,
                    SellToPlayers = true,
                    SellToPlayerPriceMin = 5000,
                    SellToPlayerPriceMax = 7500,
                    AmountToSellMin = 50,
                    AmountToSellMax = 100,
                    SellToChanceToAppear = 1,
                    SpawnItemsIfMissing = true,
                    SpawnIfBelowThisQuantity = 50
                });

                utils.WriteToJsonFile($"{CrunchEconV3.Core.path}/StoreConfigs/Example.json", list);
            }

            foreach (var file in Directory.GetFiles($"{CrunchEconV3.Core.path}/StoreConfigs/"))
            {
                try
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    MappedBlockNames.Add(name, utils.ReadFromJsonFile<List<StoreEntryModel>>(file));
                }
                catch (Exception e)
                {
                    CrunchEconV3.Core.Log.Error($"Error reading store entry file {file}");
                    throw;
                }
            }
        }

        public static List<StoreEntryModel> GetByBlockName(string blockname)
        {
            if (MappedBlockNames.TryGetValue(blockname, out var items))
            {
                return items;
            }

            return new List<StoreEntryModel>();
        }

    }
}
