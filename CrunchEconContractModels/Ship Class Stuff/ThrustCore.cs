using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace CrunchEconContractModels.Ship_Class_Stuff
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, "Thrust2x", "Thrust3x", "Thrust4x", "Thrust5x")]
    public class ThrustCore : MyGameLogicComponent
    {
        private IMyBatteryBlock _battery;
        private int _multiplier = 1;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            // the base methods are usually empty, except for OnAddedToContainer()'s, which has some sync stuff making it required to be called.
            base.Init(objectBuilder);
            MyLog.Default.WriteLine($"[Crunch]: Init");
            _battery = (IMyBatteryBlock)Entity;
            var multiplier = _battery.BlockDefinition.SubtypeId;
            switch (multiplier)
            {
                case "Thrust2x":
                    _multiplier = 2;
                    break;
                case "Thrust3x":
                    _multiplier = 3;
                    break;
                case "Thrust4x":
                    _multiplier = 4;
                    break;
                case "Thrust5x":
                    _multiplier = 5;
                    break;
            }
        //    MyLog.Default.WriteLine($"[Crunch]: {multiplier}");
            _battery.OnMarkForClose += Closed;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public void Closed(IMyEntity entity)
        {
        //    MyLog.Default.WriteLine($"[Crunch]: closed");
            UpdateThrust(true);
            _battery.OnMarkForClose -= Closed;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (_battery.CubeGrid?.Physics == null)
                return; // ignore ghost/projected grids
            NeedsUpdate |= MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            UpdateThrust();
        }

        public void UpdateThrust(bool Closed = false)
        {
            if (Closed || !_battery.SlimBlock.IsFullIntegrity)
            {
                var thrusters = _battery.CubeGrid.GetFatBlocks<IMyThrust>();
              //  MyLog.Default.WriteLine($"[Crunch]: resetting {thrusters.Count()}");
                foreach (var item in thrusters)
                {
                    item.ThrustMultiplier = 1;
                }
            }
            else if (_battery.SlimBlock.IsFullIntegrity)
            {
                var thrusters = _battery.CubeGrid.GetFatBlocks<IMyThrust>();
             //   MyLog.Default.WriteLine($"[Crunch]: modifying {thrusters.Count()}");
                foreach (var item in thrusters)
                {
                    item.ThrustMultiplier = _multiplier;
                }
            }
        }

    }
}