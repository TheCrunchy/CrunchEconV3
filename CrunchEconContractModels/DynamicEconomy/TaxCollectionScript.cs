using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Utils;
using CrunchGroup.Handlers;
using CrunchGroup.Models;
using CrunchGroup.Territories.Interfaces;
using CrunchGroup.Territories.Models;
using CrunchGroup.Territories.PointOwners;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.Managers;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.Network;
using VRageMath;

namespace CrunchEconContractModels.DynamicEconomy
{
    [PatchShim]
    public static class TaxCollectionScript
    {
        private static ITorchPlugin GroupPlugin;
        public static MethodInfo GetTerritoriesMethod;

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(logupdate2).Suffixes.Add(storePatchLog2);
            ctx.GetPattern(update).Prefixes.Add(storePatchTwo);
        }

        internal static readonly MethodInfo logupdate2 =
            typeof(MyStoreBlock).GetMethod("SendBuyItemResult", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(string), typeof(long), typeof(int), typeof(MyStoreBuyItemResults), typeof(EndpointId) }, null) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchLog2 =
            typeof(TaxCollectionScript).GetMethod(nameof(StorePatchMethodBuy), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");
        internal static readonly MethodInfo update =
            typeof(MyStoreBlock).GetMethod("BuyFromPlayer", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchTwo =
            typeof(TaxCollectionScript).GetMethod(nameof(StorePatchMethod), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        private static Dictionary<long, TransactionData> transactionDataDictionary = new Dictionary<long, TransactionData>();

        public static void StorePatchMethodBuy(long id, string name, long price, int amount, MyStoreBuyItemResults result, EndpointId targetEndpoint)
        {
            if (transactionDataDictionary.TryGetValue(id, out TransactionData data) && result == MyStoreBuyItemResults.Success)
            {
                ApplyTax(data.Store, (long)(amount * price));
            }
            transactionDataDictionary.Remove(id);
        }

        public static Boolean StorePatchMethod(long id, int amount, long targetEntityId, MyPlayer player, MyAccountInfo playerAccountInfo, MyStoreBlock __instance)
        {
            if (__instance is MyStoreBlock store)
            {
                MyStoreItem storeItem = store.PlayerItems.FirstOrDefault(item => item.Id == id);
                if (storeItem == null || storeItem.IsCustomStoreItem) return true;

                var transactionData = new TransactionData
                {
                    Id = id,
                    Amount = amount,
                    TargetEntityId = targetEntityId,
                    Player = player,
                    PlayerAccountInfo = playerAccountInfo,
                    Store = store
                };

                // Store the transaction data in the dictionary
                transactionDataDictionary[id] = transactionData;
            }
            return true;
        }

        //add the rest of the shit thats used for stores
        public static void ApplyTax(MyStoreBlock store, long moneysAmount)
        {
            var territory = GetTerritoryInside(store.CubeGrid.PositionComp.GetPosition());
            if (territory == null)
            {
                //not taxable 
                return;
            }

            var taxRate = 0.1m;
            if (territory.RandomJsonStuff.TryGetValue("TaxRate", out var rate))
            {
                taxRate = decimal.Parse(rate);
            }

            var afterTax = moneysAmount * taxRate;

            var owner = territory.Owner;
            switch (owner.GetOwner())
            {
                case IMyFaction faction:
                {
                    EconUtils.addMoney(faction.FactionId, (long)afterTax);
                    break;
                }
                case Group group:
                {
                    EconUtils.addMoney(group.GroupLeader, (long)afterTax);
                    break;
                }

            }
        }



        public static Territory GetTerritoryInside(Vector3 position)
        {
            foreach (var territory in GetAllTerritories())
            {
                var distance = Vector3.Distance(position, territory.Position);
                if (distance <= territory.RadiusDistance)
                {
                    return territory;
                }
            }

            return null;
        }

        public static List<Territory> GetAllTerritories()
        {
            return CrunchGroup.Core.GetAllTerritories();
          //  return GetTerritoriesMethod.Invoke(null, null) as List<Territory>;
        }

        public class TransactionData
        {
            public long Id { get; set; }
            public int Amount { get; set; }
            public long TargetEntityId { get; set; }
            public MyPlayer Player { get; set; }
            public MyAccountInfo PlayerAccountInfo { get; set; }
            public MyStoreBlock Store { get; set; }
        }

    }
}
