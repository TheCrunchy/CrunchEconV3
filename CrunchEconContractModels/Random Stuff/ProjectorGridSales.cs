using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Network;
using VRageMath;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class ProjectorGridSales
    {
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(buttonMethod).Prefixes.Add(buttonPatch);
        }

        internal static readonly MethodInfo buttonMethod =
            typeof(MyButtonPanel).GetMethod("ActivateButton",
                BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo buttonPatch =
            typeof(ProjectorGridSales).GetMethod(nameof(Activate), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static Dictionary<ulong, Confirm> Confirms = new Dictionary<ulong, Confirm>();

        public static bool Activate(MyButtonPanel __instance, int index)
        {
            if (string.IsNullOrEmpty(__instance.CustomData))
                return true;

            //  Core.Log.Info($"{index}");

            string customData = __instance.CustomData;
            if (index.ToString() != customData)
            {
                return true;
            }
            try
            {
                var owningFac = MySession.Static.Factions.TryGetPlayerFaction(__instance.OwnerId);
                if (owningFac == null)
                {
                    return false;
                }

                if (Core.StationStorage.GetAll().Any(x => x.GetGrid() != null && x.GetGrid().EntityId == __instance.CubeGrid.GetBiggestGridInGroup().EntityId && owningFac.Tag == x.FactionTag))
                {
                    ulong steamId = MyEventContext.Current.Sender.Value;

                    if (MySession.Static.Players.TryGetPlayerBySteamId(steamId, out var player))
                    {
                        var projectors = __instance.CubeGrid.GetFatBlocks().OfType<MyProjectorBase>()
                            .Where(x => x.ProjectedGrid != null);
                        if (!projectors.Any())
                        {
                            Core.SendMessage("Grid Sales", "No active projections found.", color: Color.Red,
                                steamID: steamId);
                            return false;
                        }

                        GridSale actualSale = null;
                        MyProjectorBase saleData = null;
                        foreach (var projector in projectors)
                        {
                             var temp = JsonConvert.DeserializeObject<GridSale>(projector.CustomData);
                             if (temp.ButtonDataName != customData)
                             {
                                 continue;
                             }

                             saleData = projector;
                             actualSale = temp;
                        }

                        if (actualSale == null)
                        {
                            Core.SendMessage("Grid Sales", $"No projection with index .", color: Color.Red,
                                steamID: steamId);
                            return false;
                        }

                        if (actualSale.ReputationRequired)
                        {
                            var failed = true;
                            var failMessage = "";
                            var facs = actualSale.FacTagForReputation.Split(',').Select(x => x.Trim());
                            foreach (var faction in facs)
                            {
                                var fac = MySession.Static.Factions.TryGetFactionByTag(faction);
                                if (fac == null)
                                {
                                    failMessage = $"Faction for reputation requirement not found";
                             
                                    continue;
                                }

                                var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(
                                    player.Identity.IdentityId, fac.FactionId);

                                if (actualSale.Reputation > 0)
                                {
                                    if (rep.Item2 <= actualSale.Reputation)
                                    {
                                        failMessage=$"Reputation requirement not met, required {actualSale.ReputationRequired} with {actualSale.FacTagForReputation}";
                                        continue;
                                    }

                                    failed = false;
                                    break;
                                }

                                if (rep.Item2 >= actualSale.Reputation)
                                {

                                    failMessage = $"Reputation requirement not met, required {actualSale.ReputationRequired} with {actualSale.FacTagForReputation}";
                                    continue;
                                }

                                failed = false;
                                break;


                            }

                            if (failed)
                            {
                                var message = new NotificationMessage(failMessage, 5000, "Red");
                                ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                Core.SendMessage("Grid Sales", failMessage, color: Color.Red, steamID: steamId);
                                return false;
                            }
                       
                        }

                        if (EconUtils.getBalance(player.Identity.IdentityId) >= actualSale.Price)
                        {
                            if (Confirms.TryGetValue(steamId, out var confirmation))
                            {
                                if (DateTime.Now > confirmation.Expire)
                                {
                                    var text = $"Confirmation expired.";
                                    Core.SendMessage("Grid Sales", text, color: Color.Red, steamID: steamId);
                                    var message = new NotificationMessage(text, 5000, "Red");
                                    ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                    Confirms.Remove(steamId);
                                    return false;
                                }

                                if (confirmation.Index != index)
                                {
                                    var text = $"Confirmation not valid. Try again.";
                                    Core.SendMessage("Grid Sales", text, color: Color.Red, steamID: steamId);
                                    var message = new NotificationMessage(text, 5000, "Red");
                                    ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                    return false;
                                }

                                var grids = GridManagerUpdated.LoadFromProjector(saleData, player.Id.SteamId);
                                if (grids.Any())
                                {
                                    EconUtils.takeMoney(player.Identity.IdentityId, actualSale.Price);
                                    Confirms.Remove(steamId);
                                    return false;
                                }

                                Core.SendMessage("Grid Sales", $"Grid could not be spawned.",
                                    color: Color.Red, steamID: steamId);
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
                                Core.SendMessage("Grid Sales", text, color: Color.Green, steamID: steamId);
                                var message = new NotificationMessage(text, 5000, "Green");
                                ModCommunication.SendMessageTo(message, player.Id.SteamId);
                            }
                        }
                        else
                        {
                            Core.SendMessage("Grid Sales",
                                $"You cannot afford the purchase price of {actualSale.Price:##,###}",
                                color: Color.Red, steamID: steamId);
                        }
                    }

                }
            }
            catch (Exception e)
            {
                Core.Log.Error($"{e}");
                return true;
            }


            return true;
        }

        public class Confirm
        {
            public int Index { get; set; }
            public DateTime Expire { get; set; }
        }

        public class GridSale
        {
            public string ButtonDataName { get; set; }
            public long Price { get; set; }
            public bool ReputationRequired { get; set; }
            public string FacTagForReputation { get; set; }
            public int Reputation { get; set; }
        }
    }
}
