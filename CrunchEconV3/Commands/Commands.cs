using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace CrunchEconV3.Commands
{
    public class Commands : CommandModule
    {
        [Command("example", "example command usage !example")]
        [Permission(MyPromoteLevel.Admin)]
        public void Example()
        {
        }
    }
}
