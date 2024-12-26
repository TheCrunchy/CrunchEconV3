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

                foreach (var beacon in Beacons)
                {
                    FieldInfo fieldInfo = beacon.GetType()
                        .GetField("m_radius", BindingFlags.NonPublic | BindingFlags.Instance);

                    if (fieldInfo != null)
                    {
                        // Access the value of the field
                        VRage.Sync.Sync<float, SyncDirection.BothWays> fieldValue =
                            (Sync<float, SyncDirection.BothWays>)fieldInfo.GetValue(beacon);
                        fieldValue.ValidateAndSet(1);
                        Core.Log.Info("attempting change");
                    }
                    else
                    {
                        Core.Log.Info("it was null");
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
        }
    }
}

