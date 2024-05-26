using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
using CrunchEconV3.Utils;
using Sandbox.Definitions;
using Sandbox.Game.SessionComponents;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.ObjectBuilders;

namespace CrunchEconContractModels.PlugAndPlay.Helpers
{
    public static class PriceHelper
    {
        private static Dictionary<string, PriceModel> _prices = new Dictionary<string, PriceModel>();
        public static void Patch(PatchContext ctx)
        {
            var reader = new FileUtils();
            var path = $"{Core.path}/Prices.json";

            if (File.Exists(path))
            {
                _prices = reader.ReadFromJsonFile<Dictionary<string, PriceModel>>(path);
            }

            var defs = MyDefinitionManager.Static.GetAllDefinitions();
            foreach (MyDefinitionBase def in defs)
            {
                if ((def as MyComponentDefinition) != null)
                {
                    if (!_prices.TryGetValue(def.Id.ToString(), out var model))
                    {
                        model = new PriceModel();
                        model.MinPrice = CalculatePrice(def);
                        model.Id = def.Id.ToString();
                        _prices[def.Id.ToString()] = model;
                    }
                }

                if ((def as MyPhysicalItemDefinition) == null) continue;
                {
                    if (_prices.TryGetValue(def.Id.ToString(), out var model)) continue;
                    model = new PriceModel();
                    model.MinPrice = CalculatePrice(def);
                    model.Id = def.Id.ToString();
                    _prices[def.Id.ToString()] = model;
                }
            }
            reader.WriteToJsonFile(path, _prices);
        }

        // Access Methods
        public static PriceModel GetPriceModel(string id)
        {
            if (_prices.TryGetValue(id, out var model))
            {
                return model;
            }
            return null;
        }
        public static bool TryGetPriceModel(string id, out PriceModel model)
        {
            return _prices.TryGetValue(id, out model);
        }
        public static IReadOnlyDictionary<string, PriceModel> GetAllPrices()
        {
            return _prices;
        }

        private static long CalculatePrice(MyDefinitionBase component)
        {
            MyBlueprintDefinitionBase bpDef = MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(component.Id);
            int p = 0;
            long price = 0;
            //calculate by the minimal price per unit for modded components, vanilla is aids
            var min = 0;

            switch (component)
            {
                case MyComponentDefinition comp:
                    min = comp.MinimalPricePerUnit;
                    break;
                case MyPhysicalItemDefinition item:
                    min = item.MinimalPricePerUnit;
                    break;
            }

            var minPriceIfNotDefined = 1000000000;
            if (min > 1)
            {
                Int64 amn = Math.Abs(min);
                price += amn;
                return price;
            }
            //calculate by what makes up the component / ingot 

            if (bpDef == null)
            {
                return minPriceIfNotDefined;
            }

            if (!bpDef.Prerequisites.Any())
            {
                return minPriceIfNotDefined;
            }
            for (p = 0; p < bpDef.Prerequisites.Length; p++)
            {

                if (bpDef.Prerequisites[p].Id != null)
                {

                    MyDefinitionBase oreDef = MyDefinitionManager.Static.GetDefinition(bpDef.Prerequisites[p].Id);
                    if (oreDef != null)
                    {

                        MyPhysicalItemDefinition ore = oreDef as MyPhysicalItemDefinition;

                        if (ore != null)
                        {
                            long amn = Math.Abs(ore.MinimalPricePerUnit);

                            float count = (float)bpDef.Prerequisites[p].Amount;

                            amn = (long)Math.Round(amn * count * 3);

                            price += amn;
                        }


                    }
                }
            }

            return price == 0 ? minPriceIfNotDefined : Convert.ToInt64(price * 10);
        }
    }

    public class PriceModel
    {
        public string Id { get; set; }
        public long MinPrice { get; set; }
        //Min price has a + or - 10% 
        public float RangeModifier = 0.1f;

        //sell price is 30% lower than buy price
        public float SellPriceModifier = 0.7f;

        public Tuple<long, long> GetSellMinAndMaxPrice()
        {
            Random random = new Random();

            // Calculate the random range within the RangeModifier
            double modifier = random.NextDouble() * RangeModifier;

            // Randomly decide whether to add or subtract the modifier
            int sign = random.Next(0, 2) == 0 ? -1 : 1;
            int priceChange = (int)(MinPrice * modifier) * sign;

            // Calculate the minimum and maximum prices
            long minPrice = (long)(MinPrice + priceChange * SellPriceModifier);
            long maxPrice = (long)(MinPrice - priceChange * SellPriceModifier);

            // Ensure minPrice is the smaller value and maxPrice is the larger value
            if (minPrice > maxPrice)
            {
                long temp = minPrice;
                minPrice = maxPrice;
                maxPrice = temp;
            }

            return Tuple.Create(minPrice, maxPrice);
        }

        public Tuple<long, long> GetBuyMinAndMaxPrice()
        {
            Random random = new Random();

            // Calculate the random range within the RangeModifier
            double modifier = random.NextDouble() * RangeModifier;

            // Randomly decide whether to add or subtract the modifier
            int sign = random.Next(0, 2) == 0 ? -1 : 1;
            int priceChange = (int)(MinPrice * modifier) * sign;

            // Calculate the minimum and maximum prices
            long minPrice = MinPrice + priceChange;
            long maxPrice = MinPrice - priceChange;

            // Ensure minPrice is the smaller value and maxPrice is the larger value
            if (minPrice > maxPrice)
            {
                long temp = minPrice;
                minPrice = maxPrice;
                maxPrice = temp;
            }

            return Tuple.Create(minPrice, maxPrice);
        }
    }

}
