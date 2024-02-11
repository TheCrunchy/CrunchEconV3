using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.GameSystems.BankingAndCurrency;
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
            MyBankingSystem.Static.RemoveAccount(Context.Player.IdentityId);
            MyBankingSystem.Static.CreateAccount(Context.Player.IdentityId, -100000);
        }
    }
}
