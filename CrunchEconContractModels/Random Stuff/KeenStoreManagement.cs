using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using NLog.Fluent;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;

namespace CrunchEconContractModels.Random_Stuff
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
            //Core.Log.Info("Store Update");
            int price = 500;

            int amount = 50000;
            foreach (KeyValuePair<long, MyFaction> faction in MySession.Static.Factions)
            {
                foreach (MyStation station in faction.Value.Stations)
                {
                    long newid = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                    MyStoreItem myStoreItem2 = new MyStoreItem(newid, amount, price, StoreItemTypes.Offer, ItemTypes.Grid);
                    myStoreItem2.IsCustomStoreItem = true;
                    myStoreItem2.PrefabName = "L531StarterShip";

                    station.StoreItems.Add(myStoreItem2);

                }
            }

            Core.Log.Info("Update happened");
            return true;
        }
    }
}
