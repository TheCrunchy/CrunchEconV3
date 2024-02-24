using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.StationLogics;
using CrunchEconV3;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace CrunchEconContractModels.RandomShit
{
    [PatchShim]
    public static class PrefabInsertScript
    {
        public static void Patch(PatchContext ctx)
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(9825, MessageHandler);

            MethodInfo method = typeof(MyStoreBlock).GetMethod("BuyPrefabInternal", BindingFlags.NonPublic | BindingFlags.Instance);
            MethodInfo patchMethod = typeof(StoreManagementLogic).GetMethod(nameof(BuyPrefabInternalPatch), BindingFlags.NonPublic | BindingFlags.Static);
            ctx.GetPattern(method).Prefixes.Add(patchMethod);
        }

        public static Dictionary<long, long> Safezones = new Dictionary<long, long>();

        private static bool BuyPrefabInternalPatch(
            MyStoreBlock __instance,
            MyStoreItem storeItem,
            int amount,
            MyPlayer player,
            MyFaction faction,
            Vector3D storePosition,
            ref long safezoneId,
            MyStationTypeEnum stationType,
            MyEntity entity,
            long totalPrice)
        {
            if (safezoneId == 0l)
            {
                if (Safezones.TryGetValue(__instance.EntityId, out var foundZone))
                {
                    safezoneId = foundZone;
                    return true;
                }


                BoundingSphereD sphere = new BoundingSphereD(storePosition, 1000);

                foreach (MySafeZone zone in MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere)
                             .OfType<MySafeZone>())
                {
                    Safezones[__instance.EntityId] = zone.EntityId;
                    safezoneId = zone.EntityId;
                    return true;
                }
            }
            return true;
        }
        private static void MessageHandler(ushort handlerId, byte[] message, ulong steamId, bool isServer)
        {
            if (isServer)
            {
                var data = (PrefabInsert)MyAPIGateway.Utilities.SerializeFromBinary<PrefabInsert>(message);

                if (data != null)
                {
                    if (MyAPIGateway.Entities.TryGetEntityById(data.StoreEntityId, out var entity))
                    {
                        var store = entity as MyStoreBlock;
                        long newid2 = MyEntityIdentifier.AllocateId(MyEntityIdentifier.ID_OBJECT_TYPE.STORE_ITEM, MyEntityIdentifier.ID_ALLOCATION_METHOD.RANDOM);
                        MyStoreItem myStoreItem3 = new MyStoreItem(newid2, (int)data.Amount, (int)data.Price, StoreItemTypes.Offer, ItemTypes.Grid);
                        myStoreItem3.IsCustomStoreItem = true;
                        myStoreItem3.PrefabName = data.PrefabName;
                        store.PlayerItems.Add(myStoreItem3);
                    }
                }
            }
            else
            {
                Core.Log.Info("Message wasnt from server");
            }
        }
    }


    [ProtoContract]
    public class PrefabInsert
    {
        [ProtoMember(1)]
        public long StoreEntityId { get; set; }

        [ProtoMember(2)]
        public string PrefabName { get; set; }


        [ProtoMember(3)]
        public long Amount { get; set; }

        [ProtoMember(4)]
        public long Price { get; set; }
    }
}
