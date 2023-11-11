using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models.Contracts;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.ObjectBuilders.Private;
using VRage.Utils;
using VRageMath;

namespace CrunchEconV3.Patches
{
    [PatchShim]
    public static class DrillPatch
    {

        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(update).Suffixes.Add(updatePatch);
        }
        public static Dictionary<ulong, DateTime> messageCooldown = new Dictionary<ulong, DateTime>();

        internal static readonly MethodInfo update =
            typeof(MyDrillBase).GetMethod("OnDrillResults", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo updatePatch =
            typeof(DrillPatch).GetMethod(nameof(PatchResults), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static Type drill = null;
        public static void PatchResults(MyDrillBase __instance,
            Dictionary<MyVoxelMaterialDefinition, int> materials,
            Vector3D hitPosition,
            bool collectOre,
            Action<bool> OnDrillingPerformed = null)
        {
            if (__instance.OutputInventory != null && __instance.OutputInventory.Owner != null)
            {
                if (__instance.OutputInventory.Owner.GetBaseEntity() is MyShipDrill shipDrill)
                {
                    if (drill == null)
                    {
                        drill = __instance.GetType();
                    }

                    var owner = shipDrill.OwnerId;
                    var data = Core.PlayerStorage.GetData(MySession.Static.Players.TryGetSteamId(owner));
                    if (data != null && data.PlayersContracts.All(x => x.Value is not CrunchMiningContract))
                    {
                        return;
                    }

                    Dictionary<string, int> MinedAmount = new Dictionary<string, int>();
                    foreach (var material in materials)
                    {
                        if (string.IsNullOrEmpty(material.Key.MinedOre))
                            return;

                        if (material.Value <= 0)
                        {
                            continue;
                        }
                        MyObjectBuilder_Ore newObject = MyObjectBuilderSerializerKeen.CreateNewObject<MyObjectBuilder_Ore>(material.Key.MinedOre);
                        newObject.MaterialTypeName = new MyStringHash?(material.Key.Id.SubtypeId);
                        float num = (float)(material.Value / (double)byte.MaxValue * 1.0) * __instance.VoxelHarvestRatio * material.Key.MinedOreRatio;
                        if (!MySession.Static.AmountMined.ContainsKey(material.Key.MinedOre))
                            MySession.Static.AmountMined[material.Key.MinedOre] = (MyFixedPoint)0;
                        MySession.Static.AmountMined[material.Key.MinedOre] += (MyFixedPoint)num;
                        MyPhysicalItemDefinition physicalItemDefinition = MyDefinitionManager.Static.GetPhysicalItemDefinition((MyObjectBuilder_Base)newObject);
                        MyFixedPoint amountItems1 = (MyFixedPoint)(num / physicalItemDefinition.Volume);
                        MyFixedPoint maxAmountPerDrop = (MyFixedPoint)(float)(0.150000005960464 / (double)physicalItemDefinition.Volume);



                        MyFixedPoint collectionRatio = (MyFixedPoint)drill.GetField("m_inventoryCollectionRatio", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);

                        MyFixedPoint b = amountItems1 * ((MyFixedPoint)1 - collectionRatio);
                        MyFixedPoint amountItems2 = MyFixedPoint.Min(maxAmountPerDrop * 10 - (MyFixedPoint)0.001, b);
                        MyFixedPoint totalAmount = amountItems1 * collectionRatio - amountItems2;

                        if (totalAmount > 0)
                        {
                            if (MinedAmount.ContainsKey(material.Key.MinedOre))
                            {
                                MinedAmount[material.Key.MinedOre] += totalAmount.ToIntSafe();
                            }
                            else
                            {
                                MinedAmount.Add(material.Key.MinedOre, totalAmount.ToIntSafe());
                            }
                        }
                    }

                    foreach (var mined in MinedAmount)
                    {
                        var contracts = data.PlayersContracts.Where(x => x.Value is CrunchMiningContract);
                        foreach (var contract in contracts)
                        {
                            var mining = contract.Value as CrunchMiningContract;
                            if (mining.OreSubTypeName != mined.Key) continue;
                            if (mining.MinedOreAmount >= mining.AmountToMine) continue;
                            mining.MinedOreAmount += mined.Value;
                            data.PlayersContracts[mining.ContractId] = mining;
                            if (mining.MinedOreAmount >= mining.AmountToMine)
                            {
                                mining.ReadyToDeliver = true;
                                mining.SendDeliveryGPS();
                                Core.SendMessage("Contracts",
                                    "Contract Ready to be completed, Deliver " +
                                    String.Format("{0:n0}", mining.AmountToMine) + " " + mining.OreSubTypeName +
                                    " to the delivery GPS.", Color.Gold,
                                    MySession.Static.Players.TryGetSteamId(owner));
                                messageCooldown.Remove(MySession.Static.Players.TryGetSteamId(owner));
                                messageCooldown.Add(MySession.Static.Players.TryGetSteamId(owner),
                                    DateTime.Now.AddSeconds(0.5));
                                Core.PlayerStorage.Save(data);
                                return;
                            }
                            if (messageCooldown.TryGetValue(MySession.Static.Players.TryGetSteamId(owner),
                                    out DateTime time))
                            {
                                if (DateTime.Now < time) return;
                                Core.SendMessage("Contracts",
                                    "Progress: " + mined.Key + " " +
                                    String.Format("{0:n0}", mining.MinedOreAmount) + " / " +
                                    String.Format("{0:n0}", mining.AmountToMine), Color.Gold,
                                    MySession.Static.Players.TryGetSteamId(owner));
                                messageCooldown[MySession.Static.Players.TryGetSteamId(owner)] =
                                    DateTime.Now.AddSeconds(0.5);


                            }
                            else
                            {
                                Core.SendMessage("Boss Dave",
                                    "Progress: " + mined.Key + " " +
                                    String.Format("{0:n0}", mining.MinedOreAmount) + " / " +
                                    String.Format("{0:n0}", mining.AmountToMine), Color.Gold,
                                    MySession.Static.Players.TryGetSteamId(owner));
                                messageCooldown.Add(MySession.Static.Players.TryGetSteamId(owner),
                                    DateTime.Now.AddSeconds(0.5));

                            }
                            Core.PlayerStorage.Save(data);
                            return;
                        }
                    }

                }
            }

        }
    }
}
