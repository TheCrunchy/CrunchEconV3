using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconV3.Models.ContractStuff
{
    public class ItemToDeliver
    {
        public string TypeId { get; set; }
        public string SubTypeId { get; set; }
        public int AmountToDeliver { get; set; }
        public long Pay { get; set; }

        public static explicit operator ItemToDeliver(ItemHaul v)
        {
            var amount = Core.random.Next(v.AmountMin, v.AmountMax);
            return new ItemToDeliver()
            {
                AmountToDeliver = amount,
                Pay = Core.random.Next(v.PricePerItemMin, v.PricePerItemMax) * amount,
                SubTypeId = v.SubTypeId,
                TypeId = v.TypeId,
            };
        }
    }
}
