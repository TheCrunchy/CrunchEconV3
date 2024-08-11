using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace CrunchEconContractModels.Ship_Class_Stuff
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, "Reactor2x", "Reactor3x", "Reactor4x")]
    public class ReactorCore : MyGameLogicComponent
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
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(0, DamageHandler);
            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        private void DamageHandler(object target, ref MyDamageInformation info)
        {
            if (_battery.Closed)
            {
                return;
            }
            if (!(target is IMySlimBlock))
            {
                //handle characters differently
                return;
            }

            var block = target as IMySlimBlock;
            if (block.CubeGrid.EntityId == _battery.CubeGrid.EntityId)
            {
                // MyLog.Default.WriteLine($"[Crunch]: nerfing damage");
                info.Amount *= _damagemultiplier;
            }
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
            if (Closed || !_battery.SlimBlock.IsFullIntegrity)
            {
                var thrusters = _battery.CubeGrid.GetFatBlocks<IMyReactor>();
                //      MyLog.Default.WriteLine($"[Crunch]: resetting {thrusters.Count()}");
                foreach (var item in thrusters)
                {
                    item.PowerOutputMultiplier = 1;
                }
            }
            else if (_battery.SlimBlock.IsFullIntegrity)
            {
                var thrusters = _battery.CubeGrid.GetFatBlocks<IMyReactor>();
                //     MyLog.Default.WriteLine($"[Crunch]: modifying {thrusters.Count()}");
                foreach (var item in thrusters)
                {
                    item.PowerOutputMultiplier = _multiplier;
                }
            }

        }

    }
}