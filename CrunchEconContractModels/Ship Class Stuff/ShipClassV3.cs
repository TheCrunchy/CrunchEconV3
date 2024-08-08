using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconContractModels.Ship_Class_Stuff
{
    public class ShipClassV3
    {
        public class ShipClassModel
        {
            public string BeaconBlockPairName { get; set; } = "Beacon";
            public float GridDamageTakenModifier { get; set; } = 1;
            public float GridDamageBuffModifier { get; set; } = 1;
        }
    }
}
