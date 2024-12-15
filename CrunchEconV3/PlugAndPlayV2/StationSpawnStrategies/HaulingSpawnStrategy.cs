using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlayV2.Abstracts;
using CrunchEconV3.PlugAndPlayV2.Helpers;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.GameSystems;
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
    public class MiningSpawnStrategy : StationSpawnStrategyAbstract
    {
        public override List<StationConfig> SpawnStations(List<MyFaction> availableFactions, string templateName, int maximumToSpawn, List<MyPlanet> planets = null)
        {
            if (!DoesTemplateExist(templateName))
            {
                Core.Log.Error("Template does not exist");
                return new List<StationConfig>();
            }
            if (planets == null)
            {
                planets = MyPlanets.GetPlanets();
            }
            var endStations = new List<StationConfig>();
            foreach (var planet in planets)
            {
                for (int i = 0; i < maximumToSpawn; i++)
                {
                    var faction = availableFactions.GetRandomItemFromList();
                    var station = KeenStationPrefabHelper.GetRandomStationPrefabName(MyStationTypeEnum.MiningStation);
                    var distanceFromPlanet = 250000;
                    var minDistanceFromPlanet = distanceFromPlanet;

                    if (minDistanceFromPlanet < planet.AtmosphereRadius)
                    {
                        minDistanceFromPlanet = (int)(planet.AtmosphereRadius + distanceFromPlanet);
                    }
                    Vector3D randomDirection = MyUtils.GetRandomVector3Normalized();

                    // Generate a random distance within the specified range
                    double randomDistance = MyUtils.GetRandomDouble(minDistanceFromPlanet, minDistanceFromPlanet + distanceFromPlanet);

                    // Calculate the new position by adding the random direction multiplied by the random distance
                    Vector3D newPosition = planet.PositionComp.GetPosition() + randomDirection * randomDistance;
                    var inGrav = MyGravityProviderSystem.IsPositionInNaturalGravity(newPosition);
                    var attempts = 0;
                    while (inGrav)
                    {
                        if (attempts >= 10)
                        {
                            inGrav = false;
                            continue;
                        }
                        attempts++;
                        minDistanceFromPlanet += 15000;
                        randomDistance = MyUtils.GetRandomDouble(minDistanceFromPlanet, minDistanceFromPlanet + distanceFromPlanet);
                        newPosition = planet.PositionComp.GetPosition() + randomDirection * randomDistance;
                        inGrav = MyGravityProviderSystem.IsPositionInNaturalGravity(newPosition);
                    }
                    MyPrefabManager.Static.SpawnPrefab(station, newPosition,Vector3.Forward,Vector3.Up, ownerId: faction.FounderId, 
                        spawningOptions: SpawningOptions.SetAuthorship | SpawningOptions.UseOnlyWorldMatrix | SpawningOptions.SpawnRandomCargo | SpawningOptions.ReplaceColor);
                    MyObjectBuilder_SafeZone objectBuilderSafeZone = new MyObjectBuilder_SafeZone();
                    objectBuilderSafeZone.PositionAndOrientation = new MyPositionAndOrientation?(new MyPositionAndOrientation(newPosition, Vector3.Forward, Vector3.Up));
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
                    });
                    var generated = GenerateAtLocation(newPosition, faction.Tag, templateName);
                    endStations.Add(generated);
                    Core.StationStorage.Save(generated);

                }
            }

            return endStations;
        }
    }
}
