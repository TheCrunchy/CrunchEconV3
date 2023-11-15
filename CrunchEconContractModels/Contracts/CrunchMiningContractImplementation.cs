using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.ObjectBuilders;
using VRage.ObjectBuilders.Private;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Contracts
{
    public class CrunchMiningContractImplementation : ICrunchContract
    {
        public string ContractType { get; set; }
        public MyObjectBuilder_Contract BuildUnassignedContract(string descriptionOverride = "")
        {
            string definition = this.DefinitionId;
            string contractName = this.Name;
            string contractDescription;
            contractDescription = descriptionOverride != "" ? descriptionOverride : this.Description;

            if (!MyDefinitionId.TryParse(definition, out var definitionId)) return null;
            var newContract = new MyObjectBuilder_ContractCustom
            {
                SubtypeName = definition.Replace("MyObjectBuilder_ContractTypeDefinition/", ""),
                Id = this.ContractId,
                IsPlayerMade = false,
                State = MyContractStateEnum.Active,
                Owners = new MySerializableList<long>(),
                RewardMoney = this.RewardMoney,
                RewardReputation = this.ReputationGainOnComplete,
                StartingDeposit = this.CollateralToTake,
                FailReputationPrice = this.ReputationLossOnAbandon,
                StartFaction = 1,
                StartStation = 0,
                StartBlock = this.BlockId,
                Creation = 1,
                TicksToDiscard = (int?)this.SecondsToComplete,
                RemainingTimeInS = this.SecondsToComplete,
                ContractCondition = null,
                DefinitionId = definitionId,
                ContractName = contractName,
                ContractDescription = contractDescription
            };

            return newContract;
        }

        public MyObjectBuilder_Contract BuildAssignedContract()
        {
            var contractDescription = $"You must go mine {this.AmountToMine - this.MinedOreAmount:##,###} {this.OreSubTypeName} using a ship drill, then return here.";
            return BuildUnassignedContract(contractDescription);
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            if (this.DeliverLocation.Equals(Vector3.Zero))
            {
                Core.Log.Error("Error getting a delivery point for this contract");
                return Tuple.Create(false, MyContractResults.Error_InvalidData);
            }
            if (this.ReputationRequired != 0)
            {
                var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                if (faction != null)
                {
                    var reputation =
                        MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(identityId, faction.FactionId);
                    if (this.ReputationRequired > 0)
                    {
                        if (reputation.Item2 < this.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                    else
                    {
                        if (reputation.Item2 > this.ReputationRequired)
                        {
                            return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet);
                        }
                    }
                }
            }
            if (this.CollateralToTake > 0)
            {
                if (EconUtils.getBalance(identityId) < this.CollateralToTake)
                {
                    return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_InsufficientFunds);
                }
            }

            var current = playerData.GetContractsForType(this.ContractType);
            if (current.Count >= 3)
            {
                return Tuple.Create(false, MyContractResults.Fail_ActivationConditionsNotMet_ContractLimitReachedHard);
            }

            if (this.CollateralToTake > 0)
            {
                EconUtils.takeMoney(identityId, this.CollateralToTake);
            }
            this.AssignedPlayerIdentityId = identityId;
            this.AssignedPlayerSteamId = playerData.PlayerSteamId;
            return Tuple.Create(true, MyContractResults.Success);
        }
        public void FailContract()
        {
            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1);
            }

            CrunchEconV3.Core.SendMessage("Contracts", DateTime.Now > ExpireAt ? $"{this.Name} failed, time expired." : $"{this.Name} failed.", Color.Red,
                this.AssignedPlayerSteamId);
        }
        public void SendDeliveryGPS()
        {
            if (!ReadyToDeliver)
            {
                return;
            }
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Deliver " + this.AmountToMine + " " + this.OreSubTypeName + " Ore.");
            sb.AppendLine("Contract Delivery Location.");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"Mining Delivery Location {this.OreSubTypeName}";
            gpsRef.GPSColor = Color.Orange;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = TimeSpan.FromSeconds(6000);
            gpsRef.Description = sb.ToString();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
        }

        public void DeleteDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            gpscol.SendDeleteGpsRequest(this.AssignedPlayerIdentityId, GpsId);
        }
        public void Start()
        {
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
        }
        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (DateTime.Now > ExpireAt)
            {
                FailContract();
                return true;
            }

            return false;
        }
        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            try
            {
                if (MinedOreAmount < AmountToMine) return false;
                if (!MySession.Static.Players.TryGetPlayerBySteamId((ulong)this.AssignedPlayerSteamId, out var player))
                    return false;
                float distance = Vector3.Distance(this.DeliverLocation, (Vector3)currentPosition);
                if (!(distance <= 500)) return false;
                Dictionary<MyDefinitionId, int> itemsToRemove = new Dictionary<MyDefinitionId, int>();
                var parseThis = "MyObjectBuilder_Ore/" + this.OreSubTypeName;
                if (MyDefinitionId.TryParse(parseThis, out MyDefinitionId id))
                {
                    itemsToRemove.Add(id, this.AmountToMine);
                }
                var sphere = new BoundingSphereD(this.DeliverLocation, 1000 * 2);
                var playersGrids = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                    .Where(x => x.BlocksCount > 0 && FacUtils.IsOwnerOrFactionOwned(x, this.AssignedPlayerIdentityId, true)).ToList();
                List<VRage.Game.ModAPI.IMyInventory> inventories = new List<IMyInventory>();
                foreach (var grid in playersGrids)
                {
                    inventories.AddRange(InventoriesHandler.GetInventoriesForContract(grid));
                }

                if (!InventoriesHandler.ConsumeComponents(inventories, itemsToRemove, player.Id.SteamId)) return false;

                EconUtils.addMoney(this.AssignedPlayerIdentityId, this.RewardMoney);
                if (this.ReputationGainOnComplete != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        this.FactionId, this.ReputationGainOnComplete, true);
                }
                inventories.Clear();
                if (this.SpawnOreInStation)
                {

                    var foundCargo = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere).OfType<MyCubeGrid>()
                        .Where(x => x.BlocksCount > 0).ToList();
                    if (foundCargo.Any())
                    {
                        foreach (var cargo in foundCargo)
                        {
        
                            var owner = FacUtils.GetOwner(cargo);
                            var fac = MySession.Static.Factions.TryGetPlayerFaction(owner);

                            if (fac != null && fac.FactionId == this.FactionId)
                            {
                                inventories.AddRange(InventoriesHandler.GetInventoriesForContract(cargo));
                            }
                        }

                        InventoriesHandler.SpawnItems(id, this.AmountToMine, inventories);
                    }
                }
            }
            catch (Exception e)
            {
                CrunchEconV3.Core.Log.Error($"Mining try complete error {e}");
                return true;
            }
            return true;

        }
        public long ContractId { get; set; }
        public long BlockId { get; set; }
        public long AssignedPlayerIdentityId { get; set; }
        public ulong AssignedPlayerSteamId { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public long FactionId { get; set; }
        public long RewardMoney { get; set; }
        public long DistanceReward { get; set; }
        public Vector3 DeliverLocation { get; set; }

        public String OreSubTypeName { get; set; }
        public int MinedOreAmount { get; set; }
        public int AmountToMine { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }
        public bool SpawnOreInStation { get; set; }
        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
        public int ReputationRequired { get; set; }

    
    }

    [PatchShim]
    public static class DrillPatch
    {

        public static void Patch(PatchContext ctx)
        {
            CrunchEconV3.Core.Log.Error("PATCHING DRILL");
            ctx.GetPattern(update).Suffixes.Add(updatePatch);
        }

        public static Dictionary<ulong, DateTime> messageCooldown = new Dictionary<ulong, DateTime>();

        internal static readonly MethodInfo update =
            typeof(MyDrillBase).GetMethod("OnDrillResults", BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new Exception("Failed to find patch method");

        internal static readonly MethodInfo updatePatch =
            typeof(DrillPatch).GetMethod(nameof(PatchResults), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static Type drill = null;
        public static void PatchResults(MyDrillBase __instance,
            Dictionary<MyVoxelMaterialDefinition, int> materials,
            Vector3D hitPosition,
            bool collectOre,
            Action<bool> OnDrillingPerformed = null)
        {
            if (!collectOre)
            {
                return;
            }
            if (__instance.OutputInventory != null && __instance.OutputInventory.Owner != null)
            {
                if (__instance.OutputInventory.Owner.GetBaseEntity() is MyShipDrill shipDrill)
                {
                    if (drill == null)
                    {
                        drill = __instance.GetType();
                    }

                    var owner = shipDrill.OwnerId;
                    var data = Core.PlayerStorage.GetData(MySession.Static.Players.TryGetSteamId(owner));
                    if (data != null && data.PlayersContracts.All(x => !x.Value.ContractType.Equals("CrunchMining")))
                    {
                        return;
                    }

                    Dictionary<string, int> MinedAmount = new Dictionary<string, int>();
                    foreach (var material in materials)
                    {
                        if (string.IsNullOrEmpty(material.Key.MinedOre))
                            return;

                        if (material.Value <= 0)
                        {
                            continue;
                        }
                        MyObjectBuilder_Ore newObject = MyObjectBuilderSerializerKeen.CreateNewObject<MyObjectBuilder_Ore>(material.Key.MinedOre);
                        newObject.MaterialTypeName = new MyStringHash?(material.Key.Id.SubtypeId);
                        float num = (float)(material.Value / (double)byte.MaxValue * 1.0) * __instance.VoxelHarvestRatio * material.Key.MinedOreRatio;
                        if (!MySession.Static.AmountMined.ContainsKey(material.Key.MinedOre))
                            MySession.Static.AmountMined[material.Key.MinedOre] = (MyFixedPoint)0;
                        MySession.Static.AmountMined[material.Key.MinedOre] += (MyFixedPoint)num;
                        MyPhysicalItemDefinition physicalItemDefinition = MyDefinitionManager.Static.GetPhysicalItemDefinition((MyObjectBuilder_Base)newObject);
                        MyFixedPoint amountItems1 = (MyFixedPoint)(num / physicalItemDefinition.Volume);
                        MyFixedPoint maxAmountPerDrop = (MyFixedPoint)(float)(0.150000005960464 / (double)physicalItemDefinition.Volume);



                        MyFixedPoint collectionRatio = (MyFixedPoint)drill.GetField("m_inventoryCollectionRatio", BindingFlags.NonPublic | BindingFlags.Instance).GetValue((object)__instance);

                        MyFixedPoint b = amountItems1 * ((MyFixedPoint)1 - collectionRatio);
                        MyFixedPoint amountItems2 = MyFixedPoint.Min(maxAmountPerDrop * 10 - (MyFixedPoint)0.001, b);
                        MyFixedPoint totalAmount = amountItems1 * collectionRatio - amountItems2;

                        if (totalAmount > 0)
                        {
                            if (MinedAmount.ContainsKey(material.Key.MinedOre))
                            {
                                MinedAmount[material.Key.MinedOre] += totalAmount.ToIntSafe();
                            }
                            else
                            {
                                MinedAmount.Add(material.Key.MinedOre, totalAmount.ToIntSafe());
                            }
                        }
                    }

                    foreach (var mined in MinedAmount)
                    {
                        var contracts = data.PlayersContracts.Where(x => x.Value.ContractType is "CrunchMining");
                        foreach (var contract in contracts)
                        {
                            var mining = contract.Value as CrunchMiningContractImplementation;
                            if (mining.OreSubTypeName != mined.Key) continue;
                            if (mining.MinedOreAmount >= mining.AmountToMine) continue;
                            mining.MinedOreAmount += mined.Value;
                            data.PlayersContracts[mining.ContractId] = mining;
                            if (mining.MinedOreAmount >= mining.AmountToMine)
                            {
                                mining.ReadyToDeliver = true;
                                mining.SendDeliveryGPS();
                                CrunchEconV3.Core.SendMessage("Contracts",
                                    "Contract Ready to be completed, Deliver " + $"{mining.AmountToMine:##,###}" + " " + mining.OreSubTypeName +
                                    " to the delivery GPS.", Color.Gold,
                                    MySession.Static.Players.TryGetSteamId(owner));
                                messageCooldown.Remove(MySession.Static.Players.TryGetSteamId(owner));
                                messageCooldown.Add(MySession.Static.Players.TryGetSteamId(owner),
                                    DateTime.Now.AddSeconds(0.5));
                                Task.Run(async () =>
                                {
                                    CrunchEconV3.Core.PlayerStorage.Save(data);
                                });

                                return;
                            }
                            if (messageCooldown.TryGetValue(MySession.Static.Players.TryGetSteamId(owner),
                                    out DateTime time))
                            {
                                if (DateTime.Now < time) return;
                                CrunchEconV3.Core.SendMessage("Contracts",
                                    "Progress: " + mined.Key + " " +
                                    $"{mining.MinedOreAmount:##,###}" + " / " +
                                    $"{mining.AmountToMine:##,###}", Color.Gold,
                                    MySession.Static.Players.TryGetSteamId(owner));
                                messageCooldown[MySession.Static.Players.TryGetSteamId(owner)] =
                                    DateTime.Now.AddSeconds(0.5);


                            }
                            else
                            {
                                CrunchEconV3.Core.SendMessage("Boss Dave",
                                    "Progress: " + mined.Key + " " + $"{mining.MinedOreAmount:##,###}" + " / " + $"{mining.AmountToMine:##,###}", Color.Gold,
                                    MySession.Static.Players.TryGetSteamId(owner));
                                messageCooldown.Add(MySession.Static.Players.TryGetSteamId(owner),
                                    DateTime.Now.AddSeconds(0.5));

                            }

                            Task.Run(async () =>
                            {
                                CrunchEconV3.Core.PlayerStorage.Save(data);
                            });

                            return;
                        }
                    }

                }
            }

        }
    }

    public class MiningContractConfig : IContractConfig
    {       //check the discord for documentation on what each thing in the interface does 
        //https://discord.gg/cQFJeKvVAA
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
            OresToPickFrom = new List<string>() { "Iron,Nickel,Cobalt,Stone,Magnesium,Gold" };
        }
        public ICrunchContract GenerateFromConfig(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            if (this.ChanceToAppear < 1)
            {
                var random = CrunchEconV3.Core.random.NextDouble();
                if (random > this.ChanceToAppear)
                {
                    return null;
                }
            }
            var description = new StringBuilder();
            var contract = new CrunchMiningContractImplementation();
            contract.AmountToMine = Core.random.Next(this.AmountToMineThenDeliverMin, this.AmountToMineThenDeliverMax);
            contract.RewardMoney = contract.AmountToMine * (Core.random.Next((int)this.PricePerItemMin, (int)this.PricePerItemMax));
            contract.DeliverLocation = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.ContractType = "CrunchMining";
            contract.BlockId = idUsedForDictionary;
            contract.OreSubTypeName = this.OresToPickFrom.GetRandomItemFromList();
            contract.ReputationGainOnComplete = Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/ObtainAndDeliver";
            contract.Name = $"{contract.OreSubTypeName} Mining Contract";
            contract.ReputationRequired = this.ReputationRequired;
            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            contract.SpawnOreInStation = this.SpawnOreInStation;
            description.AppendLine($"You must go mine {contract.AmountToMine:##,###} {contract.OreSubTypeName} using a ship drill, then return here.");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}".PadRight(69, '_'));
            }

            contract.Description = description.ToString();

            return contract;
        }

        public Vector3 AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            return __instance != null ? __instance.PositionComp.GetPosition() : keenstation.Position;
        }

        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 1;
        public List<string> DeliveryGPSes { get; set; }
        public long PricePerItemMin { get; set; } = 1;
        public long PricePerItemMax { get; set; } = 3;
        public int AmountToMineThenDeliverMin { get; set; } = 1;
        public int AmountToMineThenDeliverMax { get; set; } = 10;
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationRequired { get; set; } = 0;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 5;
        public int ReputationLossOnAbandon { get; set; } = 10;
        public List<String> OresToPickFrom { get; set; }
        public bool SpawnOreInStation { get; set; }
    }
}
