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
                if (Core.StationStorage.GetAll().Any(x => x.GetGrid().EntityId == __instance.CubeGrid.EntityId))
                {
                    if (File.Exists(path))
                    {

                        ulong steamId = MyEventContext.Current.Sender.Value;

                        if (MySession.Static.Players.TryGetPlayerBySteamId(steamId, out var player))
                        {
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
        }

        //{
        //    "prefabName": null,
        //    "price": 0
        //}
    }
}
