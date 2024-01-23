using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using NLog;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Network;
using VRageMath;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public class StorePurchaseLimit
    {
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(logupdate2).Suffixes.Add(storePatchLog2);
            ctx.GetPattern(update).Prefixes.Add(storePatch);
            ctx.GetPattern(updateStation).Prefixes.Add(storePatchKeen);


            LimitedItems.Add("MyObjectBuilder_Ingot/Iron",
                new LimitConfig() { MaximumPurchase = 50, SecondsBeforeNextPurchase = 600 });
        }

        internal static readonly MethodInfo logupdate2 =
            typeof(MyStoreBlock).GetMethod("SendBuyItemResult", BindingFlags.Instance | BindingFlags.NonPublic, null,
                new Type[]
                {
                    typeof(long), typeof(string), typeof(long), typeof(int), typeof(MyStoreBuyItemResults),
                    typeof(EndpointId)
                }, null) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchLog2 =
            typeof(StorePurchaseLimit).GetMethod(nameof(StorePatchMethodBuy),
                BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo update =
            typeof(MyStoreBlock).GetMethod("BuyFromPlayer", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo updateStation =
            typeof(MyStoreBlock).GetMethod("BuyFromStation", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatch =
            typeof(StorePurchaseLimit).GetMethod(nameof(StorePatchMethod), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchKeen =
            typeof(StorePurchaseLimit).GetMethod(nameof(BuyFromStation), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");
   

        public static Dictionary<ulong, PurchaseLimitsHolder> NextPurchase =
            new Dictionary<ulong, PurchaseLimitsHolder>();

        public static Dictionary<string, LimitConfig> LimitedItems = new Dictionary<string, LimitConfig>();

        public static Dictionary<long, PossibleSale> PossibleSales = new Dictionary<long, PossibleSale>();

        public static void StorePatchMethodBuy(long id, string name, long price, int amount,
            MyStoreBuyItemResults result, EndpointId targetEndpoint)
        {
           
            if (result == MyStoreBuyItemResults.Success && PossibleSales.TryGetValue(id, out var sale))
            {
                if (NextPurchase.TryGetValue(sale.SteamId, out var playersLimits))
                {
                    if (playersLimits.Limits.TryGetValue(sale.ItemsId, out var boughtLimit))
                    {
                        boughtLimit.AmountPurchased += sale.Amount;
                    }

                }
            }
            PossibleSales.Remove(id);
        }

        public static bool BuyFromStation(
            long id,
            ref int amount,
            MyPlayer player,
            MyAccountInfo playerAccountInfo,
            MyStation station,
            long targetEntityId,
            long lastEconomyTick)

        {
            MyStoreItem storeItem = (MyStoreItem)null;
            foreach (MyStoreItem playerItem in station.StoreItems)
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
            var itemid = $"{storeItem.Item.Value.TypeIdString}/{storeItem.Item.Value.SubtypeId}";
            return DoLimitChecks(id, ref amount, player, storeItem, itemid);
        }

        //ADD YOUR FACTION TAG CHECKING HERE SO PLAYER STORES DONT GET LIMITED
        //ADD YOUR FACTION TAG CHECKING HERE SO PLAYER STORES DONT GET LIMITED
        //ADD YOUR FACTION TAG CHECKING HERE SO PLAYER STORES DONT GET LIMITED
        //ADD YOUR FACTION TAG CHECKING HERE SO PLAYER STORES DONT GET LIMITED
        //ADD YOUR FACTION TAG CHECKING HERE SO PLAYER STORES DONT GET LIMITED
        //ADD YOUR FACTION TAG CHECKING HERE SO PLAYER STORES DONT GET LIMITED
        public static Boolean StorePatchMethod(long id,
            ref int amount,
            long targetEntityId,
            MyPlayer player,
            MyAccountInfo playerAccountInfo, MyStoreBlock __instance)
        {

            if (__instance is MyStoreBlock store)
            {
                //Add some differentiation here to only apply to your NPCs if they arent setup as keen stations 
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
                var itemid = $"{storeItem.Item.Value.TypeIdString}/{storeItem.Item.Value.SubtypeId}";
               return DoLimitChecks(id, ref amount, player, storeItem, itemid);
            }
            return true;
        }
        public static void SendMessage(string author, string message, Color color, ulong steamID)
        {
            Logger _chatLog = LogManager.GetLogger("Chat");
            ScriptedChatMsg scriptedChatMsg1 = new ScriptedChatMsg();
            scriptedChatMsg1.Author = author;
            scriptedChatMsg1.Text = message;
            scriptedChatMsg1.Font = "White";
            scriptedChatMsg1.Color = color;
            scriptedChatMsg1.Target = Sync.Players.TryGetIdentityId(steamID);
            ScriptedChatMsg scriptedChatMsg2 = scriptedChatMsg1;
            MyMultiplayerBase.SendScriptedChatMessage(ref scriptedChatMsg2);
        }
        private static bool DoLimitChecks(long id, ref int amount, MyPlayer player, MyStoreItem storeItem, string itemid)
        {
            if (LimitedItems.TryGetValue(itemid, out var limit))
            {
                if (amount > limit.MaximumPurchase)
                {
                    amount = limit.MaximumPurchase;
                }


                if (NextPurchase.TryGetValue(player.Id.SteamId, out var playersLimits))
                {
                    if (playersLimits.Limits.TryGetValue(itemid, out var boughtLimit))
                    {
                        if (boughtLimit.AmountPurchased >= limit.MaximumPurchase)
                        {
                            if (boughtLimit.NextPurchaseTime >= DateTime.Now)
                            {
                                amount = 0;
                                var timeUntil = (boughtLimit.NextPurchaseTime - DateTime.Now).TotalSeconds;
                                var text =
                                    $"Limit of {limit.MaximumPurchase} reached, wait {timeUntil:##,##} seconds for refresh.";
                                SendMessage("Store Limit", text, Color.Red, player.Id.SteamId);
                                return false;
                            }
                            else
                            {
                                boughtLimit.NextPurchaseTime = DateTime.Now.AddSeconds(limit.SecondsBeforeNextPurchase);
                                boughtLimit.AmountPurchased = 0;
                            }
                        }

                        if (boughtLimit.AmountPurchased + amount > limit.MaximumPurchase)
                        {
                            amount = limit.MaximumPurchase - boughtLimit.AmountPurchased;
                        }
                    }
                    else
                    {
                        var limits = new PurchaseLimit()
                        {
                            AmountPurchased = amount,
                            NextPurchaseTime = DateTime.Now.AddSeconds(limit.SecondsBeforeNextPurchase)
                        };
                        playersLimits.Limits.Add(itemid,limits);
                    }
                }
                else
                {
                    var limits = new PurchaseLimit()
                    {
                        AmountPurchased = 0,
                        NextPurchaseTime = DateTime.Now.AddSeconds(limit.SecondsBeforeNextPurchase)
                    };
                    NextPurchase.Add(player.Id.SteamId, new PurchaseLimitsHolder()
                    {
                        Limits = new Dictionary<string, PurchaseLimit>
                        {
                            { itemid, limits }
                        }
                    });
                }

                var possibleSale = new PossibleSale()
                {
                    Amount = amount,
                    ItemsId = itemid,
                    SteamId = player.Id.SteamId,
                };
                PossibleSales.Add(id, possibleSale);
            }

            return true;
        }
    }


    public class PurchaseLimitsHolder
    {
        public Dictionary<string, PurchaseLimit> Limits = new Dictionary<string, PurchaseLimit>();
    }

    public class PurchaseLimit
    {
        public DateTime NextPurchaseTime = DateTime.Now;
        public int AmountPurchased = 0;
    }
    public class LimitConfig
    {
        public int MaximumPurchase = 50;
        public int SecondsBeforeNextPurchase = 1200;
    }

    public class PossibleSale
    {
        public ulong SteamId;
        public string ItemsId;
        public int Amount;
    }
}
