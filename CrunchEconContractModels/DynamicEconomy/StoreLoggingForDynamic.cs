using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CrunchEconV3;
using NLog;
using NLog.Config;
using NLog.Targets;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Network;

namespace CrunchEconContractModels.DynamicEconomy
{
    [PatchShim]
    public static class StoreLoggingForDynamic
    {
        internal static readonly MethodInfo logupdate =
         typeof(MyStoreBlock).GetMethod("SendSellItemResult", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(string), typeof(long), typeof(int), typeof(MyStoreSellItemResults) }, null) ??
         throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchLog =
            typeof(StoreLoggingForDynamic).GetMethod(nameof(StorePatchMethodSell), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo logupdate2 =
  typeof(MyStoreBlock).GetMethod("SendBuyItemResult", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(string), typeof(long), typeof(int), typeof(MyStoreBuyItemResults), typeof(EndpointId) }, null) ??
  throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchLog2 =
            typeof(StoreLoggingForDynamic).GetMethod(nameof(StorePatchMethodBuy), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo update =
            typeof(MyStoreBlock).GetMethod("BuyFromPlayer", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo updateTwo =
       typeof(MyStoreBlock).GetMethod("SellToPlayer", BindingFlags.Instance | BindingFlags.NonPublic) ??
       throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatch =
            typeof(StoreLoggingForDynamic).GetMethod(nameof(StorePatchMethod), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchTwo =
             typeof(StoreLoggingForDynamic).GetMethod(nameof(StorePatchMethodTwo), BindingFlags.Static | BindingFlags.Public) ??
             throw new Exception("Failed to find patch method");

        public static Logger log = LogManager.GetLogger("Stores");

        public static void ApplyLogging()
        {
            var rules = LogManager.Configuration.LoggingRules;

            for (int i = rules.Count - 1; i >= 0; i--)
            {

                var rule = rules[i];

                if (rule.LoggerNamePattern == "Stores")
                    rules.RemoveAt(i);
            }

            var logTarget = new FileTarget
            {
                FileName = "Logs/Stores-" + DateTime.Now.Day + "-" + DateTime.Now.Month + "-" + DateTime.Now.Year + ".txt",
                Layout = "${var:logStamp} ${var:logContent}"
            };

            var logRule = new LoggingRule("Stores", LogLevel.Debug, logTarget)
            {
                Final = true
            };

            rules.Insert(0, logRule);

            LogManager.Configuration.Reload();
        }

        public static void Patch(PatchContext ctx)
        {
            ApplyLogging();
            ctx.GetPattern(logupdate).Suffixes.Add(storePatchLog);
            ctx.GetPattern(logupdate2).Suffixes.Add(storePatchLog2);
            ctx.GetPattern(update).Prefixes.Add(storePatch);
            // ctx.GetPattern(update).Prefixes.Add(storePatch);
            ctx.GetPattern(updateTwo).Prefixes.Add(storePatchTwo);
   
            Directory.CreateDirectory(NexusPath);
        }

        public static Dictionary<long, string> PossibleLogs = new Dictionary<long, string>();

        public static string NexusPath = $"{Core.path}//EconEntries//";
        public static void StorePatchMethodSell(long id, string name, long price, int amount, MyStoreSellItemResults result)
        {
            if (result == MyStoreSellItemResults.Success && PossibleLogs.ContainsKey(id))
            {
                log.Info(PossibleLogs[id]);
                WriteLog(id); // pass the id directly
            }
        }

        public static void StorePatchMethodBuy(long id, string name, long price, int amount, MyStoreBuyItemResults result, EndpointId targetEndpoint)
        {
            if (result == MyStoreBuyItemResults.Success && PossibleLogs.ContainsKey(id))
            {
                log.Info(PossibleLogs[id]);

                WriteLog(id); // pass the id directly
            }
          
        }

        private static void WriteLog(long id)
        {
            Task.Run(async () =>
            {
                File.WriteAllText($"{NexusPath}{Guid.NewGuid()}.txt", PossibleLogs[id]);
                PossibleLogs.Remove(id);
            });
            return;

        }
        //PossibleLogs.Add(id,
        //$"SteamId:,action:,Amount:,TypeId:,SubTypeId:,TotalMoney:GridId:,FacTag:,GridName:");
        public static Boolean StorePatchMethod(long id,
            int amount,
            long targetEntityId,
            MyPlayer player,
            MyAccountInfo playerAccountInfo, MyStoreBlock __instance)
        {
            if (__instance is MyStoreBlock store)
            {
                MyStoreItem storeItem = (MyStoreItem)null;
                foreach (MyStoreItem playerItem in store.PlayerItems)
                {
                    if (playerItem.Id == id)
                    {
                        storeItem = playerItem;
                        break;
                    }
                }
                if (storeItem == null)
                {

                    return true;
                }
                if (storeItem.IsCustomStoreItem || storeItem.ItemType == ItemTypes.Hydrogen || storeItem.ItemType == ItemTypes.Oxygen || storeItem.ItemType == ItemTypes.Grid)
                {
                    return true;
                }
                if (!PossibleLogs.ContainsKey(id))
                {
                    PossibleLogs.Add(id,
                        $"{player.Id.SteamId},bought,{amount},{storeItem.Item.Value.TypeIdString}," +
                        $"{storeItem.Item.Value.SubtypeName},{storeItem.PricePerUnit * (long)amount}," +
                        $"{store.CubeGrid.EntityId},{store.GetOwnerFactionTag()},{store.CubeGrid.DisplayName},{store.DisplayNameText},{DateTime.Now}");
                }

            }
            return true;
        }

        public static Boolean StorePatchMethodTwo(long id, int amount, long sourceEntityId, MyPlayer player, MyStoreBlock __instance)
        {
            if (__instance is MyStoreBlock store)
            {
                MyStoreItem myStoreItem = (MyStoreItem)null;

                foreach (MyStoreItem playerItem in store.PlayerItems)
                {
                    if (playerItem.Id == id)
                    {
                        myStoreItem = playerItem;
                        break;
                    }
                }
                if (myStoreItem == null)
                {
                    return false;
                }

                if (!PossibleLogs.ContainsKey(id))
                {
                    PossibleLogs.Add(id,
                        $"{player.Id.SteamId},sold,{amount},{myStoreItem.Item.Value.TypeIdString}," +
                        $"{myStoreItem.Item.Value.SubtypeName},{myStoreItem.PricePerUnit * (long)amount}," +
                        $"{store.CubeGrid.EntityId},{store.GetOwnerFactionTag()},{store.CubeGrid.DisplayName},{store.DisplayNameText},{DateTime.Now}");
                }
            }

            return true;
        }
    }
}
