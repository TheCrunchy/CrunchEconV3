using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconV3.PlugAndPlayV2.Models
{
    public class StoreLists
    {
        public List<StoreEntryModel> SellingToPlayers { get; set; } = new();
        public List<StoreEntryModel> BuyingFromPlayers { get; set; } = new();
    }
}
