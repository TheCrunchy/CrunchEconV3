using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlay;
using CrunchEconV3.PlugAndPlayV2.Helpers;
using CrunchEconV3.PlugAndPlayV2.Interfaces;
using CrunchEconV3.PlugAndPlayV2.StationSpawnStrategies;
using CrunchEconV3.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
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
        public void Setup()
        {
            throw new NotImplementedException();
        }

        public Task<bool> DoLogic(MyCubeGrid grid)
        {
            throw new NotImplementedException();
        }

        public int Priority { get; set; }
    }
}
