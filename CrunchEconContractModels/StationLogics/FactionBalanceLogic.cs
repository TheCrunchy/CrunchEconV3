using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using NLog.Fluent;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace CrunchEconContractModels.StationLogics
{
    public class FactionBalanceLogic : IStationLogic
    {
        public void Setup()
        {
          
        }
        public Task<bool> DoLogic(MyCubeGrid grid)
        {
            if (DateTime.Now >= NextRefresh)
            {
                NextRefresh = DateTime.Now.AddSeconds(SecondsBetweenRefresh);
            }
            else
            {
                return Task.FromResult(true);
            }

            var owner = FacUtils.GetOwner(grid);
            var balance = EconUtils.getBalance(owner);
            if (balance >= BalanceToMaintain)
            {
                return Task.FromResult(true);
            }
            var MoneyToAdd = this.BalanceToMaintain - balance;
            EconUtils.addMoney(owner, MoneyToAdd);
            return Task.FromResult(true);
        }

        public int Priority { get; set; }
        public long BalanceToMaintain { get; set; } = 1000000000;
        public DateTime NextRefresh { get; set; }
        public int SecondsBetweenRefresh = 6000;
    }
}
