using System;
using System.Collections.Generic;
using System.Reflection;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Sync;
using IMyBeacon = Sandbox.ModAPI.Ingame.IMyBeacon;

namespace CrunchEconContractModels.Ship_Class_Stuff
{
    [PatchShim]
    public static class BeaconStuff
    {
        private static List<MyBeacon> Beacons = new List<MyBeacon>();
        private static List<MyGyro> Gyros = new List<MyGyro>();
        private static int PatchCount = 0;
        public static void Patch(PatchContext ctx)
        {
            PatchCount++;
            if (PatchCount != 1)
            {
                return;
            }
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;

            // Iterate through all existing grids when the mod initializes
            var grids = new List<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity is IMyCubeGrid grid)
                {
                    OnEntityAdd(grid);
                }
                return false;
            });

            Core.UpdateCycle += UpdateExample;
        }

        private static int ticks;

        public static void UpdateExample()
        {
            if (PatchCount != 1)
            {
                return;
            }
            ticks++;
            if (ticks % 60 == 0)
            {

                //foreach (var beacon in Beacons)
                //{
                //    try
                //    {

                //        FieldInfo fieldInfo = beacon.GetType()
                //            .GetField("m_radius", BindingFlags.NonPublic | BindingFlags.Instance);

                //        if (fieldInfo != null)
                //        {
                //            // Access the value of the field
                //            VRage.Sync.Sync<float, SyncDirection.BothWays> fieldValue =
                //                (Sync<float, SyncDirection.BothWays>)fieldInfo.GetValue(beacon);
                //            fieldValue.ValidateAndSet(1);
                //            Core.Log.Info("attempting change on beacon");
                //        }
                //        else
                //        {
                //            Core.Log.Info("it was null");
                //        }
                //    }
                //    catch (Exception e)
                //    {

                //    }
                //}

                foreach (var gyro in Gyros)
                {
                    try
                    {

                        FieldInfo fieldInfo = gyro.GetType()
                        .GetField("m_gyroOverride", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fieldInfo != null)
                    {
                        // Access the value of the field
                        VRage.Sync.Sync<float, SyncDirection.BothWays> fieldValue =
                            (Sync<float, SyncDirection.BothWays>)fieldInfo.GetValue(gyro);
                        fieldValue.ValidateAndSet(1);
                        Core.Log.Info("attempting change on gyro");
                    }
                    else
                    {
                        Core.Log.Info("it was null");
                    }
                    }
                catch (Exception e)
                {

                }
            }
            }
        }
        private static void OnEntityAdd(IMyEntity entity)
        {
            if (entity is IMyCubeGrid grid)
            {
                grid.OnBlockAdded += OnBlockAdded;
                var asMyCube = grid as MyCubeGrid;
                foreach (var block in asMyCube.GetBlocks())
                {
                    OnBlockAdded(block);
                }
            }
        }

        private static void OnBlockAdded(IMySlimBlock block)
        {
            if (block.FatBlock is IMyBeacon beacon)
            {
                Core.Log.Info("Beacon");
                var asBeacon = beacon as MyBeacon;
                Beacons.Add(asBeacon);
            }
            if (block.FatBlock is IMyGyro gyro)
            {
                Core.Log.Info("gyro");
                var asBeacon = gyro as MyGyro;
                Gyros.Add(asBeacon);
            }
        }
    }
}

