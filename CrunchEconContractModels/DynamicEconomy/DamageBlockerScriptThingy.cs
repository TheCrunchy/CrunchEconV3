using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMySlimBlock = VRage.Game.ModAPI.IMySlimBlock;

namespace CrunchEconContractModels.DynamicEconomy
{
    public class ShipClassConfig
    {

        public string ClassName = DamageBlockerScriptThingy.DefaultClassName;
        public Dictionary<string, BlockModifier> Modifiers = new Dictionary<string, BlockModifier>();
        public float DefaultChargeDeducationModifier = 0.5f;

        public class BlockModifier
        {
            public bool TakesDamage { get; set; }
            public float DamageModifier { get; set; } = 1f;
            public bool DeductsCharge { get; set; }
            public float ChargeModifier { get; set; } = 0.5f;
        }
    }
    public class GridClass
    {
        public MyCubeGrid MainGrid { get; set; }
        public MyBatteryBlock BatteryBlock { get; set; }

        public string MappedClass { get; set; } = DamageBlockerScriptThingy.DefaultClassName;

    }
    public class EasyAddModel
    {
        public string SubTypesSeperatedByComma = "LargeHeavyBlockArmorBlock,LargeHeavyBlockArmorBlock2,LargeHeavyBlockArmorBlock3";
        public string TypeId = "CubeBlock";

        public ShipClassConfig.BlockModifier Modifier = new ShipClassConfig.BlockModifier()
        {
            DeductsCharge = true,
            ChargeModifier = 0.5f,
            DamageModifier = 1,
            TakesDamage = true
        };

        public List<String> GetSubtypes()
        {
            return SubTypesSeperatedByComma.Split(',').Select(x => x.Trim()).ToList();
        }
    }

    [PatchShim]
    public static class DamageBlockerScriptThingy
    {

        public const string DefaultClassName = "Default";
        public static Dictionary<string, ShipClassConfig> Configs = new Dictionary<string, ShipClassConfig>();

        public static int ticks = 0;
        public static Dictionary<long, GridClass> GridsWithShields = new Dictionary<long, GridClass>();

        public static void Patch(PatchContext ctx)
        {
            var modifiers = new Dictionary<string, ShipClassConfig.BlockModifier>();
            var easyAdd = new EasyAddModel();

            foreach (var item in easyAdd.GetSubtypes())
            {
                modifiers.Add($"{easyAdd.TypeId}-{item}", easyAdd.Modifier);
            }

            modifiers.Add($"CubeBlock", new ShipClassConfig.BlockModifier()
            {
                DeductsCharge = true,
                ChargeModifier = 0.5f,
                DamageModifier = 1,
                TakesDamage = false
            });

            Configs.Add(DefaultClassName, new ShipClassConfig()
            {
                ClassName = DefaultClassName,
                DefaultChargeDeducationModifier = 100,
                Modifiers = modifiers
            });

            try
            {
                ctx.GetPattern(DamageRequest).Prefixes.Add(patchSlimDamage);
            }
            catch (Exception e)
            {
                Core.Log.Error($"Failed to apply patch: {e.Message}");
            }

            Core.UpdateCycle += Update100;
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;

            // Iterate through all existing grids when the mod initializes
            var grids = new List<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(null, (entity) =>
            {
                if (entity is IMyCubeGrid grid)
                {
                    OnEntityAdd(grid);
                }
                return false;
            });
        }

        public static void Update100()
        {
            ticks++;
            if (ticks % 100 == 0)
            {
                foreach (var grid in GridsWithShields)
                {
                    if (grid.Value.BatteryBlock == null)
                    {
                        var battery = grid.Value.MainGrid.GetFatBlocks().OfType<MyBatteryBlock>().FirstOrDefault(x =>
                            x.BlockDefinition.Id.SubtypeName.Contains("LargeBlockBatteryBlock"));
                        if (battery == null)
                        {
                            continue;
                        }
                        
                        grid.Value.BatteryBlock = battery;
                    }

                    var charge = grid.Value.BatteryBlock.CurrentStoredPower / grid.Value.BatteryBlock.MaxStoredPower * 100;
                    var current = grid.Value.MainGrid.GridGeneralDamageModifier.Value;
                    if (charge <= 10)
                    {

                    }

                    if (charge <= 25)
                    {

                    }

                    if (charge <= 50)
                    {
                  
                    }

                    if (charge <= 75)
                    {

                    }

                    if (charge <= 100)
                    {

                    }
                }
            }
        }

        public static bool OnDamageRequest(MySlimBlock __instance, ref float damage,
            MyStringHash damageType,
            bool sync,
            MyHitInfo? hitInfo,
            long attackerId, long realHitEntityId = 0, bool shouldDetonateAmmo = true, MyStringHash? extraInfo = null)
        {
            Core.Log.Info($"{damageType}");
            Core.Log.Info("1");
            if (!GridsWithShields.TryGetValue(__instance.CubeGrid.EntityId, out var gridsClass)) return true;
            Core.Log.Info("2");
            if (gridsClass.BatteryBlock == null) return true;
            Core.Log.Info("3");
            if (!Configs.TryGetValue(gridsClass.MappedClass, out var itsClass)) return true;
            Core.Log.Info("4");
            if (__instance.BlockDefinition != null)
            {
                var charge = gridsClass.BatteryBlock.CurrentStoredPower / gridsClass.BatteryBlock.MaxStoredPower * 100;
                if (charge <= 5)
                {
                    return true;
                }
                Core.Log.Info("5");
                if (itsClass.Modifiers.TryGetValue($"{__instance.BlockDefinition.Id.TypeId.ToString().Replace("MyObjectBuilder_", "")}", out var blockModifier))
                {

                    if (blockModifier.DeductsCharge)
                    {
                        gridsClass.BatteryBlock.CurrentStoredPower -= (damage * blockModifier.ChargeModifier);
                    }

                    Core.Log.Info("6");
                    if (blockModifier.TakesDamage)
                    {
                        Core.Log.Info("Modifying damage");
                        damage *= (float)blockModifier.DamageModifier;
                    }
                    else
                    {
                        Core.Log.Info("Denying damage");
                        damage = 0.0f;
                        return false;
                    }



                    return true;
                }

                if (itsClass.Modifiers.TryGetValue(
                        $"{__instance.BlockDefinition.Id.TypeId.ToString().Replace("MyObjectBuilder_", "")}-{__instance.BlockDefinition.Id.SubtypeId}",
                        out var modifier))
                {
                    if (modifier.DeductsCharge)
                    {
                        gridsClass.BatteryBlock.CurrentStoredPower -= (damage * modifier.ChargeModifier);
                    }
                    Core.Log.Info("7");
                    if (modifier.TakesDamage)
                    {
                        Core.Log.Info("Modifying damage");
                        damage *= (float)modifier.DamageModifier;
                    }
                    else
                    {
                        Core.Log.Info("Denying damage");
                        damage = 0.0f;
                        return false;
                    }
                  

                    return true;
                }
            }
            Core.Log.Info("deduct battery power");
            gridsClass.BatteryBlock.CurrentStoredPower -= (damage * itsClass.DefaultChargeDeducationModifier);
            return true;
        }

        private static void OnEntityAdd(IMyEntity entity)
        {
            if (entity is IMyCubeGrid grid)
            {
                grid.OnBlockAdded += OnBlockAdded;

                var gridClass = new GridClass()
                {
                    MainGrid = grid as MyCubeGrid
                };
                GridsWithShields.Add(grid.EntityId, gridClass);
                gridClass.MainGrid.GridGeneralDamageModifier.ValidateAndSet(1f);
            }
        }

        private static void OnBlockAdded(IMySlimBlock block)
        {
            if (block.BlockDefinition != null && block.BlockDefinition.Id.SubtypeName.Contains("LargeBlockBatteryBlock"))
            {
                var grid = block.CubeGrid as MyCubeGrid;
                var gridClass = new GridClass()
                {
                    MainGrid = grid
                };
                GridsWithShields[grid.EntityId] = gridClass;
            }
        }

        // Method to send chat messages to the player
        private static void SendChatMessage(MyCubeGrid grid, string message)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, p => p.Controller?.ControlledEntity?.Entity.GetTopMostParent() == grid);

            foreach (var player in players)
            {
                ulong steamId = player.SteamUserId;
                CrunchEconV3.Core.SendMessage("Shields", message, VRageMath.Color.LightGreen, steamId);
            }
        }

        internal static readonly MethodInfo DamageRequest =
            typeof(MySlimBlock).GetMethod("DoDamage", BindingFlags.Instance | BindingFlags.Public, null,
                new Type[]
                {
                    typeof(float), typeof(MyStringHash), typeof(bool), typeof(MyHitInfo?), typeof(long), typeof(long),
                    typeof(bool), typeof(MyStringHash?)
                }, null) ??
            throw new Exception("Failed to find patch method slim block DoDamage");

        internal static readonly MethodInfo patchSlimDamage =
            typeof(DamageBlockerScriptThingy).GetMethod(nameof(OnDamageRequest), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");
    }
}
