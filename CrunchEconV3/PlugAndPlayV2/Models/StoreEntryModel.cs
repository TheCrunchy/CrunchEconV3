using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconV3.PlugAndPlayV2.Models
{
    public class StoreEntryModel
    {
        public string Type { get; set; } = "";
        public string Subtype { get; set; } = "";
        public float ChanceToAppear = 0.5f;
        public int AmountMin { get; set; } = 100;
        public int AmountMax { get; set; } = 150;
    }

}
