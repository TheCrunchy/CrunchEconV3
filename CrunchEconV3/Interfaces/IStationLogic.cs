using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;

namespace CrunchEconV3.Interfaces
{
    public interface IStationLogic
    {
        public Task<Boolean> DoLogic(MyCubeGrid grid);
        public int Priority { get; set; }
    }
}
