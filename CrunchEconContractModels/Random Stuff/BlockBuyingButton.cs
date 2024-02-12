using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Network;
using VRage.Replication;
using VRageMath;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class BlockBuyingButton
    {
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Patching button for grid sales");
            ctx.GetPattern(buttonMethod).Prefixes.Add(buttonPatch);
        }



        public static void UnPatch(PatchContext ctx)
        {
            Core.Log.Info("Unpatching button for grid sales");
            ctx.GetPattern(buttonMethod).Prefixes.Remove(buttonPatch);

            var method = typeof(MyCubeGrid).GetMethod("OnChangeOwnerRequest",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            ctx.GetPattern(method).Prefixes.Add(typeof(BlockBuyingButton).GetMethod(nameof(OnChangeOwnerRequestPrefix)));

        }
        public static void OnChangeOwnerRequestPrefix(MyCubeGrid __instance, long blockId, long owner, MyOwnershipShareModeEnum shareMode)
        {
            Core.Log.Info($"OnChangeOwnerRequest method invoked. BlockId: {blockId}, Owner: {owner}, ShareMode: {shareMode}");
        }

        internal static readonly MethodInfo buttonMethod =
            typeof(MyButtonPanel).GetMethod("ActivateButton",
                BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo buttonPatch =
            typeof(BlockBuyingButton).GetMethod(nameof(Activate), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static Dictionary<ulong, Confirm> Confirms = new Dictionary<ulong, Confirm>();

        [ReflectedGetter(Name = "m_clientStates")]
        public static Func<MyReplicationServer, IDictionary> _clientStates;

        private const string CLIENT_DATA_TYPE_NAME = "VRage.Network.MyClient, VRage";
        [ReflectedGetter(TypeName = CLIENT_DATA_TYPE_NAME, Name = "Replicables")]
        public static Func<object, MyConcurrentDictionary<IMyReplicable, MyReplicableClientData>> _replicables;

        [ReflectedMethod(Name = "RemoveForClient", OverrideTypeNames = new[] { null, CLIENT_DATA_TYPE_NAME, null })]
        private static Action<MyReplicationServer, IMyReplicable, object, bool> _removeForClient;

        [ReflectedMethod(Name = "ForceReplicable")]
        private static Action<MyReplicationServer, IMyReplicable, Endpoint> _forceReplicable;
#pragma warning restore 649
        public static bool Activate(MyButtonPanel __instance, int index)
        {
            if (string.IsNullOrEmpty(__instance.CustomData))
                return true;

            //  Core.Log.Info($"{index}");

            string customData = __instance.CustomData;
            try
            {
                var sale = JsonConvert.DeserializeObject<List<Purchase>>(customData);
                var actualSale = sale.FirstOrDefault(x => x.ButtonIndex == index);
                if (actualSale == null)
                {
                    Core.Log.Info($"No button sale found for index {index}");
                    return false;
                }
                var owningFac = MySession.Static.Factions.TryGetPlayerFaction(__instance.OwnerId);
                if (owningFac == null)
                {
                    return false;
                }

                if (Core.StationStorage.GetAll().Any(x => x.GetGrid() != null && x.GetGrid().EntityId == __instance.CubeGrid.EntityId && owningFac.Tag.Equals(x.FactionTag)))
                {
                    ulong steamId = MyEventContext.Current.Sender.Value;
                    var terminals = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(__instance.CubeGrid);
                    var blocks = new List<IMyTerminalBlock>();
                    terminals.GetBlocks(blocks);
                    var BlocksWithId = blocks.Where(x =>
                        x.CustomData != null && x.CustomData == $"PURCHASEID:{actualSale.PurchaseId}");
                    if (!BlocksWithId.Any())
                    {
                        Core.SendMessage("Block Sales", "No purchasable blocks found for that ID", Color.Red, steamId);
                        return false;
                    }
                    if (MySession.Static.Players.TryGetPlayerBySteamId(steamId, out var player))
                    {
                        if (actualSale.ReputationRequired)
                        {
                            var fac = MySession.Static.Factions.TryGetFactionByTag(actualSale.FacTagForReputation);
                            if (fac == null)
                            {
                                Core.SendMessage("Grid Sales", "Faction for reputation requirement not found", Color.Red, steamId);
                                return false;
                            }

                            var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(
                                player.Identity.IdentityId, fac.FactionId);

                            if (actualSale.Reputation > 0)
                            {
                                if (rep.Item2 <= actualSale.Reputation)
                                {
                                    var text =
                                        $"Reputation requirement not met, required {actualSale.ReputationRequired} with {actualSale.FacTagForReputation}";
                                    Core.SendMessage("Grid Sales", text, Color.Red, steamId);
                                    var message = new NotificationMessage(text, 5000, "Red");
                                    ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                    return false;
                                }
                            }
                            else
                            {
                                if (rep.Item2 >= actualSale.Reputation)
                                {

                                    var text =
                                        $"Reputation requirement not met, required {actualSale.ReputationRequired} with {actualSale.FacTagForReputation}";
                                    Core.SendMessage("Grid Sales", text, Color.Red, steamId);
                                    var message = new NotificationMessage(text, 5000, "Red");
                                    ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                    return false;
                                }
                            }

                        }

                        if (EconUtils.getBalance(player.Identity.IdentityId) >= actualSale.Price)
                        {
                            if (Confirms.TryGetValue(steamId, out var confirmation))
                            {
                                if (DateTime.Now > confirmation.Expire)
                                {
                                    var text = $"Confirmation expired.";
                                    Core.SendMessage("Grid Sales", text, Color.Red, steamId);
                                    var message = new NotificationMessage(text, 5000, "Red");
                                    ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                    Confirms.Remove(steamId);
                                    return false;
                                }

                                if (confirmation.Index != index)
                                {
                                    var text = $"Confirmation not valid. Try again.";
                                    Core.SendMessage("Grid Sales", text, Color.Red, steamId);
                                    var message = new NotificationMessage(text, 5000, "Red");
                                    ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                    return false;
                                }

                                EconUtils.takeMoney(player.Identity.IdentityId, actualSale.Price);
                                Confirms.Remove(steamId);

                                foreach (var block in BlocksWithId)
                                {
                                    MyCubeBlock slim = (MyCubeBlock)block;
                                    slim.ChangeOwner(player.Identity.IdentityId, MyOwnershipShareModeEnum.None);
                                    
                                }
                               // Core.Session.Torch.Invoke(() =>
                              //      {
                                         bool consumed = true;
                                var manager = Core.Session.Managers.GetManager<CommandManager>();
                                manager?.HandleCommand("!entities refresh", player.Id.SteamId, ref consumed);
                               //     });
                                return false;

                            }
                            else
                            {
                                var confirm = new Confirm()
                                {
                                    Expire = DateTime.Now.AddSeconds(5),
                                    Index = index
                                };

                                Confirms.Remove(steamId);
                                Confirms.Add(steamId, confirm);
                                var text = $"Press button again within 5 seconds to confirm sale.";
                                Core.SendMessage("Grid Sales", text, Color.Green, steamId);
                                var message = new NotificationMessage(text, 5000, "Green");
                                ModCommunication.SendMessageTo(message, player.Id.SteamId);
                            }
                        }
                        else
                        {
                            Core.SendMessage("Grid Sales", $"You cannot afford the purchase price of {actualSale.Price:##,###}", Color.Red, steamId);
                        }
                    }

                }
            }
            catch (Exception e)
            {
                Core.Log.Error(e);
                return true;
            }
            return true;
        }

        public class Confirm
        {
            public int Index { get; set; }
            public DateTime Expire { get; set; }
        }
        public class Purchase
        {
            public int ButtonIndex { get; set; }
            public string PurchaseId { get; set; }
            public long Price { get; set; }
            public bool ReputationRequired { get; set; }
            public string FacTagForReputation { get; set; }
            public int Reputation { get; set; }
        }
    }
}
