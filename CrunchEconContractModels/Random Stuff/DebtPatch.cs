using System;
using System.Reflection;
using CrunchEconV3.Utils;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Torch.Managers.PatchManager;

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
                return true;

            }

            return true;
        }

        //internal static readonly MethodInfo flee =
        //    typeof(MyBankingSystem).GetMethod("ChangeBalance",
        //        BindingFlags.Static | BindingFlags.Public) ??
        //    throw new Exception("Failed to find patch method");

        //internal static readonly MethodInfo patchFlee =
        //    typeof(DebtPatch).GetMethod(nameof(DoBalanceChange),
        //        BindingFlags.Static | BindingFlags.Public) ??
        //    throw new Exception("Failed to find patch method");

        //public static void Patch(PatchContext ctx)
        //{

        //    ctx.GetPattern(flee).Prefixes.Add(patchFlee);
        //}

        //public static bool DoBalanceChange(long identifierId, long amount)
        //{
        //    var balance = EconUtils.getBalance(identifierId);
        //    if (balance < 0)
        //    {
        //        var newBalance = balance + amount;
        //        MyBankingSystem.Static.RemoveAccount(identifierId);
        //        MyBankingSystem.Static.CreateAccount(identifierId, newBalance);
        //        return false;

        //    }

        //    return true;
        //}

    }
}
