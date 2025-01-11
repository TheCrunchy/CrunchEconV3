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
                    var station = KeenStationPrefabHelper.GetRandomStationPrefabName(MyStationTypeEnum.Outpost);
                    var surfacePosition = PlanetHelper.GetSurfacePositionWithForward(planet);
                    if (surfacePosition == null)
                    {
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

  
    }
}
