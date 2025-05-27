using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using NLog.Fluent;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.SessionComponents;
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
using VRage.Game.ObjectBuilders.Gui;
using VRage.Groups;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace CrunchEconContractModels.StationLogics
{
    public class ExampleCommand : CommandModule
    {
        [Command("easystore", "export the orders and offers in a store block to store file")]
        [Permission(MyPromoteLevel.Admin)]
        public void EasyStore(string stationName, string ownerTag)
        {
            var station = new StationConfig();
            station.FileName = stationName + ".json";
            station.LocationGPS = GPSHelper.CreateGps(Context.Player.GetPosition(), Color.Orange, "Station", "").ToString();
            station.Enabled = true;
            station.FactionTag = ownerTag;
            station.Logics = new List<IStationLogic>();
            station.Logics.Add(new StoreManagementLogic());
            station.ContractFiles = new List<string>();

            Core.StationStorage.Save(station);

            var items = new Dictionary<string, Dictionary<string, StoreEntryModel>>();
            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> gridWithSubGrids = GridFinder.FindLookAtGridGroup(Context.Player.Character);
            ReadAndSaveStores(gridWithSubGrids, items);

            Context.Respond("Station Saved, !crunchecon reload to load it");
        }

        [Command("exportstore", "export the orders and offers in a store block to store file")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportStore()
        {
            var items = new Dictionary<string, Dictionary<string, StoreEntryModel>>();
            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> gridWithSubGrids = GridFinder.FindLookAtGridGroup(Context.Player.Character);
            ReadAndSaveStores(gridWithSubGrids, items);
            Context.Respond("Store block exported!");
        }

        [Command("addsafezone", "add a safezone at players current location")]
        [Permission(MyPromoteLevel.Admin)]
        public void addSafeZone(long safezoneSize = 500)
        {
            MyObjectBuilder_SafeZone objectBuilderSafeZone = new MyObjectBuilder_SafeZone();
            objectBuilderSafeZone.PositionAndOrientation = new MyPositionAndOrientation?(new MyPositionAndOrientation(Context.Player.Character.PositionComp.GetPosition(), Vector3.Forward, Vector3.Up));
            objectBuilderSafeZone.PersistentFlags = MyPersistentEntityFlags2.InScene;
            objectBuilderSafeZone.Shape = MySafeZoneShape.Sphere;
            objectBuilderSafeZone.Radius = (float)safezoneSize;
            objectBuilderSafeZone.Enabled = true;
            objectBuilderSafeZone.DisplayName = $"Store Safezone";
            objectBuilderSafeZone.AccessTypeGrids = MySafeZoneAccess.Blacklist;
            objectBuilderSafeZone.AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist;
            objectBuilderSafeZone.AccessTypeFactions = MySafeZoneAccess.Blacklist;
            objectBuilderSafeZone.AccessTypePlayers = MySafeZoneAccess.Blacklist;
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                MyEntity ent =
                    Sandbox.Game.Entities.MyEntities.CreateFromObjectBuilderAndAdd(
                        (MyObjectBuilder_EntityBase)objectBuilderSafeZone, true);
            });
        }


        private void ReadAndSaveStores(ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> gridWithSubGrids, Dictionary<string, Dictionary<string, StoreEntryModel>> items)
        {
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
                                if (savedItems.TryGetValue($"{thing?.Item?.TypeIdString}/{thing?.Item?.SubtypeId}",
                                        out var stored))
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

            foreach (KeyValuePair<long, MyFaction> faction in MySession.Static.Factions)
            {
                foreach (MyStation station in faction.Value.Stations)
                {
                    if (station.StoreItems == null)
                    {
                        continue;
                    }

                    foreach (var thing in station.StoreItems)
                    {
                        if (thing.IsCustomStoreItem)
                        {
                            continue;
                        }

                        try
                        {
                            if (items.TryGetValue(faction.Value.Tag, out var savedItems))
                            {
                                if (savedItems.TryGetValue($"{thing?.Item?.TypeIdString}/{thing?.Item?.SubtypeId}",
                                        out var stored))
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

                                items[faction.Value.Tag] = savedItems;
                            }
                        }
                        catch (Exception)
                        {
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
        }

        private static StoreEntryModel MapItem(MyStoreItem thing, StoreEntryModel stored)
        {
            if (thing.StoreItemType == StoreItemTypes.Offer)
            {
                stored.AmountToBuyMax = thing.Amount;
                stored.AmountToBuyMin = thing.Amount;
                stored.Type = thing.Item.Value.TypeIdString;
                stored.Subtype = thing.Item.Value.SubtypeId;
                stored.SellToPlayerPriceMax = thing.PricePerUnit;
                stored.SellToPlayerPriceMax = thing.PricePerUnit;
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
                stored.BuyFromPlayerPriceMax = thing.PricePerUnit;
                stored.BuyFromPlayerPriceMax = thing.PricePerUnit;
                stored.SpawnIfBelowThisQuantity = thing.Amount;
                stored.BuyFromChanceToAppear = 1;
                stored.SellToPlayers = true;
            }

            return stored;
        }

        [Command("reloadstores", "export the orders and offers in a store block to store file")]
        [Permission(MyPromoteLevel.Admin)]
        public void ReloadStores()
        {
            Context.Respond("Loading the files");
            StoreItemsHandler.LoadTheFiles();
            Context.Respond("Loaded the files");
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
            DatapadHelper.Setup();
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

            if (DebugMessages)
            {
                Core.Log.Error("Store Management running");
            }
            if (this.DeleteItemsPeriodically)
            {
                if (DateTime.Now >= NextDelete)
                {
                    if (DebugMessages)
                    {
                        Core.Log.Error("Running delete");
                    }
                    NextDelete = DateTime.Now.AddMinutes(MinutesBetweenDeletes);
                    ClearInventories(grid);
                }

            }
            var Station = Core.StationStorage.GetAll().FirstOrDefault(x => x.GetGrid() == grid);
            if (Station == null)
            {
                if (DebugMessages)
                {
                    Core.Log.Error("Station grid is null");
                }
                return Task.FromResult(true);
            }
            if (DebugMessages)
            {
                Core.Log.Error("beginning store loop");
            }
            var gridOwnerFac = FacUtils.GetOwner(grid);
            foreach (var store in grid.GetFatBlocks().OfType<MyStoreBlock>().Where(x => x.OwnerId == gridOwnerFac))
            {
                if (DebugMessages)
                {
                    Core.Log.Error($"{store.DisplayNameText}");
                }

                if (store.DisplayNameText.Contains("!exclude"))
                {
                    continue;
                }

                ClearStoreOfPlayersBuyingOffers(store);
                var items = StoreItemsHandler.GetByBlockName(store.DisplayNameText);
                if (DebugMessages)
                {
                    Core.Log.Error($"Checking {items.Count} entries");
                }
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
            if (DebugMessages)
            {
                Core.Log.Error($"Ending store loop");
            }

            return Task.FromResult(true);
        }

        public void DoBuy(StoreEntryModel item, MyStoreBlock store, MyFixedPoint quantityInGrid, List<IMyInventory> gridInventories)
        {
            if (!item.BuyFromPlayers) return;
            if (item.BuyFromChanceToAppear < 1 && CrunchEconV3.Core.random.NextDouble() > item.BuyFromChanceToAppear)
            {
                return;
            }

            if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id)) return;

            SerializableDefinitionId itemId = new SerializableDefinitionId(id.TypeId, item.Subtype);
            if (this.OnlyAllowToBuyOrSellNotBoth)
            {
                if (store.PlayerItems.Any(x => x.Item.HasValue && x.Item.Value.TypeId == itemId.TypeId && x.Item.Value.SubtypeId == itemId.SubtypeId))
                {
                    return;
                }
            }
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
                if (result == MyStoreInsertResults.Fail_PricePerUnitIsLessThanMinimum || result == MyStoreInsertResults.Fail_StoreLimitReached)
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

        public void DoSell(StoreEntryModel item, MyStoreBlock store, MyFixedPoint quantityInGrid, List<IMyInventory> gridInventories)
        {
            var skip = false;
            if (!item.SellToPlayers) return;
            if (item.SellToChanceToAppear < 1 && CrunchEconV3.Core.random.NextDouble() > item.SellToChanceToAppear)
            {
                return;
            }

            if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id)) return;

            SerializableDefinitionId itemId = new SerializableDefinitionId(id.TypeId, item.Subtype);

            if (this.OnlyAllowToBuyOrSellNotBoth)
            {
                if (store.PlayerItems.Any(x => x.Item.HasValue && x.Item.Value.TypeId == itemId.TypeId && x.Item.Value.SubtypeId == itemId.SubtypeId))
                {
                    return;
                }
            }
            int price = CrunchEconV3.Core.random.Next((int)item.SellToPlayerPriceMin, (int)item.SellToPlayerPriceMax);

            int amount = CrunchEconV3.Core.random.Next((int)item.AmountToSellMin,
                (int)item.AmountToSellMax);
            int notSpawnedAmount = 0;
            if (!item.IsGas && !item.IsPrefab)
            {
                if (quantityInGrid < amount)
                {
                    if (item.SpawnItemsIfMissing && quantityInGrid < item.SpawnIfBelowThisQuantity)
                    {
                        var amountToSpawn = amount - quantityInGrid;
                        var used = new HashSet<String>();
                        if (id.TypeId.ToString() == "MyObjectBuilder_Datapad")
                        {
                            for (int i = 0; i < amountToSpawn; i++)
                            {
                                var datapadBuilder = BuildDataPad(id.SubtypeName);
                                if (used.Contains(datapadBuilder.Data))
                                {
                                    notSpawnedAmount += 1;
                                    continue;
                                }
                                used.Add(datapadBuilder.Data);
                                var inventory = gridInventories.FirstOrDefault(x =>
                                    x.CanItemsBeAdded(1, new MyItemType(id.TypeId, id.SubtypeId)));
                                if (inventory != null)
                                {
                                    inventory.AddItems(1, datapadBuilder);
                                }

                            }
                            amountToSpawn -= notSpawnedAmount;
                        }
                        else
                        {
                            if (amountToSpawn < 0)
                            {
                                Core.Log.Info($"Quantity to spawn is below 0, the fuck?");
                                return;
                            }
                            if (!CrunchEconV3.Handlers.InventoriesHandler.SpawnItems(id, amountToSpawn, gridInventories))
                            {
                                CrunchEconV3.Core.Log.Error(
                                    $"Unable to spawn items for offer in grid {item.Type} {item.Subtype}");
                            }
                        }


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
            }

            if (amount <= 0)
            {
                return;
            }

            if (item.IsGas)
            {
                long gasId = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                MyStoreItem gasItem = null;
                switch (item.GasSubType.ToLower())
                {
                    case "hydrogen":
                        gasItem = new MyStoreItem(gasId, amount, price, StoreItemTypes.Offer, ItemTypes.Hydrogen);
                        break;
                    case "oxygen":
                        gasItem = new MyStoreItem(gasId, amount, price, StoreItemTypes.Offer, ItemTypes.Oxygen);
                        break;
                }
                gasItem.IsCustomStoreItem = true;
                store.PlayerItems.Add(gasItem);
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
                    if (result == MyStoreInsertResults.Fail_PricePerUnitIsLessThanMinimum || result == MyStoreInsertResults.Fail_StoreLimitReached)
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

        public bool OnlyAllowToBuyOrSellNotBoth = false;
        public DateTime NextRefresh { get; set; }
        public int SecondsBetweenRefresh = 600;

        public bool DebugMessages { get; set; } = false;

        public static MyObjectBuilder_Datapad BuildDataPad(string subtype)
        {
            var station = DatapadHelper.GetRandomStation();
            if (DatapadHelper.DatapadEntriesBySubtypes.TryGetValue(subtype, out var lists))
            {
                var datapadBuilder = new MyObjectBuilder_Datapad() { SubtypeName = subtype };
                var randomStation =
                    datapadBuilder.Data = lists.GetRandomItemFromList()
                        .Replace("{StationGps}", station);
                return datapadBuilder;
            }
            var datapadBuilder2 = new MyObjectBuilder_Datapad() { SubtypeName = "Datapad" };
            datapadBuilder2.Data = "Subtype for datapad not found! report to admins.";
            return datapadBuilder2;
        }
    }

    public static class DatapadHelper
    {
        public static Dictionary<string, List<string>>
            DatapadEntriesBySubtypes = new Dictionary<string, List<string>>();

        public static void Setup()
        {
            FileUtils utils = new FileUtils();
            var path = $"{Core.path}Datapads.json";
            if (File.Exists(path))
            {
                DatapadEntriesBySubtypes = utils.ReadFromJsonFile<Dictionary<string, List<string>>>(path);
            }
            else
            {
                DatapadEntriesBySubtypes.Add("Datapad", new List<string>() { "{StationGps}", "Hello! {StationGps}" });
                utils.WriteToJsonFile(path, DatapadEntriesBySubtypes);
            }
        }

        public static string GetRandomStation()
        {
            List<Tuple<Vector3D, long>> availablePositions = new List<Tuple<Vector3D, long>>();

            // If it's not a custom station, get random keen ones
            var stations = Core.StationStorage.GetAll()
                    .Where(x => x.UseAsDeliveryLocation)
                    .ToList();

            foreach (var station in stations)
            {
                var foundFaction = MySession.Static.Factions.TryGetFactionByTag(station.FactionTag);
                var GPS = GPSHelper.ScanChat(station.LocationGPS);
                availablePositions.Add(Tuple.Create(GPS.Coords, foundFaction.FactionId));
            }


            if (MySession.Static.Settings.EnableEconomy)
            {
                var positions = MySession.Static.Factions.GetNpcFactions()
                    .Where(x => x.Stations.Any())
                    .SelectMany(x => x.Stations)
                    .Select(x => Tuple.Create(x.Position, x.FactionId))
                    .ToList();
                availablePositions.AddRange(positions);
            }

            var chosen = availablePositions.GetRandomItemFromList();
            var faction = MySession.Static.Factions.TryGetFactionById(chosen.Item2);
            var gps = new MyGps()
            {
                Coords = chosen.Item1,
                Name = $"{faction.Tag} - Station",
                DisplayName = $"{faction.Tag} - Station",
            };
            return gps.ToString();
        }
    }

    public class StoreEntryModel
    {
        public bool IsPrefab = false;
        public bool IsGas = false;
        public string GasSubType = "Hydrogen";
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
                    MappedBlockNames[name] = utils.ReadFromJsonFile<List<StoreEntryModel>>(file);
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
