using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using Torch.Managers.PatchManager;
using VRage.Game.Components;

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

        public static void InitAllGrids()
        {
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity as VRage.Game.ModAPI.IMyCubeGrid != null)
                {
                    StoreGridId(entity);
                }
                return false;
            });
        }

        public static void StoreGridId(VRage.ModAPI.IMyEntity entity)
        {
            if (entity is MyCubeGrid grid)
            {
                GetAndStoreShipsKnownId(grid);
            }
        }

        public static void RemoveGridId(VRage.ModAPI.IMyEntity entity)
        {
            if (entity is MyCubeGrid grid)
            {
                MyModStorageComponentBase storage = grid.Storage;
                if (storage == null)
                {
                    return;
                }

                if (storage.TryGetValue(StorageGuid, out string id))
                {
                    MarkedForSalvage.Remove(entity.EntityId);
                }
            }
        }


        public static void GetAndStoreShipsKnownId(MyCubeGrid grid, bool createEntryInStorage = false)
        {
            bool isMarkedForSalvage;
            MyModStorageComponentBase storage = grid.Storage;
            if (storage == null)
            {
                grid.Storage = new MyModStorageComponent();
                storage = grid.Storage;
            }

            if (storage.TryGetValue(StorageGuid, out string id))
            {
                if (bool.TryParse(id, out isMarkedForSalvage))
                {
                   MarkedForSalvage.Add(grid.EntityId);
                   return;
                }
            }

            if (createEntryInStorage)
            {
                isMarkedForSalvage = true;
                grid.Storage.SetValue(StorageGuid, isMarkedForSalvage.ToString());
            }

            return;
        }

    }
}
