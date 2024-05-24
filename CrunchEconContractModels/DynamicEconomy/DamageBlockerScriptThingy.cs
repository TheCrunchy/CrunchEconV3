using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Utils;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMySlimBlock = VRage.Game.ModAPI.IMySlimBlock;

namespace CrunchEconContractModels.DynamicEconomy
{

    public class ShipClassConfig
    {

        public string ClassName = DamageBlockerScriptThingy.DefaultClassName;
        public Dictionary<string, BlockModifier> Modifiers = new Dictionary<string, BlockModifier>();
        public float DefaultChargeDeducation = 100;

        public class BlockModifier
        {
            public bool TakesDamage { get; set; }
            public float DamageModifier { get; set; } = 1.05f;
            public bool DeductsCharge { get; set; }
            public float ChargeDeducated { get; set; } = 100f;
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
            ChargeDeducated = 500,
            DamageModifier = 1,
            TakesDamage = true
        };

        public List<String> GetSubtypes()
        {
            return SubTypesSeperatedByComma.Split(',').Select(x => x.Trim()).ToList();
        }
    }

    [PatchShim]
    public class DamageBlockerScriptThingy
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

            Configs.Add(DefaultClassName, new ShipClassConfig()
            {
                ClassName = DefaultClassName,
                DefaultChargeDeducation = 100,
                Modifiers = modifiers
            });

            try
            {
                ctx.GetPattern(DamageRequest).Suffixes.Add(patchSlimDamage);
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

        public static void OnDamageRequest(MySlimBlock __instance, ref float damage,
            MyStringHash damageType,
            bool sync,
            MyHitInfo? hitInfo,
            long attackerId, long realHitEntityId = 0, bool shouldDetonateAmmo = true, MyStringHash? extraInfo = null)
        {
            if (__instance.FatBlock == null) return;
            if (!GridsWithShields.TryGetValue(__instance.CubeGrid.EntityId, out var gridsClass)) return;
            if (gridsClass.BatteryBlock == null) return;
            if (!Configs.TryGetValue(gridsClass.MappedClass, out var itsClass)) return;
            if (__instance.BlockDefinition != null)
            {
                if (itsClass.Modifiers.TryGetValue($"{__instance.BlockDefinition.Id.TypeId}", out var blockModifier))
                {
                    if (blockModifier.TakesDamage)
                    {
                        damage *= (float)blockModifier.DamageModifier;
                    }

                    if (blockModifier.DeductsCharge)
                    {
                        gridsClass.BatteryBlock.CurrentStoredPower -= blockModifier.ChargeDeducated;
                    }

                    return;
                }

                if (itsClass.Modifiers.TryGetValue(
                        $"{__instance.BlockDefinition.Id.TypeId}-{__instance.BlockDefinition.Id.SubtypeId}",
                        out var modifier))
                {
                    if (modifier.TakesDamage)
                    {
                        damage *= (float)modifier.DamageModifier;
                    }

                    if (modifier.DeductsCharge)
                    {
                        gridsClass.BatteryBlock.CurrentStoredPower -= modifier.ChargeDeducated;
                    }

                    return;
                }
            }

            gridsClass.BatteryBlock.CurrentStoredPower -= itsClass.DefaultChargeDeducation;
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
