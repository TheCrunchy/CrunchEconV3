using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconContractModels.PlugAndPlay.Helpers;
using CrunchEconV3.PlugAndPlay.Helpers;

namespace CrunchEconV3.PlugAndPlay.Models
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
            var price = PriceHelper.GetPriceModel($"{v.TypeId}/{v.SubTypeId}");
            var pricing = price.GetSellMinAndMaxPrice(true);
            return new ItemToDeliver()
            {
                AmountToDeliver = amount,
                Pay = Core.random.Next((int)pricing.Item1, (int)pricing.Item2) * amount,
                SubTypeId = v.SubTypeId,
                TypeId = v.TypeId,
            };
        }
    }
}
