using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Utils;
using Sandbox.Definitions;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;

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
        }

        private static FileUtils Utils = new FileUtils();
        public static void ForceTick()
        {
            var econComp = MySession.Static.GetComponent<MySessionComponentEconomy>();
            econComp.ForceEconomyTick();
        }

        public static Dictionary<string, List<MyStoreItem>> MappedFactions = new Dictionary<string, List<MyStoreItem>>();
        public static bool Update()
        {
            if (UpdatingStoreFiles)
            {
                Directory.CreateDirectory($"{Core.path}/StoreConfigs/");
            }

            foreach (KeyValuePair<long, MyFaction> faction in MySession.Static.Factions)
            {
                foreach (MyStation station in faction.Value.Stations)
                {
                   
                    if (UpdatingStoreFiles)
                    {
                        if (MappedFactions.TryGetValue(faction.Value.FactionType.ToString(), out var items))
                        {
                            MapItems(station, items);
                        }
                        else
                        {
                            var newItems = new List<MyStoreItem>();
                            MapItems(station, newItems);
                            MappedFactions[faction.Value.FactionType.ToString()] = newItems;
                        }
                    }

                   
                    StationHandler.SetNPCNeedsRefresh(station.StationEntityId, DateTime.Now.AddSeconds(MyAPIGateway.Session.SessionSettings.EconomyTickInSeconds));
                }

                if (UpdatingStoreFiles)
                {
                    Task.Run(() =>
                    {

                        foreach (var item in MappedFactions)
                        {
                            Utils.WriteToJsonFile($"{Core.path}/StoreConfigs/{item.Key}_stores.json", item.Value);
                        }
                    });
                }
              
            }

            return true;
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
                    items.Add(item);
                }
            }

            foreach (var item in station.StoreItems.Where(x => x.ItemType == ItemTypes.Grid))
            {
                if (!items.Any(x => x.PrefabName == item.PrefabName && x.PrefabName != null))
                {
                    items.Add(item);
                }
            }

            foreach (var item in station.StoreItems.Where(x => x.ItemType == ItemTypes.Hydrogen))
            {
                if (!items.Any(x => x.ItemType == ItemTypes.Hydrogen && x.StoreItemType == item.StoreItemType))
                {
                    items.Add(item);
                }
            }

            foreach (var item in station.StoreItems.Where(x => x.ItemType == ItemTypes.Oxygen))
            {
                if (!items.Any(x => x.ItemType == ItemTypes.Oxygen && x.StoreItemType == item.StoreItemType))
                {
                    items.Add(item);
                }
            }
        }
    }
}
