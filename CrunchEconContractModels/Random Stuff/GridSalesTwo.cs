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
using Sandbox.Game.GUI;
using Sandbox.Game.World;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.Components;
using VRage.Network;
using VRageMath;
using CrunchEconV3.Patches;

namespace CrunchEconV3.Patches
{
    [PatchShim]
    public static class GridSalesTwo
    {
        public static void Patch(PatchContext ctx)
        {
            var confirm = new GridSales.Confirm();
			Core.Log.Error("This shit compiled");
        }
    }
}
