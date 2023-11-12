using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace CrunchEconV3.Commands
{
    [Category("newcontract")]
    public class CategoryCommands : CommandModule
    {
        [Command("reload", "example command usage !categorycommands example")]
        [Permission(MyPromoteLevel.Admin)]
        public void Example()
        {
            Core.StationStorage.LoadAll();
            StationHandler.BlocksContracts.Clear();
            Context.Respond("Reloaded and cleared existing contracts");
        }
    }
}
