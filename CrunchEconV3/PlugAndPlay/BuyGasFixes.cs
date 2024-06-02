using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using Torch.API.Plugins;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Network;

namespace CrunchEconV3.PlugAndPlay
{
    [PatchShim]
    public static class BuyGasFixes
    {
        public static void Patch(PatchContext ctx)
        {
           ctx.GetPattern(update).Prefixes.Add(storePatchTwo);
        }

        internal static readonly MethodInfo update =
            typeof(MyStoreBlock).GetMethod("BuyGasInternal", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo storePatchTwo =
            typeof(BuyGasFixes).GetMethod(nameof(StorePatchMethod), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static bool StorePatchMethod(MyStoreItem storeItem,
            ref int amount,
            MyPlayer player,
            MyFaction faction,
            MyEntity entity,
            ref long totalPrice,
            MyDefinitionId gasId)
        {
            if (faction != null)
            {
                if (entity == player.Character && player.Character.OxygenComponent != null)
                {
                    amount = 1;
                    totalPrice = amount * storeItem.PricePerUnit;
                    //Core.Log.Info(1);
                    //float gasInput = (float)amount * 1000f;
                    //var amountPossible = 0f;
                    //MethodInfo methodInfo = player.Character.OxygenComponent.GetType().GetMethod("TryGetGasData", BindingFlags.Instance | BindingFlags.NonPublic);
                    //Core.Log.Info(2);
                    //// Prepare parameters for the method invocation
                    //object[] parameters = new object[] { gasId, null };
                    //Core.Log.Info(3);
                    //// Invoke the method dynamically
                    //bool success = (bool)methodInfo.Invoke(player.Character.OxygenComponent, parameters);
                    //Core.Log.Info(4);
                    //if (!success)
                    //{
                    //    Core.Log.Info("No work");
                    //    return true;
                    //}
                    //Core.Log.Info(5);
                    //// Retrieve the out parameter value
                    //float gasData = (float)parameters[1].GetType().GetField("MaxCapacity").GetValue(parameters[1]);
                    //Core.Log.Info(6);
                    //var playersGas = player.Character.OxygenComponent.GetGasFillLevel(gasId);
                    //if (playersGas >= 1f)
                    //{
                    //    amount = 1;
                    //    totalPrice = amount * storeItem.PricePerUnit;
                    //    return true;
                    //}
                    //Core.Log.Info($"{playersGas}");
                    //var capacity = gasData;
                    //Core.Log.Info($"{playersGas * capacity} and {capacity}");
                    //Core.Log.Info(7);

                    //if (gasInput > (capacity - playersGas * capacity) * 1000)
                    //{
                    //    Core.Log.Info(8);
                    //    amount = (int)(capacity - playersGas * capacity);
                    //    totalPrice = amount * storeItem.PricePerUnit;
                    //}
                }
            }
            return true;
        }
    }

    public class GasData
    {
        public int NextGasRefill = -1;
        public MyDefinitionId Id;
        public float FillLevel;
        public float MaxCapacity;
        public float Throughput;
        public float NextGasTransfer;
        public int LastOutputTime;
        public int LastInputTime;

        public override string ToString()
        {
            return string.Format("Subtype: {0}, FillLevel: {1}, CurrentCapacity: {2}, MaxCapacity: {3}", (object)this.Id.SubtypeName, (object)this.FillLevel, (object)(float)((double)this.FillLevel * (double)this.MaxCapacity), (object)this.MaxCapacity);
        }
    }
}
