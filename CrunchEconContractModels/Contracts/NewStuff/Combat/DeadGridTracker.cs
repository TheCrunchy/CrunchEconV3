using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using Torch.Managers.PatchManager;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;

namespace CrunchEconContractModels.Contracts.NewStuff.Combat
{
    public static class DeadGridTracker
    {
        private static readonly Guid StorageGuid = new Guid("f382f0cf-fca2-42c6-ad41-1b3ac9313e89");

        private static List<long> MarkedForSalvage = new List<long>();

        public static void Patch(PatchContext ctx)
        {
            MyAPIGateway.Entities.OnEntityAdd += StoreGridId;
            MyAPIGateway.Entities.OnEntityRemove += RemoveGridId;
            InitAllGrids();
        }

        public static MyCubeGrid GetRandomSalvageGrid()
        {
            if (!MarkedForSalvage.Any())
                return null;

            const int maxAttempts = 2;
            MyCubeGrid asGrid = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var random = MarkedForSalvage.GetRandomItemFromList();
                MarkedForSalvage.Remove(random);

                var asEntity = MyAPIGateway.Entities.GetEntityById(random);
                var grid = asEntity as MyCubeGrid;
                if (grid != null)
                {
                    asGrid = grid;
                    break;
                }
            }

            return asGrid;
        }

        public static void InitAllGrids()
        {
            MyAPIGateway.Entities.GetEntities(null, entity =>
            {
                if (entity is VRage.Game.ModAPI.IMyCubeGrid)
                {
                    StoreGridId(entity);
                }
                return false;
            });
        }

        public static void StoreGridId(VRage.ModAPI.IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
            {
                GetAndStoreShipsKnownId(grid);
            }
        }

        public static void RemoveGridId(VRage.ModAPI.IMyEntity entity)
        {
            var grid = entity as MyCubeGrid;
            if (grid != null)
            {
                var storage = grid.Storage;
                if (storage != null && storage.ContainsKey(StorageGuid))
                {
                    MarkedForSalvage.Remove(entity.EntityId);
                }
            }
        }

        public static void GetAndStoreShipsKnownId(MyCubeGrid grid, bool createEntryInStorage = false)
        {
            var storage = grid.Storage;
            if (storage == null)
            {
                grid.Storage = new MyModStorageComponent();
                storage = grid.Storage;
            }

            string id;
            if (storage.TryGetValue(StorageGuid, out id))
            {
                bool isMarkedForSalvage;
                if (bool.TryParse(id, out isMarkedForSalvage) && isMarkedForSalvage)
                {
                    MarkedForSalvage.Add(grid.EntityId);
                    return;
                }
            }

            if (createEntryInStorage)
            {
                storage.SetValue(StorageGuid, true.ToString());
                MarkedForSalvage.Add(grid.EntityId);
            }
        }
    }
}