using System;
using System.Collections.Generic;
using CrunchEconV3.Models;
using Sandbox.Game.Entities.Blocks;
using Sandbox.ModAPI;
using VRage.Game;

namespace CrunchEconV3.Utils
{
    public static class TankHelper
    {
        public static float RemoveGasFromTanksInGroup(TankGroup group, float gasToRemove)
        {
            foreach (var gas2 in group.TanksInGroup)
            {
                if (!(gasToRemove > 0)) continue;
                var tank = gas2 as IMyGasTank;

                var num = (float)(tank.FilledRatio) * tank.Capacity;

                if (gasToRemove >= num)
                {
                    tank.ChangeFilledRatio(0, true);
                    gasToRemove -= num;
                }
                else
                {
                    var newAmount = num - gasToRemove;
                    tank.ChangeFilledRatio(newAmount / tank.Capacity, true);
                    gasToRemove = 0;
                }
            }

            return gasToRemove;
        }

        public static float AddGasToTanksInGroup(TankGroup group, float amountToUse)
        {
            float GasRemoved = 0f;
            foreach (var tank in group.TanksInGroup)
            {
                if (!(amountToUse > 0)) continue;
                var tank2 = tank as IMyGasTank;

                var num = (float)(1.0 - tank2.FilledRatio) * tank2.Capacity;

                if (amountToUse >= num)
                {
                    tank2.ChangeFilledRatio(tank2.FilledRatio + (num / tank2.Capacity), true);
                    GasRemoved += num;
                    amountToUse -= num;
                }
                else
                {
                    tank2.ChangeFilledRatio(tank2.FilledRatio + (amountToUse / tank2.Capacity), true);
                    GasRemoved += amountToUse;  
                    amountToUse = 0;
                }
            }

            //      price += (long)(amountToUse / 1000) * storeItem.PricePerUnit;
            return GasRemoved;
        }

        public static TankGroup MakeTankGroup(List<IMyGasTank> tanks, long ownerId, long ignoredId, string gasType)
        {
            var group = new TankGroup();
            try
            {

                var gas = new VRage.Game.ObjectBuilders.Definitions.MyObjectBuilder_GasProperties
                    { SubtypeName = gasType };
                var gasId = MyDefinitionId.FromContent(gas);
                if (gasId == null)
                {
                    return group;
                }

                foreach (var tank in tanks)
                {
                    if (tank.OwnerId == ignoredId)
                    {
                        continue;
                    }

                    var relation = tank.GetUserRelationToOwner(ownerId);
                    if (!tank.Enabled) continue;

                    if (tank.OwnerId != ownerId && relation != MyRelationsBetweenPlayerAndBlock.FactionShare &&
                        relation != MyRelationsBetweenPlayerAndBlock.Neutral) continue;
                    if (tank.Stockpile)
                    {
                        continue;
                    }

                    var tankk = tank as IMyGasTank;
                    foreach (var resource in tankk.ResourceSink.AcceptedResources)
                    {
                        if (resource == gasId)
                        {
                            group.TanksInGroup.Add(tank);
                            if (tankk.FilledRatio > 0)
                            {
                                group.GasInTanks += (float)(tankk.FilledRatio) * tankk.Capacity;
                            }

                            group.Capacity += (float)(1.0 - tankk.FilledRatio) * tankk.Capacity;
                        }
                    }

                }
            }
            catch (Exception)
            {
                return group;
            }

            return group;
        }
    }
}