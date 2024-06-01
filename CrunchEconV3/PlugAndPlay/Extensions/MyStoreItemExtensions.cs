using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities.Blocks;

namespace CrunchEconV3.PlugAndPlay.Extensions
{
    public static class MyStoreItemExtensions
    {
        public static MyStoreItem Clone(this MyStoreItem item)
        {
            return new MyStoreItem
            {
                Id = item.Id,
                Item = item.Item,// Assuming MyItem also has a Clone method
                ItemType = item.ItemType,
                Amount = item.Amount,
                PricePerUnit = item.PricePerUnit,
                StoreItemType = item.StoreItemType,
                IsActive = item.IsActive,
                PrefabName = item.PrefabName,
                PrefabTotalPcu = item.PrefabTotalPcu,
                PricePerUnitDiscount = item.PricePerUnitDiscount,
                IsCustomStoreItem = item.IsCustomStoreItem,
            };
        }
    }
}
