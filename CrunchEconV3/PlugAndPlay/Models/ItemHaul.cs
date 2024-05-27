using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconV3.PlugAndPlay.Models
{
    public class ItemHaul
    {
        public string TypeId { get; set; }
        public string SubTypeId { get; set; }
        public int AmountMin { get; set; }
        public int AmountMax { get; set; }
    }

}
