using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.APIs;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.ObjectBuilders.Private;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.Patches
{
    [PatchShim]
    public static class Testpatch
    {
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(contract).Prefixes.Add(contractPatch);
        }
        internal static readonly MethodInfo contract =
            typeof(MySessionComponentBase).GetMethod("LoadData",
                BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo contractPatch =
            typeof(Testpatch).GetMethod(nameof(LoadData), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static bool Loaded = false;

        public static void LoadData()
        {
            if (!Loaded)
            {
                Core.Log.Info("Registering MES API");
               Core.SpawnerAPI = new MESApi();
                Loaded = true;
            }
        }
    }
}