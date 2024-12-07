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
  
            Context.Respond("Prefab Spawned?");

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

            var surfacePosition = GetSurfacePositionWithForward(lowestDistancePlanet);
            MyPrefabManager.Static.SpawnPrefab(stationName, surfacePosition.Item1, surfacePosition.Item3, surfacePosition.Item2, 
                ownerId: Context.Player.IdentityId, spawningOptions: SpawningOptions.SetAuthorship | SpawningOptions.UseOnlyWorldMatrix | SpawningOptions.SpawnRandomCargo);
            var gps = GPSHelper.CreateGps(surfacePosition.Item1, Color.MediumAquamarine, "SPAWNED POSITION", "Reason");

            MyObjectBuilder_SafeZone objectBuilderSafeZone = new MyObjectBuilder_SafeZone();
            objectBuilderSafeZone.PositionAndOrientation = new MyPositionAndOrientation?(new MyPositionAndOrientation(surfacePosition.Item1, Vector3.Forward, Vector3.Up));
            objectBuilderSafeZone.PersistentFlags = MyPersistentEntityFlags2.InScene;
            objectBuilderSafeZone.Shape = MySafeZoneShape.Sphere;
            objectBuilderSafeZone.Radius = (float)500;
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

                MyPlanetEnvironmentSessionComponent component = MySession.Static.GetComponent<MyPlanetEnvironmentSessionComponent>();
                BoundingBoxD worldBBox = new BoundingBoxD(surfacePosition.Item1 - (double)500, surfacePosition.Item1 + 500);
                component.ClearEnvironmentItems((MyEntity)ent, worldBBox);
            });

            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            gpscol.SendAddGpsRequest(Context.Player.IdentityId, ref gps);
        }

        //this doesnt work at all 
        public Tuple<Vector3D, Vector3D, Vector3D> GetSurfacePositionWithForward(MyPlanet planet)
        {
            // Generate a random direction
            Vector3D randomDirection = Vector3D.Normalize(new Vector3D(
                MyUtils.GetRandomDouble(-1, 1),
                MyUtils.GetRandomDouble(-1, 1),
                MyUtils.GetRandomDouble(-1, 1)));

            var directionFromCenter = Vector3D.Normalize(randomDirection);

            // Get the planet's center
            Vector3D planetCenter = planet.PositionComp.WorldAABB.Center;

            // Find a point on the planet's surface
            var surfacePoint = planet.GetClosestSurfacePointGlobal(planetCenter + (directionFromCenter * 100000));

            // Calculate the "up" vector (normal to the surface)
            Vector3D upVector = Vector3D.Normalize(surfacePoint - planetCenter);

            // Define a stable forward vector orthogonal to the up vector
            Vector3D arbitraryVector = Vector3D.Forward; // Default fallback
            if (Vector3D.IsZero(Vector3D.Cross(upVector, arbitraryVector)))
            {
                arbitraryVector = Vector3D.Right; // Use a different vector if upVector is aligned with Forward
            }

            // Compute the forward vector
            Vector3D forwardVector = Vector3D.Normalize(Vector3D.Cross(arbitraryVector, upVector));

            // Ensure forward and up are orthogonal
            Vector3D rightVector = Vector3D.Cross(forwardVector, upVector);
            forwardVector = Vector3D.Cross(upVector, rightVector);

            return Tuple.Create(surfacePoint, upVector, forwardVector);
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
