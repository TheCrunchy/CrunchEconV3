using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlay;
using CrunchEconV3.PlugAndPlay.Extensions;
using CrunchEconV3.PlugAndPlay.Helpers;
using CrunchEconV3.PlugAndPlayV2.Helpers;
using CrunchEconV3.PlugAndPlayV2.Interfaces;
using CrunchEconV3.PlugAndPlayV2.Models;
using CrunchEconV3.PlugAndPlayV2.StationSpawnStrategies;
using CrunchEconV3.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage;
using VRage.Game;
using VRage.Game.Definitions.SessionComponents;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.GameServices;
using VRage.Groups;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace CrunchEconV3.PlugAndPlayV2.StationLogics
{
    [Category("econv3")]
    public class StoreLogicCommands : CommandModule
    {
        [Command("reloadstores", "export the orders and offers in a store block to store file")]
        [Permission(MyPromoteLevel.Admin)]
        public void ReloadStores()
        {
            Context.Respond("Loading the files");
            StoreFileHelper.LoadTheFiles();
            Context.Respond("Loaded the files");
        }

        [Command("store", "testing faction definitions")]
        [Permission(MyPromoteLevel.Admin)]
        public void Easy()
        {
            List<MyFactionTypeDefinition> list = MyDefinitionManager.Static.GetAllDefinitions<MyFactionTypeDefinition>().ToList<MyFactionTypeDefinition>();
            Context.Respond($"{list.Count}");
            foreach (var item in list)
            {
                Context.Respond($"{item.OrdersList.Length}");
                Context.Respond($"{item.OffersList.Length}");
            }
        }

        [Command("prefab", "spawn a random prefab for testing")]
        [Permission(MyPromoteLevel.Admin)]
        public void EasyStore()
        {
            var stationName = KeenStationPrefabHelper.GetRandomStationPrefabName(MyStationTypeEnum.Outpost);
            Core.Log.Info(stationName);



            var planets = MyPlanets.GetPlanets();
            MyPlanet lowestDistancePlanet = null;
            var lowestDistance = 0f;
            foreach (var planet in planets)
            {
                var planetPosition = planet.PositionComp.GetPosition();
                var distance = Vector3.Distance(planetPosition, Context.Player.Character.PositionComp.GetPosition());
                if (lowestDistance == 0)
                {
                    lowestDistance = distance;
                    lowestDistancePlanet = planet;
                }

                if (distance < lowestDistance)
                {
                    lowestDistance = distance;
                    lowestDistancePlanet = planet;
                }
            }

            IStationSpawnStrategy strategy = null;
            strategy = new PlanetSpawnStrategy();
            var spawned = strategy.SpawnStations(
                new List<MyFaction>() { MySession.Static.Factions.GetPlayerFaction(Context.Player.IdentityId), MySession.Static.Factions.GetPlayerFaction(Context.Player.IdentityId) },
                "BaseTemplate", 3);

            foreach (var item in spawned)
            {
                var gps = GPSHelper.ScanChat(item.LocationGPS);
                gps.Name = "Planetary Spawn";
                gps.GPSColor = Color.Cyan;
                gps.AlwaysVisible = true;
                gps.ShowOnHud = true;
                MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
                gpscol.SendAddGpsRequest(Context.Player.IdentityId, ref gps);
            }
            Context.Respond($"{spawned.Count} Planet Stations Spawned");
            strategy = new FurtherOrbitalSpawnStrategy();
            spawned = strategy.SpawnStations(
                 new List<MyFaction>() { MySession.Static.Factions.GetPlayerFaction(Context.Player.IdentityId), MySession.Static.Factions.GetPlayerFaction(Context.Player.IdentityId) },
                 "MiningTemplate", 2);

            foreach (var item in spawned)
            {
                var gps = GPSHelper.ScanChat(item.LocationGPS);
                gps.Name = "Deep Space Spawn";
                gps.GPSColor = Color.Cyan;
                gps.AlwaysVisible = true;
                gps.ShowOnHud = true;
                MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
                gpscol.SendAddGpsRequest(Context.Player.IdentityId, ref gps);
            }

            Context.Respond($"{spawned.Count} Deep Space Stations Spawned");

            strategy = new OrbitalSpawnStrategy();
            spawned = strategy.SpawnStations(
               new List<MyFaction>() { MySession.Static.Factions.GetPlayerFaction(Context.Player.IdentityId), MySession.Static.Factions.GetPlayerFaction(Context.Player.IdentityId) },
               "BaseTemplate", 2);

            foreach (var item in spawned)
            {

                var gps = GPSHelper.ScanChat(item.LocationGPS);
                gps.Name = "Orbital Spawn";
                gps.GPSColor = Color.Cyan;
                gps.AlwaysVisible = true;
                gps.ShowOnHud = true;
                MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
                gpscol.SendAddGpsRequest(Context.Player.IdentityId, ref gps);
            }


            Context.Respond($"{spawned.Count} Orbital Space Stations Spawned");
            Core.StationStorage.LoadAll();
        }

    }

    public class StoreLogic : IStationLogic
    {
        public string StoreFileName;
        public bool IsFirstRun { get; set; } = true;
        public DateTime NextModifierReset { get; set; }
        public DateTime NextStoreRefresh { get; set; }
        public DateTime NextInventoryRefresh { get; set; }
        public double Modifier { get; set; }
        public double MaximumModifier { get; set; } = 0.15;
        public int Priority { get; set; }
        public int DaysBetweenModifierResets { get; set; } = 7;
        public bool MaintainBalance { get; set; } = true;
        public int SecondsBetweenRefresh { get; set; } = 3600;

        public void Setup()
        {

        }

        public static List<VRage.Game.ModAPI.IMyInventory> GetInventories(MyCubeGrid grid)
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = new List<VRage.Game.ModAPI.IMyInventory>();
            var gridOwnerFac = FacUtils.GetOwner(grid);

            foreach (var block in grid.GetFatBlocks().OfType<MyCargoContainer>().Where(x => x.OwnerId == gridOwnerFac))
            {
                for (int i = 0; i < block.InventoryCount; i++)
                {
                    VRage.Game.ModAPI.IMyInventory inv = ((VRage.Game.ModAPI.IMyCubeBlock)block).GetInventory(i);
                    inventories.Add(inv);
                }
            }
            return inventories;
        }
        public List<VRage.Game.ModAPI.IMyInventory> ClearInventories(MyCubeGrid grid)
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = GetInventories(grid);

            foreach (var inv in inventories)
            {
                inv.Clear();
            }
            return inventories;
        }

        public Task<bool> DoLogic(MyCubeGrid grid)
        {
            if (IsFirstRun)
            {
                FileUtils utils = new FileUtils();
                List<MyFactionTypeDefinition> list = MyDefinitionManager.Static.GetAllDefinitions<MyFactionTypeDefinition>().ToList<MyFactionTypeDefinition>();
                var listToUse = list.GetRandomItemFromList();
                var randomInt = Core.random.Next(1, 4);
                StoreFileName = $"{listToUse.Id.SubtypeName}-{randomInt}.json";
                var path = $"{Core.path}/TemplatedStores/{StoreFileName}";
                if (!File.Exists(path))
                {
                    var newStoreList = new StoreLists();
                    //file doesnt exist, lets generate it 
                    CreateSellingItems(listToUse, newStoreList);
                    CreateBuyingItems(listToUse, newStoreList);
                    StoreFileHelper.SaveFile(newStoreList, StoreFileName);

                }
            }
            if (DateTime.Now >= NextModifierReset)
            {
                NextModifierReset = DateTime.Now.AddDays(DaysBetweenModifierResets);
                var minimum = MaximumModifier * -1;

                double randomNumber = minimum + (Core.random.NextDouble() * (MaximumModifier - minimum));
                Modifier = randomNumber;
            }

            if (DateTime.Now < NextStoreRefresh)
            {
                return Task.FromResult(true);
            }

            NextStoreRefresh = DateTime.Now.AddSeconds(SecondsBetweenRefresh);

            var owner = grid.GetGridOwner();
            if (MaintainBalance)
            {
                var balance = EconUtils.getBalance(owner);
                if (balance < 1000000000000)
                {
                    EconUtils.addMoney(owner, 1000000000000 - balance);
                }
            }

            foreach (var battery in grid.GetFatBlocks().OfType<MyBatteryBlock>()
                         .Where(x => x.OwnerId == owner))
            {
                battery.CurrentStoredPower = battery.MaxStoredPower;
            }

            var inventories = GetInventories(grid);
            ClearInventories(grid);

            foreach (var store in grid.GetFatBlocks().OfType<MyStoreBlock>().Where(x => x.OwnerId == owner))
            {
                ClearStoreOfPlayersBuyingOffers(store);
                var items = GetStoreItems(store);
                if (items == null)
                {
                    continue;
                }
                foreach (var item in items.SellingToPlayers)
                {
                    if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id))
                    {
                        CrunchEconV3.Core.Log.Error($"{item.Type} {item.Subtype} not a valid id");
                        continue;
                    };

                    try
                    {
                        DoSell(item, store, inventories);
                        if (items.SellHydrogen)
                        {
                            //add later 
                        }

                        if (items.SellOxygn)
                        {
                            //add later 
                        }
                    }
                    catch (Exception e)
                    {
                        CrunchEconV3.Core.Log.Error(e);
                    }

                }
                foreach (var item in items.BuyingFromPlayers)
                {
                    if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id))
                    {
                        CrunchEconV3.Core.Log.Error($"{item.Type} {item.Subtype} not a valid id");
                        continue;
                    };

                    try
                    {
                        DoBuy(item, store, inventories);
                    }
                    catch (Exception e)
                    {
                        CrunchEconV3.Core.Log.Error(e);
                    }

                }

            }
            return Task.FromResult(true);
        }

        private StoreLists? GetStoreItems(MyStoreBlock Store)
        {
            if (Store.CustomData.Any())
            {
                var name = Store.CustomData;
                var attempted = StoreFileHelper.GetList(name);
                if (attempted != null)
                {
                    return attempted;
                }
            }

            var fromFile = StoreFileHelper.GetList(StoreFileName);
            if (fromFile != null)
            {
                return fromFile;
            }

            return null;


        }

        private void DoSell(StoreEntryModel Item, MyStoreBlock Store, List<IMyInventory> Inventories)
        {
            if (Item.ChanceToAppear < 1 && CrunchEconV3.Core.random.NextDouble() > Item.ChanceToAppear)
            {
                return;
            }

            if (!MyDefinitionId.TryParse(Item.Type, Item.Subtype, out MyDefinitionId id)) return;

            SerializableDefinitionId itemId = new SerializableDefinitionId(id.TypeId, Item.Subtype);

            var pricing = PriceHelper.GetPriceModel($"{Item.Type}/{Item.Subtype}");
            if (pricing.NotFound)
            {
                Core.Log.Info("Price not found");
                return;
            }

            var actualPrice = pricing.GetBuyMinAndMaxPrice();
            int calcedPrice = Core.random.Next((int)actualPrice.Item1, (int)actualPrice.Item2);
            var modifier = calcedPrice * Modifier;
            calcedPrice += (int)modifier;
            if (Modifier > 0)
            {
                calcedPrice += (int)modifier;
            }
            else
            {
                calcedPrice -= (int)modifier;
            }
            int amount = CrunchEconV3.Core.random.Next((int)Item.AmountMin,
                (int)Item.AmountMax);
            int notSpawnedAmount = 0;

            var amountToSpawn = amount;
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
                    var inventory = Inventories.FirstOrDefault(x =>
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
                if (!CrunchEconV3.Handlers.InventoriesHandler.SpawnItems(id, amountToSpawn, Inventories))
                {
                    CrunchEconV3.Core.Log.Error(
                        $"Unable to spawn items for offer in grid {Item.Type} {Item.Subtype}");
                }
            }

            if (amount <= 0)
            {
                return;
            }

            MyStoreItemData itemInsert =
                new MyStoreItemData(itemId, amount, calcedPrice,
                    null, null);

            MyStoreInsertResults result =
                Store.InsertOffer(itemInsert,
                    out long notUsingThis);
            if (result != MyStoreInsertResults.Success)
            {
                if (result == MyStoreInsertResults.Fail_PricePerUnitIsLessThanMinimum || result == MyStoreInsertResults.Fail_StoreLimitReached)
                {
                    long newid = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                    MyStoreItem myStoreItem = new MyStoreItem(newid, amount, calcedPrice, StoreItemTypes.Offer, ItemTypes.PhysicalItem);
                    myStoreItem.Item = itemId;
                    Store.PlayerItems.Add(myStoreItem);
                }
                else
                {
                    CrunchEconV3.Core.Log.Error($"Unable to insert this order into store {Item.Type} {Item.Subtype} Amount:{itemInsert.Amount} Price:{itemInsert.PricePerUnit} {result.ToString()}");
                }

            }


        }

        private void DoBuy(StoreEntryModel Item, MyStoreBlock Store, List<IMyInventory> Inventories)
        {
            if (Item.ChanceToAppear < 1 && CrunchEconV3.Core.random.NextDouble() > Item.ChanceToAppear)
            {
                return;
            }

            if (!MyDefinitionId.TryParse(Item.Type, Item.Subtype, out MyDefinitionId id)) return;

            SerializableDefinitionId itemId = new SerializableDefinitionId(id.TypeId, Item.Subtype);
            var pricing = PriceHelper.GetPriceModel($"{Item.Type}/{Item.Subtype}");
            if (pricing.NotFound)
            {
                Core.Log.Info("Price not found");
                return;
            }

            int amount = CrunchEconV3.Core.random.Next((int)Item.AmountMin,
                (int)Item.AmountMax);

            var actualPrice = pricing.GetSellMinAndMaxPrice();
            int calcedPrice = Core.random.Next((int)actualPrice.Item1, (int)actualPrice.Item2);
            var modifier = calcedPrice * Modifier;
            if (Modifier > 0)
            {
                calcedPrice += (int)modifier;
            }
            else
            {
                calcedPrice -= (int)modifier;
            }


            MyStoreItemData itemInsert =
                new MyStoreItemData(itemId, amount, calcedPrice,
                    null, null);

            MyStoreInsertResults result =
                Store.InsertOrder(itemInsert,
                    out long notUsingThis);

            if (result != MyStoreInsertResults.Success)
            {
                if (result == MyStoreInsertResults.Fail_PricePerUnitIsLessThanMinimum || result == MyStoreInsertResults.Fail_StoreLimitReached)
                {
                    long newid = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                    MyStoreItem myStoreItem = new MyStoreItem(newid, amount, calcedPrice, StoreItemTypes.Order, ItemTypes.PhysicalItem);
                    myStoreItem.Item = itemId;
                    Store.PlayerItems.Add(myStoreItem);
                }
                else
                {
                    CrunchEconV3.Core.Log.Error($"Unable to insert this order into store {Item.Type} {Item.Subtype} Amount:{itemInsert.Amount} Price:{itemInsert.PricePerUnit} {result.ToString()}");
                }
            }
        }

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

        private void CreateBuyingItems(MyFactionTypeDefinition listToUse, StoreLists newStoreList)
        {
            foreach (var item in listToUse.OrdersList)
            {
                if (Core.random.Next(0, 2) == 1)
                {
                    continue;
                }

                if (!MyDefinitionId.TryParse(item.TypeIdString, item.SubtypeId, out MyDefinitionId id)) continue;
                var definition = MyDefinitionManager.Static.GetDefinition(id);
                var chanceToAppear = 0.3f + (float)Core.random.NextDouble();

                if (definition is MyPhysicalItemDefinition physicalItem)
                {
                    var model = new StoreEntryModel()
                    {
                        AmountMax = physicalItem.MaximumOrderAmount,
                        AmountMin = physicalItem.MinimumOrderAmount,
                        ChanceToAppear = chanceToAppear > 1 ? (chanceToAppear - 0.3f) : chanceToAppear,
                        Type = item.TypeIdString,
                        Subtype = item.SubtypeId
                    };
                    newStoreList.BuyingFromPlayers.Add(model);
                }
            }
        }

        private void CreateSellingItems(MyFactionTypeDefinition listToUse, StoreLists newStoreList)
        {
            foreach (var item in listToUse.OffersList)
            {
                if (Core.random.Next(0, 2) == 1)
                {
                    continue;
                }

                if (!MyDefinitionId.TryParse(item.TypeIdString, item.SubtypeId, out MyDefinitionId id)) continue;
                var definition = MyDefinitionManager.Static.GetDefinition(id);
                var chanceToAppear = 0.3f + (float)Core.random.NextDouble();

                if (definition is MyPhysicalItemDefinition physicalItem)
                {
                    var model = new StoreEntryModel()
                    {
                        AmountMax = physicalItem.MaximumOfferAmount,
                        AmountMin = physicalItem.MinimumOfferAmount,
                        ChanceToAppear = chanceToAppear > 1 ? (chanceToAppear - 0.3f) : chanceToAppear,
                        Type = item.TypeIdString,
                        Subtype = item.SubtypeId
                    };
                    newStoreList.SellingToPlayers.Add(model);
                }
            }
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
    }
}
