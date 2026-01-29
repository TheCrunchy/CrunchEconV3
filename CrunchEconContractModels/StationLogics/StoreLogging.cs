using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Network;

namespace CrunchEconContractModels.StationLogics
{
	[PatchShim]
	public static class StoreLogging
	{
		internal static readonly MethodInfo logupdate =
		 typeof(MyStoreBlock).GetMethod("SendSellItemResult", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(string), typeof(long), typeof(int), typeof(MyStoreSellItemResults) }, null) ??
		 throw new Exception("Failed to find patch method");

		internal static readonly MethodInfo storePatchLog =
			typeof(StoreLogging).GetMethod(nameof(StorePatchMethodSell), BindingFlags.Static | BindingFlags.Public) ??
			throw new Exception("Failed to find patch method");

		internal static readonly MethodInfo logupdate2 =
  typeof(MyStoreBlock).GetMethod("SendBuyItemResult", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(long), typeof(string), typeof(long), typeof(int), typeof(MyStoreBuyItemResults), typeof(EndpointId) }, null) ??
  throw new Exception("Failed to find patch method");

		internal static readonly MethodInfo storePatchLog2 =
			typeof(StoreLogging).GetMethod(nameof(StorePatchMethodBuy), BindingFlags.Static | BindingFlags.Public) ??
			throw new Exception("Failed to find patch method");

		internal static readonly MethodInfo update =
			typeof(MyStoreBlock).GetMethod("BuyFromPlayer", BindingFlags.Instance | BindingFlags.NonPublic) ??
			throw new Exception("Failed to find patch method");

		internal static readonly MethodInfo updateTwo =
	   typeof(MyStoreBlock).GetMethod("SellToPlayer", BindingFlags.Instance | BindingFlags.NonPublic) ??
	   throw new Exception("Failed to find patch method");

		internal static readonly MethodInfo storePatch =
			typeof(StoreLogging).GetMethod(nameof(StorePatchMethod), BindingFlags.Static | BindingFlags.Public) ??
			throw new Exception("Failed to find patch method");

		internal static readonly MethodInfo storePatchTwo =
			 typeof(StoreLogging).GetMethod(nameof(StorePatchMethodTwo), BindingFlags.Static | BindingFlags.Public) ??
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
        }

		public static Dictionary<long, string> PossibleLogs = new Dictionary<long, string>();

		public static void StorePatchMethodSell(long id, string name, long price, int amount, MyStoreSellItemResults result)
		{
			if (result == MyStoreSellItemResults.Success && PossibleLogs.ContainsKey(id))
			{
				log.Info(PossibleLogs[id]);
				
			}
			PossibleLogs.Remove(id);

		}

		public static void StorePatchMethodBuy(long id, string name, long price, int amount, MyStoreBuyItemResults result, EndpointId targetEndpoint)
		{
			if (result == MyStoreBuyItemResults.Success && PossibleLogs.ContainsKey(id))
			{
				log.Info(PossibleLogs[id]);
				//InsertLogIntoDatabase(id); // pass the id directly
			}
			PossibleLogs.Remove(id);
		}

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
				if (storeItem.IsCustomStoreItem)
				{
					return true;
				}
				if (!PossibleLogs.ContainsKey(id))
				{
					PossibleLogs.Add(id,
						$"SteamId:{player.Id.SteamId},action:bought,Amount:{amount},TypeId:{storeItem.Item.Value.TypeIdString}," +
						$"SubTypeId:{storeItem.Item.Value.SubtypeName},TotalMoney:{storeItem.PricePerUnit * (long)amount}," +
						$"GridId:{store.CubeGrid.EntityId},FacTag:{store.GetOwnerFactionTag()},GridName:{store.CubeGrid.DisplayName}");
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
						$"SteamId:{player.Id.SteamId},action:sold,Amount:{amount},TypeId:{myStoreItem.Item.Value.TypeIdString}," +
						$"SubTypeId:{myStoreItem.Item.Value.SubtypeName},TotalMoney:{myStoreItem.PricePerUnit * (long)amount}," +
						$"GridId:{store.CubeGrid.EntityId},FacTag:{store.GetOwnerFactionTag()},GridName:{store.CubeGrid.DisplayName}");
				}
			}

			return true;
		}
    }
}
