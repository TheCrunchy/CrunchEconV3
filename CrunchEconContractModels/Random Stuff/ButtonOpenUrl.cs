using CrunchEconV3;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Game;
using Sandbox.Game.World;
using SpaceEngineers.Game.Entities.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Torch.Managers.PatchManager;
using VRage.Network;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class ButtonOpenUrl
    {
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Patching button for grid sales");
            ctx.GetPattern(buttonMethod).Prefixes.Add(buttonPatch);
        }

        internal static readonly MethodInfo buttonMethod =
            typeof(MyButtonPanel).GetMethod("ActivateButton",
                BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo buttonPatch =
            typeof(ButtonOpenUrl).GetMethod(nameof(Activate), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static List<string> AllowedFactions = new List<string>() { "FAC1234", "FAC12345" };

        public static bool Activate(MyButtonPanel __instance, int index)
        {
            if (string.IsNullOrEmpty(__instance.CustomData))
                return true;

            //  Core.Log.Info($"{index}");

            var buttonOwner = FacUtils.GetFactionTag(FacUtils.GetOwner(__instance.CubeGrid));
            if (string.IsNullOrWhiteSpace(buttonOwner))
            {
                return true;
            }
            if (buttonOwner != null && !AllowedFactions.Contains(buttonOwner));
            {
                return true;
            }

            string customData = __instance.CustomData;
            try
            {
                var url = JsonConvert.DeserializeObject<ButtonUrl>(customData);

                if (url == null)
                {
                    Core.Log.Info($"No button sale found for index {index}");
                    return false;
                }
                ulong steamId = MyEventContext.Current.Sender.Value;

                if (MySession.Static.Players.TryGetPlayerBySteamId(steamId, out var player))
                {
                    MyVisualScriptLogicProvider.OpenSteamOverlay($"https://steamcommunity.com/linkfilter/?url={url.URL}", player.Identity.IdentityId);
                }

                return false;
            }
            catch (Exception e)
            {
                Core.Log.Error(e);
            }

            return true;
        }

    }

    public class ButtonUrl
    {
        public string URL { get; set; }
    }
}


