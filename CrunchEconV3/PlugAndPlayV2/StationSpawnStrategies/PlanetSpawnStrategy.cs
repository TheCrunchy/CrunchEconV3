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
        public override List<StationConfig> SpawnStations(List<MyFaction> availableFactions, string templateName, int maximumToSpawn, List<MyPlanet> planets = null)
        {
            if (planets == null)
            {
                planets = MyPlanets.GetPlanets();
            }
       
            var endStations = new List<StationConfig>();
            foreach (var planet in planets)
            {
                Core.Log.Info("Checking planet");
                for (int i = 0; i < maximumToSpawn; i++)
                {
                    Core.Log.Info($"{i}");
                    var faction = availableFactions.GetRandomItemFromList();
                    var station = KeenStationPrefabHelper.GetRandomStationPrefabName(MyStationTypeEnum.Outpost);
                    var surfacePosition = GetSurfacePositionWithForward(planet);
                    if (surfacePosition == null)
                    {
                        Core.Log.Info("Wasnt flat");
                        i--;
                        continue;
                    }
                    MyPrefabManager.Static.SpawnPrefab(station, surfacePosition.Item1, surfacePosition.Item3, surfacePosition.Item2,
                        ownerId: faction.FounderId, spawningOptions: SpawningOptions.SetAuthorship | SpawningOptions.UseOnlyWorldMatrix | SpawningOptions.SpawnRandomCargo | SpawningOptions.ReplaceColor);

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

        private bool IsSurfaceFlat(MyPlanet planet, Vector3D surfacePoint, double radius, double flatnessThreshold, double heightThreshold)
        {
            Vector3D planetCenter = planet.PositionComp.WorldAABB.Center;

            // Directions to sample around the center point
            Vector3D[] sampleOffsets = new Vector3D[]
            {
                new Vector3D(1, 0, 0),  // Forward
                new Vector3D(-1, 0, 0), // Backward
                new Vector3D(0, 1, 0),  // Right
                new Vector3D(0, -1, 0), // Left
            };

            // Reference height and up vector
            double referenceHeight = Vector3D.Distance(surfacePoint, planetCenter);
            Vector3D referenceUp = Vector3D.Normalize(surfacePoint - planetCenter);

            foreach (var offset in sampleOffsets)
            {
                // Get the neighboring point
                Vector3D neighborPoint = surfacePoint + offset * radius;

                // Find the actual surface point for the neighbor
                Vector3D sampledPoint = planet.GetClosestSurfacePointGlobal(neighborPoint);

                // Calculate height difference
                double sampledHeight = Vector3D.Distance(sampledPoint, planetCenter);
                double heightDifference = Math.Abs(sampledHeight - referenceHeight);
                if (heightDifference > heightThreshold)
                    return false;

                // Calculate normal vector at the neighbor point
                Vector3D sampledUp = Vector3D.Normalize(sampledPoint - planetCenter);

                // Compare the angle between normals
                double angleBetween = Vector3D.Dot(referenceUp, sampledUp);
                if (Math.Acos(angleBetween) > flatnessThreshold * (Math.PI / 180))
                    return false;
            }

            return true; // The area is flat enough
        }

        private Tuple<Vector3D, Vector3D, Vector3D> GetSurfacePositionWithForward(MyPlanet planet, double radius = 10.0, double flatnessThreshold = 5.0, double heightThreshold = 2.0, int maxRetries = 10)
        {
            Vector3D planetCenter = planet.PositionComp.WorldAABB.Center;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                // Generate a random direction
                Vector3D randomDirection = Vector3D.Normalize(new Vector3D(
                    MyUtils.GetRandomDouble(-1, 1),
                    MyUtils.GetRandomDouble(-1, 1),
                    MyUtils.GetRandomDouble(-1, 1)));

                Vector3D directionFromCenter = Vector3D.Normalize(randomDirection);

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

                // Check if the area is flat
                if (IsSurfaceFlat(planet, surfacePoint, radius, flatnessThreshold, heightThreshold))
                {
                    return Tuple.Create(surfacePoint, upVector, forwardVector);
                }
            }

            // Return null if no flat area is found after maxRetries
            return null;
        }
    }
}
