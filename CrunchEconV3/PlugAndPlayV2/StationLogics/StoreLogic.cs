using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlay;
using CrunchEconV3.PlugAndPlay.Extensions;
using CrunchEconV3.PlugAndPlayV2.Helpers;
using CrunchEconV3.PlugAndPlayV2.Interfaces;
using CrunchEconV3.PlugAndPlayV2.Models;
using CrunchEconV3.PlugAndPlayV2.StationSpawnStrategies;
using CrunchEconV3.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.GameServices;
using VRage.Groups;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.PlugAndPlayV2.StationLogics
{
    [Category("econv3")]
    public class StoreLogicCommands : CommandModule
    {
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
                 "BaseTemplate", 4);

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
               "BaseTemplate", 3);

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

        }

    }

    public class StoreLogic : IStationLogic
    {
        public DateTime NextModifierReset { get; set; }
        public DateTime NextStoreRefresh { get; set; }
        public DateTime NextInventoryRefresh { get; set; }
        public double Modifier { get; set; }
        public double MaximumModifier { get; set; } = 0.15;
        public int Priority { get; set; }
        public int DaysBetweenModifierResets { get; set; } = 7;
        public bool MaintainBalance { get; set; } = true;
        public int SecondsBetweenRefresh { get; set; } = 120;
        public int SecondsBetweenInventoryRefresh { get; set; } = 3600;

        public void Setup()
        {
            throw new NotImplementedException();
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

        public void ClearInventoriesOfThingsItDoesntSell(MyCubeGrid grid, Dictionary<MyDefinitionId, int> itemsToRemove)
        {
            List<VRage.Game.ModAPI.IMyInventory> inventories = GetInventories(grid);

            InventoriesHandler.ConsumeComponents(inventories, itemsToRemove, 0l);

        }

        public Task<bool> DoLogic(MyCubeGrid grid)
        {
            if (DateTime.Now >= NextModifierReset)
            {
                NextModifierReset = DateTime.Now.AddDays(DaysBetweenModifierResets);
            }

            if (DateTime.Now < NextStoreRefresh)
            {
                return Task.FromResult(true);
            }

            NextStoreRefresh = DateTime.Now.AddSeconds(SecondsBetweenRefresh);
            if (DateTime.Now >= NextInventoryRefresh)
            {
                NextInventoryRefresh = DateTime.Now.AddSeconds(SecondsBetweenInventoryRefresh);
                //clear items that the store doesnt sell 
            }

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

            foreach (var store in grid.GetFatBlocks().OfType<MyStoreBlock>().Where(x => x.OwnerId == owner))
            {
 
                ClearStoreOfPlayersBuyingOffers(store);
                var items = GetStoreItems(store);
                foreach (var item in items)
                {
                    if (!MyDefinitionId.TryParse(item.Type, item.Subtype, out MyDefinitionId id))
                    {
                        CrunchEconV3.Core.Log.Error($"{item.Type} {item.Subtype} not a valid id");
                        continue;
                    };
                    var inventories = GetInventories(grid);
                    var quantity = CrunchEconV3.Handlers.InventoriesHandler.CountComponents(inventories, id);
                    //var itemsInInventory = InventoriesHandler.CountComponents();
                    //try
                    //{
                    //    DoBuy(item, store, quantity, inventories);
                    //    DoSell(item, store, quantity, inventories);
                    //}
                    //catch (Exception e)
                    //{
                    //    CrunchEconV3.Core.Log.Error(e);
                    //}

                }
            }
            throw new NotImplementedException();
        }

        public List<StoreEntryModel> GetStoreItems(MyStoreBlock store)
        {
            return new List<StoreEntryModel>();
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
