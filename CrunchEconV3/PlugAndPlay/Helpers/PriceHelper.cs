using System;
using System.Collections.Generic;
using System.IO;
using CrunchEconV3.Utils;
using Sandbox.Definitions;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Utils;

namespace CrunchEconV3.PlugAndPlay.Helpers
{
    public static class PriceHelper
    {
        private static Dictionary<string, PriceModel> _prices = new Dictionary<string, PriceModel>();
        private static FileSystemWatcher _fileSystemWatcher;
        private static FileUtils Reader = new FileUtils();
        public static void Patch(PatchContext ctx)
        {
            
            var path = $"{Core.path}/Prices.json";

            if (File.Exists(path))
            {
                _prices = Reader.ReadFromJsonFile<Dictionary<string, PriceModel>>(path);
            }
            var oxy = $"MyObjectBuilder_GasProperties/Oxygen";
            if (!_prices.TryGetValue(oxy, out var oxymodel))
            {
                oxymodel = new PriceModel();
                oxymodel.MinPrice = 1;
                oxymodel.Id = oxy;
                _prices[oxymodel.Id] = oxymodel;
            }
            var hydro = $"MyObjectBuilder_GasProperties/Hydrogen";
            if (!_prices.TryGetValue(hydro, out var hyromodel))
            {
                hyromodel = new PriceModel();
                hyromodel.MinPrice = 2;
                hyromodel.Id = hydro;
                _prices[hyromodel.Id] = hyromodel;
            }
            var defs = MyDefinitionManager.Static.GetAllDefinitions();
            foreach (MyDefinitionBase def in defs)
            {
                if (def as MyFactionTypeDefinition != null)
                {
                    Core.Log.Info("Faction definition");
                    var fac = def as MyFactionTypeDefinition;
                    if (fac.CanSellOxygen)
                    {
                      
                    }

                    if (fac.CanSellHydrogen)
                    {
                  
                    }
                }

                if ((def as MyComponentDefinition) != null)
                {
                    if (!_prices.TryGetValue(def.Id.ToString(), out var model))
                    {
                        model = new PriceModel();
                        model.MinPrice = CalculatePrice(def);
                        model.Id = def.Id.ToString();
                        _prices[model.Id] = model;
                    }
                }

                if ((def as MyPhysicalItemDefinition) == null) continue;
                {
                    if (_prices.TryGetValue(def.Id.ToString(), out var model)) continue;
                    model = new PriceModel();
                    model.MinPrice = CalculatePrice(def);
                    model.Id = def.Id.ToString();
                    _prices[model.Id] = model;
                }
            }
            Reader.WriteToJsonFile(path, _prices);
            _fileSystemWatcher = new FileSystemWatcher
            {
                Path = Path.GetDirectoryName(path),
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            _fileSystemWatcher.Changed += OnChanged;
        }
        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                Console.WriteLine($"File changed: {e.FullPath}");
                // Add your file handling logic here, e.g., reloading the file
                if (File.Exists(e.FullPath))
                {
                    _prices = Reader.ReadFromJsonFile<Dictionary<string, PriceModel>>(e.FullPath);
                }
            }
        }

        // Access Methods
        public static PriceModel GetPriceModel(string id)
        {
            if (_prices.TryGetValue(id, out var model))
            {
                return model;
            }
            return new PriceModel()
            {
                NotFound = true,
                MinPrice = 1000000000
            };
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
            var minPriceIfNotDefined = 1000000000;
            long price = 0;

            //calculate by the minimal price per unit for modded components, vanilla is aids
            int minimalPrice = 0;
            CalculateItemMinimalPrice(component.Id, 1f, ref minimalPrice);
            price += minimalPrice;
            return price == 0 ? minPriceIfNotDefined : Convert.ToInt64(price);
        }

        private static void CalculateItemMinimalPrice(
      MyDefinitionId itemId,
      float baseCostProductionSpeedMultiplier,
      ref int minimalPrice)
        {
          
            MyPhysicalItemDefinition definition1 = (MyPhysicalItemDefinition)null;
            if (MyDefinitionManager.Static.TryGetDefinition<MyPhysicalItemDefinition>(itemId, out definition1) && definition1.MinimalPricePerUnit != -1)
            {
                minimalPrice += definition1.MinimalPricePerUnit;
            }
            else
            {
 
                MyBlueprintDefinitionBase definition2 = (MyBlueprintDefinitionBase)null;
                if (!MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(itemId, out definition2))
                    return;
  
                
                float num1 = definition1.IsIngot ? 1f : MySession.Static.AssemblerEfficiencyMultiplier;
                int num2 = 0;


                foreach (MyBlueprintDefinitionBase.Item prerequisite in definition2.Prerequisites)
                {
                    if (itemId == prerequisite.Id)
                    {
                        minimalPrice = 1000000000;
                        return;
                    }
                    int minimalPrice1 = 0;
                    CalculateItemMinimalPrice(prerequisite.Id, baseCostProductionSpeedMultiplier, ref minimalPrice1);
                    float num3 = (float)prerequisite.Amount / num1;
                    num2 += (int)((double)minimalPrice1 * (double)num3);
                }
     
                float num4 = definition1.IsIngot ? MySession.Static.RefinerySpeedMultiplier : MySession.Static.AssemblerSpeedMultiplier;
                for (int index = 0; index < definition2.Results.Length; ++index)
                {
          
                    MyBlueprintDefinitionBase.Item result = definition2.Results[index];
                    if (result.Id == itemId)
                    {
                
                        float amount = (float)result.Amount;
                        if ((double)amount == 0.0)
                        {
                            MyLog.Default.WriteToLogAndAssert("Amount is 0 for - " + (object)result.Id);
                        }
                        else
                        {
                            float num3 = (float)(1.0 + Math.Log((double)definition2.BaseProductionTimeInSeconds + 1.0) * (double)baseCostProductionSpeedMultiplier / (double)num4);
                            minimalPrice += (int)((double)num2 * (1.0 / (double)amount) * (double)num3);
                            break;
                        }
                    }
                }
            }
        }
    }

    public class PriceModel
    {
        public bool NotFound { get; set; } = false;
        public string Id { get; set; }
        public long MinPrice { get; set; }
        //Min price has a + or - 5% 
        public float RangeModifier = 0.05f;

        //sell price is 30% lower than buy price
        public float SellPriceModifier = 0.7f;

        public float ContractPriceModifier = 0.7f;

        public Tuple<long, long> GetSellMinAndMaxPrice(bool contract = false)
        {
            Random random = new Random();

            // Calculate the random range within the RangeModifier
            double modifier = random.NextDouble() * RangeModifier;

            // Randomly decide whether to add or subtract the modifier
            int sign = random.Next(0, 2) == 0 ? -1 : 1;
            int priceChange = (int)(MinPrice * modifier) * sign;

            var endModifier = contract ? SellPriceModifier : ContractPriceModifier;

            // Calculate the minimum and maximum prices
            long minPrice = (long)(MinPrice + priceChange * endModifier);
            long maxPrice = (long)(MinPrice - priceChange * endModifier);

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
