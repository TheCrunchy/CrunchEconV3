using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Planet;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Spawn
{
    public class ScriptClass
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static string PlanetName = "Vermilion";
        private static bool SpawnNearFactionMates = true;
        private static float SpawnHeight = 400;
        private static float MinimalAirDensity = .90f;

        private static float CollisionRadius;

        public void BeforeSpawn(ref List<MyObjectBuilder_CubeGrid> Grids, ref List<long> Players, ref Vector3D SpawnPos, ref bool AlignToGravity, string CustomData = null)
        {
            try
            {
                MyPlanet ChosenSpawn = MyPlanets.GetPlanets().Find(x => x.Name.Contains(PlanetName));
                AlignToGravity = true;

                if (ChosenSpawn is null)
                {
                    Log.Error("Invalid Planet!");
                    return;
                }

                CollisionRadius = (float)FindBoundingSphere(Grids).Radius + 10;
                bool flag = false;
                SpawnPos = Vector3D.Zero;

                foreach (var grid in Grids)
                {
                    foreach (var block in grid.CubeBlocks)
                    {
                        block.Owner = Players[0];
                        block.BuiltBy = Players[0];
                    }
                    grid.IsRespawnGrid = true;
                }

                if (SpawnNearFactionMates)
                {
                    List<Vector3D> friendlyPlayerPositions = GetFriendlyPlayerPositions(Players[0]);

                    try
                    {
                        BoundingBoxD worldAABB = ChosenSpawn.PositionComp.WorldAABB;
                        for (int num = friendlyPlayerPositions.Count - 1; num >= 0; num--)
                        {
                            if (worldAABB.Contains(friendlyPlayerPositions[num]) == ContainmentType.Disjoint)
                                friendlyPlayerPositions.RemoveAt(num);
                        }

                        for (int i = 0; i < 30; i += 3)
                        {
                            if (flag)
                                break;

                            foreach (Vector3D item in friendlyPlayerPositions)
                            {
                                Vector3D? vector3D = FindPositionAbovePlanet(item, ChosenSpawn, true, i, i + 3, out Vector3 forward, out Vector3 up);
                                if (vector3D.HasValue)
                                {
                                    SpawnPos = vector3D.Value;
                                    flag = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn(e);
                    }
                }

                if (!flag)
                {
                    Vector3D center = ChosenSpawn.PositionComp.WorldVolume.Center;
                    for (int j = 0; j < 50; j++)
                    {
                        Vector3 value = MyUtils.GetRandomVector3Normalized();
                        if (value.Dot(MySector.DirectionToSunNormalized) < 0f && j < 20)
                            value = -value;

                        SpawnPos = center + value * ChosenSpawn.AverageRadius;
                        Vector3D? vector3D2 = FindPositionAbovePlanet(SpawnPos, ChosenSpawn, j < 20, 0, 30, out Vector3 forward, out Vector3 up);
                        if (vector3D2.HasValue)
                        {
                            SpawnPos = vector3D2.Value;
                            if ((SpawnPos - center).Dot(MySector.DirectionToSunNormalized) > 0.0)
                                break;
                        }
                    }
                }

                // Nearest CrunchEconV3 station logging
                try
                {
                    string folderPath = @"\\GG-BOX1\Shared\CrunchEconV3\Stations";
                    DirectoryInfo dir = new DirectoryInfo(folderPath);
                    FileInfo[] files = dir.GetFiles("*.json");

                    double closestDistance = double.MaxValue;
                    string closestStationName = "";
                    string closestGPS = "";

                    foreach (FileInfo file in files)
                    {
                        string json = File.ReadAllText(file.FullName);
                        if (json.Contains("\"LocationGPS\""))
                        {
                            string gpsLine = json.Split(new string[] { "\"LocationGPS\":" }, StringSplitOptions.None)[1]
                                                 .Split(',')[0]
                                                 .Trim().Trim('"');

                            string[] gpsParts = gpsLine.Split(':');
                            if (gpsParts.Length >= 5 &&
                                double.TryParse(gpsParts[2], out double x) &&
                                double.TryParse(gpsParts[3], out double y) &&
                                double.TryParse(gpsParts[4], out double z))
                            {
                                Vector3D stationPos = new Vector3D(x, y, z);
                                double dist = Vector3D.Distance(SpawnPos, stationPos);

                                if (dist < closestDistance)
                                {
                                    closestDistance = dist;
                                    closestStationName = Path.GetFileNameWithoutExtension(file.Name);
                                    closestGPS = gpsLine;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(closestStationName))
                    {
                        Log.Info($"Closest CrunchEconV3 station to spawn point is '{closestStationName}': {closestGPS}");

                        DoDatapadMagic(Grids, closestGPS);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn($"Error finding closest CrunchEcon station: {ex}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private static void DoDatapadMagic(List<MyObjectBuilder_CubeGrid> Grids, string closestGPS)
        {
            var datapad = BuildDataPad(closestGPS);
            var datapadItem = new MyObjectBuilder_InventoryItem
            {
                Amount = 1,
                PhysicalContent = datapad
            };


            var allCockpits = Grids.SelectMany(g => g.CubeBlocks)
                .Where(b => b is MyObjectBuilder_Cockpit)
                .Cast<MyObjectBuilder_Cockpit>();
            foreach (var cockpit in allCockpits)
            {
                // Ensure ComponentContainer exists
                if (cockpit.ComponentContainer == null)
                {
                    cockpit.ComponentContainer = new MyObjectBuilder_ComponentContainer
                    {
                        Components = new List<MyObjectBuilder_ComponentContainer.ComponentData>()
                    };
                }

                // Try to find existing Inventory component
                var inventoryComponent = cockpit.ComponentContainer.Components
                    .FirstOrDefault(c => c.TypeId == typeof(MyInventoryBase).Name);

                MyObjectBuilder_Inventory inventory;

                if (inventoryComponent == null)
                {
                    // No inventory yet — create new one and add to container
                    inventory = new MyObjectBuilder_Inventory
                    {
                        Items = new List<MyObjectBuilder_InventoryItem>(),
                        CanPutItems = true
                    };

                    cockpit.ComponentContainer.Components.Add(new MyObjectBuilder_ComponentContainer.ComponentData
                    {
                        TypeId = typeof(MyInventoryBase).Name,
                        Component = inventory
                    });
                }
                else
                {
                    // Get existing inventory
                    inventory = inventoryComponent.Component as MyObjectBuilder_Inventory;
                    if (inventory.Items == null)
                        inventory.Items = new List<MyObjectBuilder_InventoryItem>();
                }

                // Add datapad to inventory
                var clonedItem = MyObjectBuilderSerializer.Clone(datapadItem) as MyObjectBuilder_InventoryItem;
                inventory.Items.Add(clonedItem);
            }
        }

        public static MyObjectBuilder_Datapad BuildDataPad(string stationGps)
        {

            var datapadBuilder = new MyObjectBuilder_Datapad() { SubtypeName = "Datapad" };
            datapadBuilder.Data = stationGps;
            return datapadBuilder;

        }
        private static List<Vector3D> GetFriendlyPlayerPositions(long identityId)
        {
            List<Vector3D> RandomFriendlyPositions = new List<Vector3D>();
            MyFaction Faction = MySession.Static.Factions.GetPlayerFaction(identityId);

            if (Faction == null)
                return RandomFriendlyPositions;

            foreach (MyIdentity allIdentity in MySession.Static.Players.GetAllIdentities().Where(x => Faction.Members.ContainsKey(x.IdentityId)))
            {
                MyCharacter character = allIdentity.Character;
                if (character != null && !character.IsDead && !character.MarkedForClose)
                    RandomFriendlyPositions.Add(character.PositionComp.GetPosition());
            }

            return RandomFriendlyPositions;
        }

        private static Vector3D? FindPositionAbovePlanet(Vector3D friendPosition, MyPlanet info, bool testFreeZone, int distanceIteration, int maxDistanceIterations, out Vector3 forward, out Vector3 up)
        {
            Log.Info("Finding position above planet!");
            MyPlanet planet = info;
            Vector3D center = planet.PositionComp.WorldAABB.Center;
            Vector3D axis = Vector3D.Normalize(friendPosition - center);
            float optimalSpawnDistance = MySession.Static.Settings.OptimalSpawnDistance;
            float minimalClearance = (optimalSpawnDistance - optimalSpawnDistance * 0.5f) * 0.9f;

            for (int i = 0; i < 20; i++)
            {
                Vector3D randomPerpendicularVector = MyUtils.GetRandomPerpendicularVector(ref axis);
                float num = optimalSpawnDistance * (MyUtils.GetRandomFloat(0.549999952f, 1.65f) + (float)distanceIteration * 0.05f);
                Vector3D globalPos = friendPosition + randomPerpendicularVector * num;
                globalPos = planet.GetClosestSurfacePointGlobal(ref globalPos);
                if (!TestLanding(info, globalPos, testFreeZone, minimalClearance, ref distanceIteration))
                {
                    if (distanceIteration > maxDistanceIterations)
                        break;
                    continue;
                }

                Vector3D? shipOrientationForPlanetSpawn = GetShipOrientationForPlanetSpawn(ref globalPos, out forward, out up);
                if (shipOrientationForPlanetSpawn.HasValue)
                    return shipOrientationForPlanetSpawn.Value;
            }

            forward = default(Vector3);
            up = default(Vector3);
            return null;
        }

        private static Vector3D? GetShipOrientationForPlanetSpawn(ref Vector3D landingPosition, out Vector3 forward, out Vector3 up)
        {
            Log.Warn("Getting ship orientation for spawn!");

            Vector3 vector = MyGravityProviderSystem.CalculateNaturalGravityInPoint(landingPosition);
            if (Vector3.IsZero(vector))
                vector = Vector3.Up;

            Vector3D value = Vector3D.Normalize(vector);
            Vector3D value2 = -value;

            Vector3D? result = landingPosition + value2 * SpawnHeight;
            forward = Vector3.CalculatePerpendicularVector(-value);
            up = -value;
            return result;
        }

        private static bool CheckTerrain(MyPlanet Planet, Vector3D landingPosition, Vector3D DeviationNormal, Vector3D GravityVector)
        {
            Vector3 vector = (Vector3)DeviationNormal * CollisionRadius;
            Vector3 value = Vector3.Cross(vector, GravityVector);
            MyOrientedBoundingBoxD box = new MyOrientedBoundingBoxD(landingPosition, new Vector3D(CollisionRadius * 2f, Math.Min(10f, CollisionRadius * 0.5f), CollisionRadius * 2f), Quaternion.CreateFromForwardUp(DeviationNormal, GravityVector));
            int num = -1;
            for (int i = 0; i < 4; i++)
            {
                num = -num;
                int num2 = (i <= 1) ? 1 : (-1);
                Vector3D point = Planet.GetClosestSurfacePointGlobal(landingPosition + vector * num + value * num2);
                if (!box.Contains(ref point))
                    return false;
            }
            return true;
        }

        private static bool TestLanding(MyPlanet Planet, Vector3D landingPosition, bool testFreeZone, float minimalClearance, ref int distanceIteration)
        {
            if (testFreeZone && MinimalAirDensity > 0f && Planet.GetAirDensity(landingPosition) < MinimalAirDensity)
                return false;

            Vector3D center = Planet.PositionComp.WorldAABB.Center;
            Vector3D GravityVector = Vector3D.Normalize(landingPosition - center);
            Vector3D DeviationNormal = MyUtils.GetRandomPerpendicularVector(ref GravityVector);

            if (!CheckTerrain(Planet, landingPosition, DeviationNormal, GravityVector))
                return false;

            if (testFreeZone && !IsZoneFree(new BoundingSphereD(landingPosition, minimalClearance)))
            {
                distanceIteration++;
                return false;
            }

            return true;
        }

        private static bool IsZoneFree(BoundingSphereD safeZone)
        {
            ClearToken<MyEntity> clearToken = ListExtensions.GetClearToken(MyEntities.GetTopMostEntitiesInSphere(ref safeZone));
            try
            {
                foreach (MyEntity item in clearToken.List)
                {
                    if (item is MyCubeGrid)
                        return false;
                }
            }
            finally
            {
                ((IDisposable)clearToken).Dispose();
            }
            return true;
        }

        private BoundingSphereD FindBoundingSphere(List<MyObjectBuilder_CubeGrid> grids)
        {
            BoundingSphere result = new BoundingSphere(Vector3.Zero, float.MinValue);
            foreach (MyObjectBuilder_CubeGrid grid in grids)
            {
                BoundingSphere boundingSphere = MyCubeGridExtensions.CalculateBoundingSphere(grid);
                MatrixD m = grid.PositionAndOrientation.HasValue ? grid.PositionAndOrientation.Value.GetMatrix() : MatrixD.Identity;
                result.Include(boundingSphere.Transform(m));
            }
            return result;
        }
    }
}