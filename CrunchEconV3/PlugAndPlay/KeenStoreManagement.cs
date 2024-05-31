using System;
using System.Collections.Generic;
using System.Reflection;
using CrunchEconV3.Handlers;
using Sandbox.Definitions;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game;

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
                    //Type myStationGeneratorType = station.GetType().Assembly.GetType("Sandbox.Game.World.Generator.MyStationGenerator");
                    //// Step 2: Get the method info for the method you want to invoke
                    //MethodInfo getStationTypeDefinitionMethod = myStationGeneratorType.GetMethod(
                    //    "GetStationTypeDefinition",
                    //    BindingFlags.NonPublic | BindingFlags.Static
                    //);
                    //if (getStationTypeDefinitionMethod != null)
                    //{
                    //    // Step 4: Invoke the method
                    //    object result = getStationTypeDefinitionMethod.Invoke(null, new object[] { station.Type });

                    //    // Step 5: Cast the result to the appropriate type
                    //    MyStationsListDefinition stationDefinition = result as MyStationsListDefinition;

                    //    // Use the result (stationDefinition) as needed
                    //    if (stationDefinition != null)
                    //    {
                    //        Console.WriteLine("Method invoked successfully and result obtained.");
                    //        Core.Log.Info($"{result}");

                    //    }
                    //    else
                    //    {
                    //        Console.WriteLine("Method invocation failed or returned null.");
                    //    }
                    //}
                  //  var econComp = MySession.Static.GetComponent<MySessionComponentEconomy>();
                   // econComp.ForceEconomyTick();
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
