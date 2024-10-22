using System;
using System.Reflection;
using CrunchEconV3;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Weapons;
using Sandbox.Game.Weapons.Guns;
using Sandbox.Game.WorldEnvironment;
using Sandbox.Game.WorldEnvironment.Modules;
using Torch.Managers.PatchManager;
using VRage.Network;
using VRage.Utils;

namespace CrunchEconContractModels.Patches
{
    [PatchShim]
    public static class ChatPatch
    {
        private static int patchCount = 0;

        public static void Patch(PatchContext context)
        {
            var target = typeof(MyMultiplayerBase).GetMethod("OnChatMessageReceived_Server",
                BindingFlags.Static | BindingFlags.NonPublic);
            var patchMethod = typeof(ChatPatch).GetMethod(nameof(PrefixMessageProcessing),
                BindingFlags.Static | BindingFlags.NonPublic);
            context.GetPattern(target).Prefixes.Add(patchMethod);
        }

        private static bool PrefixMessageProcessing(ref ChatMsg msg)
        {
            Core.Log.Info($"Actual sender {MyEventContext.Current.Sender.Value}");
            return true;
        }
    }
}
