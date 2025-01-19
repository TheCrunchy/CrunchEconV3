using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Utils;
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
    public class StoreReputationRequirement
    {
        public const string BlockOwner = "BLOCKOWNER";

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(update).Prefixes.Add(storePatch);
            ctx.GetPattern(updateStation).Prefixes.Add(storePatchKeen);


            ItemsWithRequirements.Add("MyObjectBuilder_Ingot/Iron", new ReputationRequirement() { ReputationRequired = 500, FactionTag = BlockOwner });
            ItemsWithRequirements.Add("MyObjectBuilder_Ingot/Banana", new ReputationRequirement() { ReputationRequired = 500, FactionTag = "SPRT" });
        }



        internal static readonly MethodInfo update =
            typeof(MyStoreBlock).GetMethod("BuyFromPlayer", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo updateStation =
            typeof(MyStoreBlock).GetMethod("BuyFromStation", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatch =
            typeof(StoreReputationRequirement).GetMethod(nameof(StorePatchMethod),
                BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchKeen =
            typeof(StoreReputationRequirement).GetMethod(nameof(BuyFromStation),
                BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static Dictionary<string, ReputationRequirement> ItemsWithRequirements =
            new Dictionary<string, ReputationRequirement>();

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

            if (storeItem.IsCustomStoreItem || storeItem.Item == null)
            {
                return true;
            }
            if (ItemsWithRequirements.TryGetValue(
                             $"{storeItem.Item.Value.TypeIdString}/{storeItem.Item.Value.SubtypeId}", out var requirement))
            {

                int playersRep = 0;
                switch (requirement.FactionTag)
                {
                    case BlockOwner:
                        {
                            var fac = MySession.Static.Factions.TryGetFactionById(station.FactionId);
                            var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(
                                player.Identity.IdentityId, fac.FactionId);
                            playersRep = rep.Item2;
                            break;
                        }
                    default:
                        {
                            var fac = MySession.Static.Factions.TryGetFactionByTag(requirement.FactionTag);
                            var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(
                                player.Identity.IdentityId, fac.FactionId);
                            playersRep = rep.Item2;
                            break;
                        }
                }

                if (requirement.ReputationRequired > 0)
                {
                    if (playersRep < requirement.ReputationRequired)
                    {
                        SendRequiredMessage(id, ref amount, player, requirement);
                        return false;
                    }
                }
                else
                {
                    if (playersRep > requirement.ReputationRequired)
                    {
                        SendRequiredMessage(id, ref amount, player, requirement);
                        return false;
                    }
                }

            }

            return true;
        }

        public static Boolean StorePatchMethod(long id,
            ref int amount,
            long targetEntityId,
            MyPlayer player,
            MyAccountInfo playerAccountInfo, MyStoreBlock __instance)
        {

            if (__instance is MyStoreBlock store)
            {
                if (Core.StationStorage.GetAll().Any(x =>
                        x.GetGrid() != null && x.GetGrid().EntityId == __instance.CubeGrid.EntityId))
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
                    if (storeItem.IsCustomStoreItem || storeItem.Item == null)
                    {
                        return true;
                    }
                    if (ItemsWithRequirements.TryGetValue(
                            $"{storeItem.Item.Value.TypeIdString}/{storeItem.Item.Value.SubtypeId}", out var requirement))
                    {

                        int playersRep = 0;
                        switch (requirement.FactionTag)
                        {
                            case BlockOwner:
                                {
                                    var owner = FacUtils.GetOwner(__instance.CubeGrid);
                                    var fac = FacUtils.GetPlayersFaction(owner);
                                    var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(
                                        player.Identity.IdentityId, fac.FactionId);
                                    playersRep = rep.Item2;
                                    break;
                                }
                            default:
                                {
                                    var fac = MySession.Static.Factions.TryGetFactionByTag(requirement.FactionTag);
                                    var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(
                                        player.Identity.IdentityId, fac.FactionId);
                                    playersRep = rep.Item2;
                                    break;
                                }
                        }

                        if (requirement.ReputationRequired > 0)
                        {
                            if (playersRep < requirement.ReputationRequired)
                            {
                                SendRequiredMessage(id, ref amount, player, requirement);
                                return false;
                            }
                        }
                        else
                        {
                            if (playersRep > requirement.ReputationRequired)
                            {
                                SendRequiredMessage(id, ref amount, player, requirement);
                                return false;
                            }
                        }
                    

                    }
                }

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

        private static bool SendRequiredMessage(long id, ref int amount, MyPlayer player, ReputationRequirement requirement)
        {
            var text = "";
            if (requirement.ReputationRequired > 0)
            {
                text = $"Reputation of >= {requirement.ReputationRequired} with {requirement.FactionTag} required.";
            }
            else
            {
                text = $"Reputation of <= {requirement.ReputationRequired} with {requirement.FactionTag} required.";
            }

            SendMessage("Reputation Requirement", text, Color.Red, player.Id.SteamId);
            return false;

        }

        public class ReputationRequirement
        {
            public int ReputationRequired = 50;
            public string FactionTag = "BLOCKOWNER";
        }

    }
}