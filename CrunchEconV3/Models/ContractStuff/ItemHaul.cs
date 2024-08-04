using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconV3.Models.ContractStuff
{
    public class ItemHaul
    {
        public string TypeId { get; set; }
        public string SubTypeId { get; set; }
        public int AmountMin { get; set; }
        public int AmountMax { get; set; }
        public int PricePerItemMin { get; set; }
        public int PricePerItemMax { get; set; }
    }


}
