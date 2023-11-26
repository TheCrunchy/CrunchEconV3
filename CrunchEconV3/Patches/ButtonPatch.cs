using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.APIs;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Game.World;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using VRage.Game.Components;
using VRage.Network;

namespace CrunchEconV3.Patches
{
    [PatchShim]
    public static class ButtonPatch
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
            typeof(ButtonPatch).GetMethod(nameof(Activate), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static bool Activate(MyButtonPanel __instance, int index)
        {
            if (string.IsNullOrEmpty(__instance.CustomData))
                return true;
            string customData = __instance.CustomData;
            try
            {
                var sale = JsonConvert.DeserializeObject<GridSale>(customData);
                var path = $"{Core.path}//Grids//{sale.PrefabName}";
                if (Core.StationStorage.GetAll().Any(x => x.GetGrid() != null && x.GetGrid().EntityId == __instance.CubeGrid.EntityId && __instance.GetOwnerFactionTag().Equals(x.FactionTag)))
                {
                    if (File.Exists(path))
                    {
                        ulong steamId = MyEventContext.Current.Sender.Value;

                        if (MySession.Static.Players.TryGetPlayerBySteamId(steamId, out var player))
                        {
                            if (sale.ReputationRequired)
                            {
                                var fac = MySession.Static.Factions.TryGetFactionByTag(sale.FacTagForReputation);
                                if (fac == null)
                                {
                                    return false;
                                }

                                var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(
                                    player.Identity.IdentityId, fac.FactionId);

                                if (sale.Reputation > 0)
                                {
                                    if (rep.Item2 <= sale.Reputation)
                                    {
                                        return false;
                                    }
                                }
                                else
                                {
                                    if (rep.Item2 >= sale.Reputation)
                                    {
                                        return false;
                                    }
                                }
                           
                            }
                            if (EconUtils.getBalance(player.Identity.IdentityId) >= sale.Price)
                            {
                                if (GridManager.LoadGrid(path, player.GetPosition(), false, player.Id.SteamId,
                                        sale.PrefabName.Replace(".sbc", ""), false))
                                {
                                    EconUtils.takeMoney(player.Identity.IdentityId, sale.Price);
                                    return false;
                                }
                            }
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

        public class GridSale
        {
            public string PrefabName { get; set; }
            public long Price { get; set; }
            public bool ReputationRequired { get; set; }
            public string FacTagForReputation { get; set; }
            public int Reputation { get; set; }
        }

        //{
        //    "prefabName": null,
        //    "price": 0
        //}
        /*
         {
             {
      "PrefabName": "pirate.sbc",
      "Price": 50000,
      "ReputationRequired": false,
      "FacTagForReputation": "DAVE",
      "Reputation": 1500
    }
          }
        */
    }
}
