﻿using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Contracts;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Commands;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;
using VRage.ObjectBuilders.Private;
using VRageMath;

namespace CrunchEconV3.Utils
{

    //Class from LordTylus ALE Core
    //https://github.com/LordTylus/SE-Torch-ALE-Core/blob/master/GridManager.cs
    public static class GridManager
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static string newGridName;

        static GridManager()
        {
        }

        public static bool SaveGridNoDelete(string path, string filename, bool keepOriginalOwner, bool keepProjection, List<MyCubeGrid> grids)
        {

            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

            foreach (MyCubeGrid grid in grids)
            {
                grid.DestructibleBlocks = true;
                grid.Editable = true;
                /* What else should it be? LOL? */
                if (!(grid.GetObjectBuilder(true) is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }

            return SaveGridNoDelete(path, filename, keepOriginalOwner, keepProjection, objectBuilders);
        }

        public static bool SaveGridNoDelete(string path, string filename, bool keepOriginalOwner, bool keepProjection, List<MyObjectBuilder_CubeGrid> objectBuilders)
        {

            MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializerKeen.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), filename);
            definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();


            /* Reset ownership as it will be different on the new server anyway */
            foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids)
            {
                foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks)
                {

                    if (!keepOriginalOwner)
                    {
                        cubeBlock.Owner = 0L;
                        cubeBlock.BuiltBy = 0L;
                    }

                    /* Remove Pilot and Components (like Characters) from cockpits */
                    if (cubeBlock is MyObjectBuilder_Cockpit cockpit)
                    {

                        cockpit.Pilot = null;

                        if (cockpit.ComponentContainer != null)
                        {

                            var components = cockpit.ComponentContainer.Components;

                            if (components != null)
                            {

                                for (int i = components.Count - 1; i >= 0; i--)
                                {

                                    var component = components[i];

                                    if (component.TypeId == "MyHierarchyComponentBase")
                                    {
                                        components.RemoveAt(i);
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializerKeen.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };

            return MyObjectBuilderSerializerKeen.SerializeXML(path, false, builderDefinition);
        }
        public static MyObjectBuilder_ShipBlueprintDefinition[] getBluePrint(string name, long newOwner, bool keepProjection, List<MyCubeGrid> grids)
        {
            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

            foreach (MyCubeGrid grid in grids)
            {

                /* What else should it be? LOL? */
                if (!(grid.GetObjectBuilder(true) is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }

            MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializerKeen.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), name);
            definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();

            /* Reset ownership as it will be different on the new server anyway */
            foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids)
            {
                cubeGrid.DisplayName = newOwner.ToString();

                foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks)
                {
                    long ownerID = IdentityHelper.GetIdentityByNameOrId(newOwner.ToString()).IdentityId;
                    cubeBlock.Owner = ownerID;
                    cubeBlock.BuiltBy = ownerID;


                    /* Remove Projections if not needed */
                    if (!keepProjection)
                        if (cubeBlock is MyObjectBuilder_ProjectorBase projector)
                        {
                            projector.ProjectedGrid = null;
                            projector.ProjectedGrids = null;
                        }



                    /* Remove Pilot and Components (like Characters) from cockpits */
                    if (cubeBlock is MyObjectBuilder_Cockpit cockpit)
                    {

                        cockpit.Pilot = null;

                        if (cockpit.ComponentContainer != null)
                        {

                            var components = cockpit.ComponentContainer.Components;

                            if (components != null)
                            {

                                for (int i = components.Count - 1; i >= 0; i--)
                                {

                                    var component = components[i];

                                    if (component.TypeId == "MyHierarchyComponentBase")
                                    {
                                        components.RemoveAt(i);
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializerKeen.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };

            return builderDefinition.ShipBlueprints;
        }


        public static List<MyObjectBuilder_CubeGrid> GetObjectBuilders(string path)
        {
            if (MyObjectBuilderSerializerKeen.DeserializeXML(path, out MyObjectBuilder_Definitions Definition))
            {
                List<MyObjectBuilder_CubeGrid> gridsToReturn = new List<MyObjectBuilder_CubeGrid>();
                if (Definition.Prefabs != null && Definition.Prefabs.Count() != 0)
                {
                    foreach (var prefab in Definition.Prefabs)
                    {
                        foreach (var grid in prefab.CubeGrids)
                        {
                            gridsToReturn.Add(grid);
                        }
                    }
                }
                else if (Definition.ShipBlueprints != null && Definition.ShipBlueprints.Count() != 0)
                {
                    foreach (var bp in Definition.ShipBlueprints)
                    {
                        foreach (var grid in bp.CubeGrids)
                        {
                            gridsToReturn.Add(grid);
                        }
                    }
                }
            }

            return null;
        }
        public static bool LoadGrid(string path, Vector3D playerPosition, bool keepOriginalLocation, ulong steamID, String name, bool force = false, CommandContext context = null)
        {
            if (MyObjectBuilderSerializerKeen.DeserializeXML(path, out MyObjectBuilder_Definitions myObjectBuilder_Definitions))
            {

                var shipBlueprints = myObjectBuilder_Definitions.ShipBlueprints;

                if (shipBlueprints == null)
                {

                    Log.Warn("No ShipBlueprints in File '" + path + "'");

                    if (context != null)
                        context.Respond("There arent any Grids in your file to import!");

                    return false;
                }

                foreach (var shipBlueprint in shipBlueprints)
                {

                    if (!LoadShipBlueprint(shipBlueprint, playerPosition, keepOriginalLocation, (long)steamID, name, context, force))
                    {

                        Log.Warn("Error Loading ShipBlueprints from File '" + path + "'");
                        return false;
                    }
                }

                return true;
            }

            Log.Warn("Error Loading File '" + path + "' check Keen Logs.");

            return false;
        }



        public static bool LoadShipBlueprint(MyObjectBuilder_ShipBlueprintDefinition shipBlueprint,
            Vector3D playerPosition, bool keepOriginalLocation, long steamID, string Name, CommandContext context = null, bool force = false)
        {
            var grids = shipBlueprint.CubeGrids;

            if (grids == null || grids.Length == 0)
            {

                Log.Warn("No grids in blueprint!");

                if (context != null)
                    context.Respond("No grids in blueprint!");

                return false;
            }

            foreach (var grid in grids)
            {
                foreach (MyObjectBuilder_CubeBlock block in grid.CubeBlocks)
                {
                    try
                    {
                        long ownerID = IdentityHelper.GetIdentityByNameOrId(steamID.ToString()).IdentityId;
                        block.Owner = ownerID;
                        block.BuiltBy = ownerID;
                        grid.DisplayName = Name;
                    }
                    catch (Exception)
                    {
                        block.Owner = steamID;
                        block.BuiltBy = steamID;
                        grid.DisplayName = Name;
                    }
                }
            }

            List<MyObjectBuilder_EntityBase> objectBuilderList = new List<MyObjectBuilder_EntityBase>(grids.ToList());

            if (!keepOriginalLocation)
            {

                /* Where do we want to paste the grids? Lets find out. */
                var pos = FindPastePosition(grids, playerPosition);
                if (pos == null)
                {

                    Log.Warn("No free Space found!");

                    if (context != null)
                        context.Respond("No free space available!");

                    return false;
                }

                var newPosition = pos.Value;

                /* Update GridsPosition if that doesnt work get out of here. */
                if (!UpdateGridsPosition(grids, newPosition))
                {

                    if (context != null)
                        context.Respond("The File to be imported does not seem to be compatible with the server!");

                    return false;
                }
                var player = MySession.Static.Players.GetPlayerByName(IdentityHelper.GetIdentityByNameOrId(steamID.ToString()).DisplayName);
                if (player != null)
                {
                    MyGps gps = CreateGps(pos.Value, Color.LightGreen, 60, Name);
                    MyGpsCollection gpsCollection = (MyGpsCollection)MyAPIGateway.Session?.GPS;
                    MyGps gpsRef = gps;
                    long entityId = 0L;
                    entityId = gps.EntityId;
                    gpsCollection.SendAddGpsRequest(player.Identity.IdentityId, ref gpsRef, entityId, true);
                }
            }
            else if (!force)
            {

                var sphere = FindBoundingSphere(grids);

                var position = grids[0].PositionAndOrientation.Value;

                sphere.Center = position.Position;

                List<MyEntity> entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entities);

                foreach (var entity in entities)
                {

                    if (entity is MyCubeGrid)
                    {

                        if (context != null)
                            context.Respond("There are potentially other grids in the way. If you are certain is free you can set 'force' to true!");

                        return false;
                    }
                }
            }
            /* Stop grids */
            foreach (var grid in grids)
            {

                grid.AngularVelocity = new SerializableVector3();
                grid.LinearVelocity = new SerializableVector3();

                Random random = new Random();

            }
            /* Remapping to prevent any key problems upon paste. */
            MyEntities.RemapObjectBuilderCollection(objectBuilderList);

            bool hasMultipleGrids = objectBuilderList.Count > 1;

            //if (!hasMultipleGrids)
            //{

            foreach (var ob in objectBuilderList)
            {
               var ent = MyEntities.CreateFromObjectBuilderParallel(ob, true);
               //if (ent is MyCubeGrid grid)
               //{
               //    grid.GridSystems.AiBlockSystem.SetMovementFlightMode(FlightMode.Circle);
               //    var wayPoint = new MyAutopilotWaypoint(playerPosition, "Home");

               //    grid.GridSystems.AiBlockSystem.SetMovementWaypoint(wayPoint);
               //    Core.Log.Info("ai");
               // }
            }
            
            //}
            //else
            //{
            //    MyEntities.Load(objectBuilderList, out _);
            //}
            return true;
        }

        private static MyGps CreateGps(Vector3D Position, Color gpsColor, int seconds, string Name)
        {

            MyGps gps = new MyGps
            {
                Coords = Position,
                Name = Name.Split('_')[0],
                DisplayName = Name.Split('_')[0] + " Paste Position",
                GPSColor = gpsColor,
                IsContainerGPS = true,
                ShowOnHud = true,
                DiscardAt = new TimeSpan(0, 0, seconds, 0),
                Description = "Paste Position",
            };
            gps.UpdateHash();


            return gps;
        }

        private static bool UpdateGridsPosition(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition)
        {

            bool firstGrid = true;
            double deltaX = 0;
            double deltaY = 0;
            double deltaZ = 0;

            foreach (var grid in grids)
            {

                var position = grid.PositionAndOrientation;

                if (position == null)
                {

                    Log.Warn("Position and Orientation Information missing from Grid in file.");

                    return false;
                }

                var realPosition = position.Value;

                var currentPosition = realPosition.Position;

                if (firstGrid)
                {
                    deltaX = newPosition.X - currentPosition.X;
                    deltaY = newPosition.Y - currentPosition.Y;
                    deltaZ = newPosition.Z - currentPosition.Z;

                    currentPosition.X = newPosition.X;
                    currentPosition.Y = newPosition.Y;
                    currentPosition.Z = newPosition.Z;

                    firstGrid = false;

                }
                else
                {

                    currentPosition.X += deltaX;
                    currentPosition.Y += deltaY;
                    currentPosition.Z += deltaZ;
                }

                realPosition.Position = currentPosition;
                grid.PositionAndOrientation = realPosition;


            }

            return true;
        }

        private static Vector3D? FindPastePosition(MyObjectBuilder_CubeGrid[] grids, Vector3D playerPosition)
        {

            BoundingSphere sphere = FindBoundingSphere(grids);

            /* 
             * Now we know the radius that can house all grids which will now be 
             * used to determine the perfect place to paste the grids to. 
             */
            return MyEntities.FindFreePlace(playerPosition, sphere.Radius, allowSafezones:true);
        }

        private static BoundingSphereD FindBoundingSphere(MyObjectBuilder_CubeGrid[] grids)
        {

            Vector3? vector = null;
            float radius = 0F;

            foreach (var grid in grids)
            {

                var gridSphere = grid.CalculateBoundingSphere();

                /* If this is the first run, we use the center of that grid, and its radius as it is */
                if (vector == null)
                {

                    vector = gridSphere.Center;
                    radius = gridSphere.Radius;
                    continue;
                }

                /* 
                 * If its not the first run, we use the vector we already have and 
                 * figure out how far it is away from the center of the subgrids sphere. 
                 */
                float distance = Vector3.Distance(vector.Value, gridSphere.Center);

                /* 
                 * Now we figure out how big our new radius must be to house both grids
                 * so the distance between the center points + the radius of our subgrid.
                 */
                float newRadius = distance + gridSphere.Radius;

                /*
                 * If the new radius is bigger than our old one we use that, otherwise the subgrid 
                 * is contained in the other grid and therefore no need to make it bigger. 
                 */
                if (newRadius > radius)
                    radius = newRadius;


            }

            return new BoundingSphereD(vector.Value, radius);
        }
    }
}