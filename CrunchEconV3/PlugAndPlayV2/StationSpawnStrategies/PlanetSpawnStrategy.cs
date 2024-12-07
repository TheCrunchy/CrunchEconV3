using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlayV2.Abstracts;
using CrunchEconV3.PlugAndPlayV2.Helpers;
using CrunchEconV3.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.PlugAndPlayV2.StationSpawnStrategies
{
    public class PlanetSpawnStrategy : StationSpawnStrategyAbstract
    {
        public override List<StationConfig> SpawnStations(List<MyFaction> availableFactions, string templateName, int maximumToSpawn)
        {
            var planets = MyPlanets.GetPlanets();
            var endStations = new List<StationConfig>();
            foreach (var planet in planets)
            {
                for (int i = 0; i >= maximumToSpawn; i++)
                {
                    var faction = availableFactions.GetRandomItemFromList();
                    var station = KeenStationPrefabHelper.GetRandomStationPrefabName(MyStationTypeEnum.Outpost);
                    var surfacePosition = GetSurfacePositionWithForward(planet);
                    MyPrefabManager.Static.SpawnPrefab(station, surfacePosition.Item1, surfacePosition.Item3, surfacePosition.Item2,
                        ownerId: faction.FounderId, spawningOptions: SpawningOptions.SetAuthorship | SpawningOptions.UseOnlyWorldMatrix | SpawningOptions.SpawnRandomCargo);
                    MyObjectBuilder_SafeZone objectBuilderSafeZone = new MyObjectBuilder_SafeZone();
                    objectBuilderSafeZone.PositionAndOrientation = new MyPositionAndOrientation?(new MyPositionAndOrientation(surfacePosition.Item1, Vector3.Forward, Vector3.Up));
                    objectBuilderSafeZone.PersistentFlags = MyPersistentEntityFlags2.InScene;
                    objectBuilderSafeZone.Shape = MySafeZoneShape.Sphere;
                    objectBuilderSafeZone.Radius = (float)250;
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
                        BoundingBoxD worldBBox = new BoundingBoxD(surfacePosition.Item1 - (double)250, surfacePosition.Item1 + 250);
                        component.ClearEnvironmentItems((MyEntity)ent, worldBBox);
                    });
                    var generated = GenerateAtLocation(surfacePosition.Item1, faction.Tag, templateName);
                    endStations.Add(generated);
                    Core.StationStorage.Save(generated);

                }
            }

            return endStations;
        }

        public Tuple<Vector3D, Vector3D, Vector3D> GetSurfacePositionWithForward(MyPlanet planet)
        {
            //100% chat gpt 
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
}
