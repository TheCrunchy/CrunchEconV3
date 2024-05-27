using System;
using System.Collections.Generic;
using System.Reflection;
using CrunchEconV3.Handlers;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;

namespace CrunchEconV3.PlugAndPlay
{
    [PatchShim]
    public static class KeenStoreManagement
    {
        internal static readonly MethodInfo updateMethod =
            typeof(MySessionComponentEconomy).GetMethod("UpdateStations",
                BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo updatePatch =
            typeof(KeenStoreManagement).GetMethod(nameof(Update), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Adding keen patch");
            ctx.GetPattern(updateMethod).Prefixes.Add(updatePatch);
        }

        public static bool Update()
        {
        //    Core.Log.Info("Store Update");
           
            foreach (KeyValuePair<long, MyFaction> faction in MySession.Static.Factions)
            {
                foreach (MyStation station in faction.Value.Stations)
                {
                    StationHandler.SetNPCNeedsRefresh(station.StationEntityId, DateTime.Now.AddSeconds(MyAPIGateway.Session.SessionSettings.EconomyTickInSeconds));
                    
                    //long newid = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                    //MyStoreItem myStoreItem2 = new MyStoreItem(newid, amount, price, StoreItemTypes.Offer, ItemTypes.Grid);
                    //myStoreItem2.IsCustomStoreItem = true;
                    //myStoreItem2.PrefabName = "L531StarterShip";
                    //station.StoreItems.Add(myStoreItem2);
                }
            }

            return true;
        }
    }
}
