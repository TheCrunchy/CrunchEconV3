using System.Collections.Generic;
using Sandbox.ModAPI;

namespace CrunchEconV3.Models
{
    public class TankGroup
    {
        public float Capacity;
        public float GasInTanks;
        public List<IMyGasTank> TanksInGroup = new List<IMyGasTank>();
    }
}