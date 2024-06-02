using CrunchEconV3.Handlers;
using CrunchEconV3.PlugAndPlay.Extensions;
using CrunchEconV3.PlugAndPlay.Helpers;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;

namespace CrunchEconV3.PlugAndPlay
{
    [Category("keen")]
    public class KeenEconCommands : CommandModule
    {
        [Command("forcetick", "force an econ tick")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportStore()
        {
            KeenStoreManagement.ForceTick();
            Context.Respond("Econ tick forced.");
        }

        [Command("toggle", "force an econ tick")]
        [Permission(MyPromoteLevel.Admin)]
        public void SetUpdatingStoreFiles()
        {
            KeenStoreManagement.UpdatingStoreFiles = !KeenStoreManagement.UpdatingStoreFiles;
            Context.Respond($"Forcing economy ticks every 5 seconds set to {KeenStoreManagement.UpdatingStoreFiles}");
        }
    }

    [PatchShim]
    public static class KeenStoreManagement
    {
        public static bool UpdatingStoreFiles = false;
        internal static readonly MethodInfo updateMethod =
            typeof(MySessionComponentEconomy).GetMethod("UpdateStations",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo updatePatch =
            typeof(KeenStoreManagement).GetMethod(nameof(Update), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Adding keen patch");
            ctx.GetPattern(updateMethod).Prefixes.Add(updatePatch);

            LoadStores();

        }

        private static FileUtils Utils = new FileUtils();
        public static void ForceTick()
        {
            var econComp = MySession.Static.GetComponent<MySessionComponentEconomy>();
            econComp.ForceEconomyTick();
        }

        public static Dictionary<string, List<MyStoreItem>> MappedFactions = new Dictionary<string, List<MyStoreItem>>();

        public static Dictionary<string, List<StoreEntryModel>> MappedStoreNames =
            new Dictionary<string, List<StoreEntryModel>>();

        public static bool Update()
        {
            if (UpdatingStoreFiles)
            {
                Directory.CreateDirectory($"{Core.path}/KeenStoreConfigs/");
            }

            foreach (KeyValuePair<long, MyFaction> faction in MySession.Static.Factions)
            {
                foreach (MyStation station in faction.Value.Stations)
                {
                    if (station.StoreItems == null)
                    {
                       Core.Log.Info("Well i fucked this");
                       continue;
                    }
                    
                    DoItemMapping(faction, station);

                    if (Core.config.OverrideKeenStores)
                    {
                        station.StoreItems.Clear();
                        var useThis = Core.config.KeenNPCStoresOverrides.FirstOrDefault(x =>
                            x.NPCFactionTags.Contains(faction.Value.Tag));
                        var storesToUse = new List<StoreEntryModel>();
                        if (useThis != null)
                        {
                            storesToUse = MappedStoreNames[useThis.StoreFileName];
                        }
                        else
                        {
                            if (MappedStoreNames.TryGetValue($"{faction.Value.FactionType.ToString()}_{station.Type.ToString()}_stores", out var items))
                            {
                                storesToUse = items;
                            }
                        }

                        MapStores(storesToUse, station);

                    }

                    StationHandler.SetNPCNeedsRefresh(station.StationEntityId, DateTime.Now.AddSeconds(MyAPIGateway.Session.SessionSettings.EconomyTickInSeconds));
                }
                if (UpdatingStoreFiles)
                {
                    Task.Run(() =>
                    {

                        foreach (var item in MappedFactions)
                        {
                            var remapped = new List<StoreEntryModel>();
                            foreach (var storeEntry in item.Value)
                            {
                                remapped.Add(MapItem(storeEntry, new StoreEntryModel()));
                            }
                            Utils.WriteToJsonFile($"{Core.path}/KeenStoreConfigs/{item.Key}_stores.json", remapped);
                        }
                        PriceHelper.SavePrices();
                    });

                }

            }

            if (Core.config.OverrideKeenStores)
            {
                return false;
            }

            return true;
        }

        private static void MapStores(List<StoreEntryModel> storesToUse, MyStation station)
        {
            station.StoreItems.Clear();
            foreach (var item in storesToUse)
            {
                if (item.ChanceToAppear < 1)
                {
                    var random = CrunchEconV3.Core.random.NextDouble();
                    if (random > item.ChanceToAppear)
                    {
                        continue;
                    }
                }
                
                if (item.AmountMin > item.AmountMax)
                {
                    (item.AmountMin, item.AmountMax) = (item.AmountMax, item.AmountMin);
                }
                var amount = Core.random.Next(item.AmountMin, item.AmountMax);
                if (amount <= 0)
                {
                    amount = 1;
                }
                var insertThis = new MyStoreItem();
               var newId = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                if (item.IsGas)
                {
                    insertThis.ItemType = item.GasSubType switch
                    {
                        "Hydrogen" => ItemTypes.Hydrogen,
                        "Oxygen" => ItemTypes.Oxygen,
                        _ => insertThis.ItemType
                    };

                    var price = PriceHelper.GetPriceModel($"MyObjectBuilder_GasProperties/{item.GasSubType}");
                    if (price.NotFound)
                    {
                        continue;
                    }

                    AssignPricing(item, insertThis, price);
                    insertThis.IsCustomStoreItem = true;
                }

                if (item.IsPrefab)
                {
                    insertThis.ItemType = ItemTypes.Grid;
                    insertThis.IsCustomStoreItem = true;
                    insertThis.PrefabName = item.PrefabSubType;
                    var price = PriceHelper.GetPriceModel(item.PrefabSubType);
                    if (price.NotFound)
                    {
                        Core.Log.Info("Prefab price not found");
                        continue;
                    }
                    AssignPricing(item, insertThis, price);
                }

                if (!item.IsPrefab && !item.IsGas)
                {
                    if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id)) return;
                    insertThis.ItemType = ItemTypes.PhysicalItem;
                    SerializableDefinitionId itemId = new SerializableDefinitionId(id.TypeId, id.SubtypeName);
                    if (itemId.IsNull())
                    {
                        continue;
                    }
                    insertThis.Item = new SerializableDefinitionId?(itemId);
                    var pricing = PriceHelper.GetPriceModel($"{item.Type}/{item.Subtype}");
                    if (pricing.NotFound)
                    {
                        Core.Log.Info("Price not found");
                        continue;
                    }
                    AssignPricing(item, insertThis, pricing);
                }
                var discountChance = CrunchEconV3.Core.random.NextDouble();
                if (discountChance <= 0.10)
                {
                    insertThis.PricePerUnitDiscount = (float)((Core.random.NextDouble() * 0.15));
                    continue;
                }

                insertThis.Id = newId;
                insertThis.Amount = amount;
                station.StoreItems.Add(insertThis);
            }
        }

        private static void AssignPricing(StoreEntryModel item, MyStoreItem insertThis, PriceModel model)
        {
            switch (item.SaleType.ToLower())
            {
                case "offer":
                    {
                        insertThis.StoreItemType = StoreItemTypes.Offer;
                        var price = model.GetBuyMinAndMaxPrice();
                        insertThis.PricePerUnit = Core.random.Next((int)price.Item1, (int)price.Item2);
                    }
                    break;
                case "order":
                    {
                        insertThis.StoreItemType = StoreItemTypes.Order;
                        var price = model.GetSellMinAndMaxPrice();
                        insertThis.PricePerUnit = Core.random.Next((int)price.Item1, (int)price.Item2);
                    }
                    break;
            }
        }

        private static void DoItemMapping(KeyValuePair<long, MyFaction> faction, MyStation station)
        {
            if (UpdatingStoreFiles)
            {
                if (MappedFactions.TryGetValue($"{faction.Value.FactionType.ToString()}_{station.Type.ToString()}", out var items))
                {
                    MapItems(station, items);
                }
                else
                {
                    var newItems = new List<MyStoreItem>();
                    MapItems(station, newItems);
                    MappedFactions[$"{faction.Value.FactionType.ToString()}_{station.Type.ToString()}"] = newItems;
                }
            }
        }

        public class StoreEntryModel
        {
            public bool IsPrefab = false;
            public bool IsGas = false;
            public string GasSubType = "";
            public string PrefabSubType = "";
            public string Type { get; set; } = "";
            public string Subtype { get; set; } = "";
            public float ChanceToAppear = 0.5f;
            public int AmountMin { get; set; } = 100;
            public int AmountMax { get; set; } = 150;
            public string SaleType { get; set; }
        }

        private static StoreEntryModel MapItem(MyStoreItem thing, StoreEntryModel stored)
        {
            stored.SaleType = thing.StoreItemType.ToString();
            stored.AmountMax = (int)(thing.Amount * 1.2);
            stored.AmountMin = (int)(thing.Amount * 0.8);
            if (thing.ItemType == ItemTypes.Oxygen)
            {
                stored.IsGas = true;
                stored.GasSubType = "Oxygen";
            }
            if (thing.ItemType == ItemTypes.Hydrogen)
            {
                stored.IsGas = true;
                stored.GasSubType = "Hydrogen";
            }
            if (thing.ItemType == ItemTypes.Grid)
            {
                PriceHelper.InsertPrice(thing.PrefabName, thing.PricePerUnit);
                stored.IsPrefab = true;
                stored.PrefabSubType = thing.PrefabName;
                stored.ChanceToAppear = 0.2f;
            }

            if (thing.StoreItemType == StoreItemTypes.Offer && thing.ItemType == ItemTypes.PhysicalItem)
            {

                stored.Type = thing.Item?.TypeIdString;
                stored.Subtype = thing.Item?.SubtypeId;
            }

            if (thing.StoreItemType == StoreItemTypes.Order && thing.ItemType == ItemTypes.PhysicalItem)
            {

                stored.Type = thing.Item?.TypeIdString;
                stored.Subtype = thing.Item?.SubtypeId;
            }

            return stored;
        }

        private static void MapItems(MyStation station, List<MyStoreItem> items)
        {
            if (station.StoreItems == null || !station.StoreItems.Any())
            {
                return;
            }
            foreach (var item in station.StoreItems.Where(x => x.ItemType == ItemTypes.PhysicalItem))
            {
                if (!items.Any(x =>
                        x.Item?.ToString() == item.Item?.ToString() && x.Item != null && x.StoreItemType == item.StoreItemType && x.ItemType == ItemTypes.PhysicalItem))
                {
                    if (item.Amount != 0)
                    {
                        items.Add(item.Clone());
                    }
                }
            }

            foreach (var item in station.StoreItems.Where(x => x.ItemType == ItemTypes.Grid))
            {
                if (!items.Any(x => x.PrefabName == item.PrefabName && x.PrefabName != null))
                {
                    items.Add(item.Clone());
                }
            }

            foreach (var item in station.StoreItems.Where(x => x.ItemType == ItemTypes.Hydrogen))
            {
                if (!items.Any(x => x.ItemType == ItemTypes.Hydrogen && x.StoreItemType == item.StoreItemType))
                {
                    items.Add(item.Clone());
                }
            }

            foreach (var item in station.StoreItems.Where(x => x.ItemType == ItemTypes.Oxygen))
            {
                if (!items.Any(x => x.ItemType == ItemTypes.Oxygen && x.StoreItemType == item.StoreItemType))
                {
                    items.Add(item.Clone());
                }
            }
        }

        public static void LoadStores()
        {
            Directory.CreateDirectory($"{Core.path}/KeenStoreConfigs/");
            foreach (var file in Directory.GetFiles($"{Core.path}/KeenStoreConfigs/", "*",
                         SearchOption.AllDirectories))
            {
                var parsed = Utils.ReadFromJsonFile<List<StoreEntryModel>>(file);
                if (parsed != null)
                {
                    MappedStoreNames[Path.GetFileNameWithoutExtension(file)] = parsed;
                }
            }
        }
    }
}
