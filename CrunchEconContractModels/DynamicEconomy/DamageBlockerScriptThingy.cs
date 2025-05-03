//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Reflection;
//using System.Threading.Tasks;
//using CrunchEconV3;
//using Sandbox.Game.Entities;
//using Sandbox.Game.Entities.Cube;
//using Sandbox.ModAPI;
//using Torch.Managers.PatchManager;
//using VRage.Game.ModAPI;
//using VRage.Game.ModAPI.Ingame;
//using VRage.Utils;
//using VRage.Game;
//using VRage.Game.Entity;
//using VRageMath; // Add this for BoundingSphereD
//using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
//using IMySlimBlock = VRage.Game.ModAPI.IMySlimBlock;
//using MyJumpDrive = Sandbox.Game.Entities.MyJumpDrive;

//namespace CrunchEconContractModels.DynamicEconomy
//{
//    public class ShipClassConfig
//    {
//        public string ClassName = DamageBlockerScriptThingy.DefaultClassName;
//        public Dictionary<string, BlockModifier> Modifiers = new Dictionary<string, BlockModifier>();
//        public float DefaultChargeDeducationModifier = 0.5f;

//        public class BlockModifier
//        {
//            public bool TakesDamage { get; set; }
//            public float DamageModifier { get; set; } = 1f;
//            public bool DeductsCharge { get; set; }
//            public float ChargeModifier { get; set; } = 0.5f;
//        }
//    }
//    public class GridClass
//    {
//        public MyCubeGrid MainGrid { get; set; }
//        public MyJumpDrive JumpDrive { get; set; }

//        public string MappedClass { get; set; } = DamageBlockerScriptThingy.DefaultClassName;
//    }
//    public class EasyAddModel
//    {
//        public string SubTypesSeperatedByComma = "LargeHeavyBlockArmorBlock,LargeHeavyBlockArmorBlock2,LargeHeavyBlockArmorBlock3";
//        public string TypeId = "CubeBlock";

//        public ShipClassConfig.BlockModifier Modifier = new ShipClassConfig.BlockModifier()
//        {
//            DeductsCharge = true,
//            ChargeModifier = 0.5f,
//            DamageModifier = 1,
//            TakesDamage = false
//        };

//        public List<String> GetSubtypes()
//        {
//            return SubTypesSeperatedByComma.Split(',').Select(x => x.Trim()).ToList();
//        }
//    }

//    [PatchShim]
//    public static class DamageBlockerScriptThingy
//    {
//        public const string DefaultClassName = "Default";
//        public static Dictionary<string, ShipClassConfig> Configs = new Dictionary<string, ShipClassConfig>();
//        public static int ticks = 0;
//        public static Dictionary<long, GridClass> GridsWithShields = new Dictionary<long, GridClass>();

//        public static void Patch(PatchContext ctx)
//        {
//            var modifiers = new Dictionary<string, ShipClassConfig.BlockModifier>();
//            var easyAdd = new EasyAddModel();

//            foreach (var item in easyAdd.GetSubtypes())
//            {
//                string key = $"{easyAdd.TypeId}-{item}";
//                if (!Configs.ContainsKey(DefaultClassName))
//                {
//                    Configs[DefaultClassName] = new ShipClassConfig()
//                    {
//                        ClassName = DefaultClassName,
//                        Modifiers = new Dictionary<string, ShipClassConfig.BlockModifier>()
//                    };
//                }
//                Configs[DefaultClassName].Modifiers[key] = easyAdd.Modifier;
//            }

//            // Define a default modifier to apply only when no specific subtype is found
//            if (!Configs[DefaultClassName].Modifiers.ContainsKey($"CubeBlock-Default"))
//            {
//                Configs[DefaultClassName].Modifiers.Add($"CubeBlock-Default", new ShipClassConfig.BlockModifier()
//                {
//                    DeductsCharge = true,
//                    ChargeModifier = 0.5f,
//                    DamageModifier = 1,
//                    TakesDamage = false
//                });
//            }

//            try
//            {
//                ctx.GetPattern(DamageRequest).Prefixes.Add(patchSlimDamage);
//            }
//            catch (Exception e)
//            {
//                Core.Log.Error($"Failed to apply patch: {e.Message}");
//            }

//            Core.UpdateCycle += Update100;
//            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;

//            // Iterate through all existing grids when the mod initializes
//            var grids = new List<IMyEntity>();
//            MyAPIGateway.Entities.GetEntities(null, (entity) =>
//            {
//                if (entity is IMyCubeGrid grid)
//                {
//                    OnEntityAdd(grid);
//                }
//                return false;
//            });
//        }

//        public static void Update100()
//        {
//            ticks++;
//            if (ticks % 100 != 0) return;

//            foreach (var grid in GridsWithShields)
//            {
//                if (grid.Value.JumpDrive == null)
//                {
//                    var jumpDrive = grid.Value.MainGrid.GetFatBlocks<MyJumpDrive>()
//                        .FirstOrDefault(x => x.BlockDefinition.Id.SubtypeName.Contains("LargeJumpDrive") ||
//                                             x.BlockDefinition.Id.SubtypeName.Contains("LargeJumpDriveElite"));
//                    if (jumpDrive == null)
//                    {
//                        continue;
//                    }

//                    grid.Value.JumpDrive = jumpDrive;
//                }

//                var charge = grid.Value.JumpDrive.StoredPowerRatio / ((IMyJumpDrive)grid.Value.JumpDrive).MaxStoredPower * 100;
//                var current = grid.Value.MainGrid.GridGeneralDamageModifier.Value;
//                bool shouldDisableBlocks = false; // Default to false unless conditions are met

//                switch (charge)
//                {
//                    case > 75 and <= 100:
//                    case > 50 and <= 75:
//                    case > 25 and <= 50:
//                    case > 10 and <= 25:
//                        shouldDisableBlocks = true;
//                        break;
//                    case <= 10:
//                        shouldDisableBlocks = false;
//                        break;
//                }

//                // Execute the block disabling logic only if shouldDisableBlocks is true
//                if (!shouldDisableBlocks) continue;
//                // Create a sphere around the jumpdrive
//                var position = grid.Value.JumpDrive.PositionComp.GetPosition();
//                var radius = 1000.0; // Adjust the radius as needed
//                var sphere = new BoundingSphereD(position, radius);

//                // Get a list of entities inside the sphere
//                var entities = new HashSet<VRage.ModAPI.IMyEntity>();
//                MyAPIGateway.Entities.GetEntities(entities, entity => sphere.Contains(entity.GetPosition()) == ContainmentType.Contains);

//                // Define the block types to disable
//                var blockTypesToDisable = new Dictionary<string, string>
//                {
//                    { "LargeJumpDrive", "SomeSubtypeId1" },
//                    { "LargeJumpDriveElite", "SomeSubtypeId2" }
//                };

//                // Check for enemy entities and disable specific blocks
//                foreach (var entity in entities)
//                {
//                    if (entity is not IMyCubeGrid enemyGrid ||
//                        !MyAPIGateway.Session.Factions.AreFactionsEnemies(
//                            MyAPIGateway.Session.Factions.TryGetPlayerFaction(enemyGrid.BigOwners.FirstOrDefault())
//                                ?.FactionId ?? 0,
//                            MyAPIGateway.Session.Factions
//                                .TryGetPlayerFaction(grid.Value.MainGrid.BigOwners.FirstOrDefault())?.FactionId ?? 0))
//                        continue;

//                    foreach (var block in enemyGrid.GetFatBlocks<MyCubeBlock>())
//                    {
//                        if (!blockTypesToDisable.ContainsKey(block.BlockDefinition.Id.SubtypeName)) continue;
//                        if (block is MyFunctionalBlock functionalBlock && functionalBlock.Enabled)
//                        {
//                            functionalBlock.Enabled = false;
//                        }
//                    }
//                }
//            }
//        }

//        public static bool OnDamageRequest(MySlimBlock __instance, ref float damage,
//            MyStringHash damageType,
//            bool sync,
//            MyHitInfo? hitInfo,
//            long attackerId, long realHitEntityId = 0, bool shouldDetonateAmmo = true, MyStringHash? extraInfo = null)
//        {
//            Core.Log.Info($"{damageType}");
//            Core.Log.Info("1");
//            if (!GridsWithShields.TryGetValue(__instance.CubeGrid.EntityId, out var gridsClass)) return true;
//            Core.Log.Info("2");
//            if (gridsClass.JumpDrive == null) return true;
//            Core.Log.Info("3");
//            if (!Configs.TryGetValue(gridsClass.MappedClass, out var itsClass)) return true;
//            Core.Log.Info("4");
//            if (__instance.BlockDefinition != null)
//            {
//                var charge = gridsClass.JumpDrive.CurrentStoredPower / ((IMyJumpDrive)gridsClass.JumpDrive).MaxStoredPower * 100;
//                if (charge <= 5 || gridsClass.JumpDrive.Closed)
//                {
//                    return true;
//                }
//                Core.Log.Info("5");

//                string typeKey = $"{__instance.BlockDefinition.Id.TypeId.ToString().Replace("MyObjectBuilder_", "")}";
//                string subtypeKey = $"{__instance.BlockDefinition.Id.TypeId.ToString().Replace("MyObjectBuilder_", "")}-{__instance.BlockDefinition.Id.SubtypeId}";

//                Core.Log.Info($"Looking for block modifier with SubtypeId: {subtypeKey} or TypeId: {typeKey}");

//                // First, check for specific subtype modifier
//                if (itsClass.Modifiers.TryGetValue(subtypeKey, out var blockModifier))
//                {
//                    Core.Log.Info($"Block Modifier Found for SubtypeId: {subtypeKey} - TakesDamage={blockModifier.TakesDamage}, DamageModifier={blockModifier.DamageModifier}, DeductsCharge={blockModifier.DeductsCharge}, ChargeModifier={blockModifier.ChargeModifier}");

//                    if (blockModifier.DeductsCharge)
//                    {
//                        float chargeToDeduct = (damage / 1000) * blockModifier.ChargeModifier; // Dividing damage by 1000 to convert to kWh
//                        gridsClass.JumpDrive.CurrentStoredPower -= chargeToDeduct;
//                        Core.Log.Info($"Deducting Charge: Damage={damage}, ChargeModifier={blockModifier.ChargeModifier}, ChargeToDeduct={chargeToDeduct}, RemainingCharge={gridsClass.JumpDrive.CurrentStoredPower}");
//                    }

//                    Core.Log.Info("6");
//                    if (blockModifier.TakesDamage)
//                    {
//                        Core.Log.Info("Modifying damage");
//                        damage *= (float)blockModifier.DamageModifier;
//                    }
//                    else
//                    {
//                        Core.Log.Info("Denying damage");
//                        damage = 0.0f;
//                        return false;
//                    }

//                    return true;
//                }

//                // Apply default modifier only if no specific subtype modifier is found
//                if (itsClass.Modifiers.TryGetValue(typeKey, out var defaultModifier))
//                {
//                    Core.Log.Info($"Default Block Modifier Found for TypeId: {typeKey} - TakesDamage={defaultModifier.TakesDamage}, DamageModifier={defaultModifier.DamageModifier}, DeductsCharge={defaultModifier.DeductsCharge}, ChargeModifier={defaultModifier.ChargeModifier}");

//                    if (defaultModifier.DeductsCharge)
//                    {
//                        float chargeToDeduct = (damage / 1000) * defaultModifier.ChargeModifier; // Dividing damage by 1000 to convert to kWh
//                        gridsClass.JumpDrive.CurrentStoredPower -= chargeToDeduct;
//                        Core.Log.Info($"Deducting Default Charge: Damage={damage}, ChargeModifier={defaultModifier.ChargeModifier}, ChargeToDeduct={chargeToDeduct}, RemainingCharge={gridsClass.JumpDrive.CurrentStoredPower}");
//                    }

//                    Core.Log.Info("7");
//                    if (defaultModifier.TakesDamage)
//                    {
//                        Core.Log.Info("Modifying damage");
//                        damage *= (float)defaultModifier.DamageModifier;
//                    }
//                    else
//                    {
//                        Core.Log.Info("Denying damage");
//                        damage = 0.0f;
//                        return false;
//                    }

//                    return true;
//                }

//                // If no specific or default modifier is found, apply the global default modifier from ShipClassConfig.BlockModifier
//                var globalDefaultModifier = new ShipClassConfig.BlockModifier()
//                {
//                    TakesDamage = false,
//                    DamageModifier = 1,
//                    DeductsCharge = true,
//                    ChargeModifier = 0.5f
//                };

//                Core.Log.Info($"Global Default Block Modifier Applied - TakesDamage={globalDefaultModifier.TakesDamage}, DamageModifier={globalDefaultModifier.DamageModifier}, DeductsCharge={globalDefaultModifier.DeductsCharge}, ChargeModifier={globalDefaultModifier.ChargeModifier}");

//                if (globalDefaultModifier.DeductsCharge)
//                {
//                    float chargeToDeduct = (damage / 1000) * globalDefaultModifier.ChargeModifier; // Dividing damage by 1000 to convert to kWh
//                    gridsClass.JumpDrive.CurrentStoredPower -= chargeToDeduct;
//                    Core.Log.Info($"Deducting Global Default Charge: Damage={damage}, ChargeModifier={globalDefaultModifier.ChargeModifier}, ChargeToDeduct={chargeToDeduct}, RemainingCharge={gridsClass.JumpDrive.CurrentStoredPower}");
//                }

//                if (globalDefaultModifier.TakesDamage)
//                {
//                    Core.Log.Info("Modifying damage with global default modifier");
//                    damage *= (float)globalDefaultModifier.DamageModifier;
//                }
//                else
//                {
//                    Core.Log.Info("Denying damage with global default modifier");
//                    damage = 0.0f;
//                    return false;
//                }
//            }
//            return true;
//        }

//        private static void OnEntityAdd(IMyEntity entity)
//        {
//            if (entity is IMyCubeGrid grid)
//            {
//                grid.OnBlockAdded += OnBlockAdded;

//                var gridClass = new GridClass()
//                {
//                    MainGrid = grid as MyCubeGrid
//                };
//                GridsWithShields.Add(grid.EntityId, gridClass);
//                gridClass.MainGrid.GridGeneralDamageModifier.ValidateAndSet(1f);
//            }
//        }

//        private static void OnBlockAdded(IMySlimBlock block)
//        {
//            if (block.BlockDefinition != null &&
//                (block.BlockDefinition.Id.SubtypeName.Contains("LargeJumpDrive") ||
//                 block.BlockDefinition.Id.SubtypeName.Contains("LargeJumpDriveElite")))
//            {
//                var grid = block.CubeGrid as MyCubeGrid;
//                if (GridsWithShields.ContainsKey(grid.EntityId))
//                {
//                    GridsWithShields[grid.EntityId].JumpDrive = (MyJumpDrive)block.FatBlock;
//                }
//                else
//                {
//                    var gridClass = new GridClass()
//                    {
//                        MainGrid = grid,
//                        JumpDrive = (MyJumpDrive)block.FatBlock
//                    };
//                    GridsWithShields.Add(grid.EntityId, gridClass);
//                    grid.GridGeneralDamageModifier.ValidateAndSet(1f);
//                }
//            }
//        }

//        // Method to send chat messages to the player
//        private static void SendChatMessage(MyCubeGrid grid, string message)
//        {
//            var players = new List<IMyPlayer>();
//            MyAPIGateway.Players.GetPlayers(players, p => p.Controller?.ControlledEntity?.Entity.GetTopMostParent() == grid);

//            foreach (var player in players)
//            {
//                ulong steamId = player.SteamUserId;
//                CrunchEconV3.Core.SendMessage("Shields", message, VRageMath.Color.LightGreen, steamId);
//            }
//        }

//        internal static readonly MethodInfo DamageRequest =
//            typeof(MySlimBlock).GetMethod("DoDamage", BindingFlags.Instance | BindingFlags.Public, null,
//                new Type[]
//                {
//                    typeof(float), typeof(MyStringHash), typeof(bool), typeof(MyHitInfo?), typeof(long), typeof(long),
//                    typeof(bool), typeof(MyStringHash?)
//                }, null) ??
//            throw new Exception("Failed to find patch method slim block DoDamage");

//        internal static readonly MethodInfo patchSlimDamage =
//            typeof(DamageBlockerScriptThingy).GetMethod(nameof(OnDamageRequest), BindingFlags.Static | BindingFlags.Public) ??
//            throw new Exception("Failed to find patch method");
//    }
//}
