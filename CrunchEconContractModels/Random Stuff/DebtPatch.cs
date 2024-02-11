using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.StationLogics;
using CrunchEconV3.Utils;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using SpaceEngineers.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace CrunchEconContractModels.Random_Stuff
{
    public static class DebtPatch
    {
        internal static readonly MethodInfo flee =
            typeof(MyBankingSystem).GetMethod("ChangeBalance",
                BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo patchFlee =
            typeof(DebtPatch).GetMethod(nameof(DoBalanceChange),
                BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static void Patch(PatchContext ctx)
        {

            ctx.GetPattern(flee).Prefixes.Add(patchFlee);
        }

        public static bool DoBalanceChange(long identifierId, long amount)
        {
            var balance = EconUtils.getBalance(identifierId);
            if (balance < 0)
            {
                var newBalance = balance + amount;
                MyBankingSystem.Static.RemoveAccount(identifierId);
                MyBankingSystem.Static.CreateAccount(identifierId, newBalance);
                return false;

            }

            return true;
        }

    }
}
