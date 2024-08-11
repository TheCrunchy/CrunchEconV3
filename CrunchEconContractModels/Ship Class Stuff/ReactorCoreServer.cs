using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ObjectBuilders.Definitions.Reputation;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace CrunchEconContractModels.Ship_Class_Stuff
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, "Reactor2x", "Reactor3x", "Reactor4x")]
    public class ReactorCoreServer : MyGameLogicComponent
    {
        private IMyBatteryBlock _battery;
        private float _multiplier = 1;
        private float _damagemultiplier = 1;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            // the base methods are usually empty, except for OnAddedToContainer()'s, which has some sync stuff making it required to be called.
            base.Init(objectBuilder);
            MyLog.Default.WriteLine($"[Crunch]: Init");
            _battery = (IMyBatteryBlock)Entity;
            var multiplier = _battery.BlockDefinition.SubtypeId;
            switch (multiplier)
            {
                case "Reactor2x":
                    _multiplier = 1.1f;
                    _damagemultiplier = 0.9f;
                    break;
                case "Reactor3x":
                    _multiplier = 1.2f;
                    _damagemultiplier = 0.8f;
                    break;
                case "Reactor4x":
                    _multiplier = 1.3f;
                    _damagemultiplier = 0.7f;
                    break;
                case "Reactor5x":
                    _multiplier = 1.4f;
                    break;
            }
            MyLog.Default.WriteLine($"[Crunch]: {multiplier}");
            _battery.OnMarkForClose += Closed;
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public void Closed(IMyEntity entity)
        {
            MyLog.Default.WriteLine($"[Crunch]: closed");
            UpdateThrust(true);
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
            var grid = _battery.CubeGrid as MyCubeGrid;
            if (Closed || !_battery.SlimBlock.IsFullIntegrity && grid.GridGeneralDamageModifier.Value < 1)
            {
                Core.Log.Info($"Resetting damage modifier");
               grid.GridGeneralDamageModifier.ValidateAndSet(1);
            }
            else if (_battery.SlimBlock.IsFullIntegrity && grid.GridGeneralDamageModifier.Value >= 1)
            {
                Core.Log.Info($"Setting damage modifier");
                grid.GridGeneralDamageModifier.ValidateAndSet(_damagemultiplier);
            }

        }

    }
}