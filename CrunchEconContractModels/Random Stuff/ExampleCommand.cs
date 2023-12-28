using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace CrunchEconContractModels.Random_Stuff
{
    public class ExampleCommand : CommandModule
    {
        [Command("bobdavefred", "example command usage !example")]
        [Permission(MyPromoteLevel.Admin)]
        public void Example()
        {
            Context.Respond("This is a command compiled from scripts folder");
        }
    }
}
