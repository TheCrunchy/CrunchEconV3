using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class TradeOperatorsStationFaker
    {
        public static HashSet<String> TOCFacTags = new HashSet<String>() { "TAG1", "TAG2", "ETC" };
        public static List<string> BannedItems = new List<string>() { "Ingot/Banana", "Ore/Banana" };
        public static void Patch(PatchContext ctx)
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            Core.Fakes.Clear();
            // Iterate through all existing grids when the mod initializes
            var grids = new List<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity is IMyCubeGrid grid)
                {
                    OnEntityAdd(entity);
                }
                return false;
            });

            ctx.GetPattern(insert).Prefixes.Add(insertPatch);
        }

        private static void OnEntityAdd(IMyEntity entity)
        {
            if (entity is MyCubeGrid grid)
            {
                if (!grid.IsStatic)
                {
                    return;
                }
                if (grid.EntityId != grid.GetBiggestGridInGroup().EntityId)
                {
                    return;
                }

                var owner = FacUtils.GetOwner((Sandbox.Game.Entities.MyCubeGrid)grid);
                var faction = FacUtils.GetPlayersFaction(owner);
                if (faction != null && TOCFacTags.Contains(faction.Tag))
                {
                    if (grid.GetFatBlocks().OfType<MyContractBlock>().Any())
                    {

                        var fake = new StationConfig();
                        fake.SetUsesDefault();

                        var gps = new MyGps();
                        fake.FactionTag = faction.Tag;
                        gps.Name = "Fake Station";
                        gps.Coords = grid.GetBiggestGridInGroup().PositionComp.GetPosition();
                        fake.LocationGPS = gps.ToString();
                        fake.SetFake();
                        fake.UsesDefault = true;
                        fake.SetGrid(grid.GetBiggestGridInGroup());
                        fake.FileName = grid.GetBiggestGridInGroup().DisplayName;
                        Core.Fakes.Add(fake);
                        Core.Log.Info("Adding a fake");
                    }
                }
            }
        }
        internal static readonly MethodInfo insert =
       typeof(MyStoreBlock).GetMethod("InsertStoreItem", BindingFlags.Instance | BindingFlags.Public) ??
       throw new Exception("Failed to find patch method InsertStoreItem");

        internal static readonly MethodInfo insertPatch =
            typeof(TradeOperatorsStationFaker).GetMethod(nameof(StorePatchMethod), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static Boolean StorePatchMethod(MyStoreBlock __instance, IMyStoreItem item)
        {
            var owner = FacUtils.GetOwner(__instance.CubeGrid);
            var owningFac = FacUtils.GetPlayersFaction(owner);
            if (owningFac == null)
            {
                return true;
            }

            if (TOCFacTags.Contains(owningFac.Tag))
            {
                MyStoreItem storeItem = (MyStoreItem)null;
                if (storeItem.IsCustomStoreItem)
                {
                    return true;
                }
                Core.Log.Info($"Checking if banned {storeItem.Item.Value.TypeIdString.Replace("MyObjectBuilder_", "")}{storeItem.Item.Value.SubtypeId}");
                if (BannedItems.Contains($"{storeItem.Item.Value.TypeIdString.Replace("MyObjectBuilder_", "")}{storeItem.Item.Value.SubtypeId}"))
                {
                    Core.Log.Info("Not allowing item");
                    return false;
                }

                return true;
            }

            return true;
        }
    }
}
