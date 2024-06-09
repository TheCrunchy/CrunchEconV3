using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using CrunchEconV3.APIs;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using Torch;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRage.ObjectBuilder;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.Contracts
{
    public class CrunchGridDeathCombatContractImplementation : ICrunchContract
    {
        private bool HasPower(VRage.Game.ModAPI.IMyCubeGrid grid)
        {
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            foreach (var block in blocks)
            {
                var terminalBlock = block.FatBlock as IMyTerminalBlock;
                if (terminalBlock != null)
                {
                    MyResourceSourceComponent powerProducer;
                    if (terminalBlock.Components.TryGet(out powerProducer) && powerProducer.CurrentOutput > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        private bool HasActiveThrusters(IMyCubeGrid grid)
        {
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);

            foreach (var block in blocks)
            {
                var terminalBlock = block.FatBlock as IMyTerminalBlock;
                if (terminalBlock != null && terminalBlock is IMyThrust)
                {
                    var thrust = terminalBlock as IMyThrust;
                    if (thrust.IsWorking)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        public long ContractId { get; set; }
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
            return BuildUnassignedContract(Description);
        }

        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance)
        {
            if (this.DeliverLocation.Equals(Vector3.Zero))
            {
                Core.Log.Error("Error getting a target point for this contract");
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
            if (current.Count >= 1)
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

        public void Start()
        {
            ExpireAt = DateTime.Now.AddSeconds(SecondsToComplete);
            SendDeliveryGPS();
            this.ReadyToDeliver = false;
        }


        private DateTime NextSpawn = DateTime.Now;
        public int CurrentWave = 0;
        private bool HasStarted = false;

        public Dictionary<long, long> GridIdsToPay = new Dictionary<long, long>();
        private Dictionary<long, MyCubeGrid> MappedGrids = new Dictionary<long, MyCubeGrid>();
        public Dictionary<long, int> StartingBlockCounts = new Dictionary<long, int>();
        public bool Update100(Vector3 PlayersCurrentPosition)
        {
            if (ReadyToDeliver)
            {
          //     Core.Log.Info("try complete 1");
                var result = TryCompleteContract(this.AssignedPlayerSteamId, null);
                if (result)
                {
                    return true;
                }
            }
            if (DateTime.Now > ExpireAt)
            {
                FailContract();
                return true;
            }
            if (!HasStarted)
            {
                var distance = Vector3.Distance(PlayersCurrentPosition, DeliverLocation);
                if (distance < 15000)
                {
                    HasStarted = true;
                    NextSpawn = DateTime.Now;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                var distance = Vector3.Distance(PlayersCurrentPosition, DeliverLocation);
                if (distance > MaximumDistanceFromLocationToCountDamage)
                {
                    return false;
                }
            }

            var temp = new List<long>();
            foreach (var item in GridIdsToPay)
            {
                if (MappedGrids.TryGetValue(item.Key, out var grid))
                {
                    if (grid.Closed || grid.MarkedForClose)
                    {
                        UncollectedPay += item.Value;
                        temp.Add(item.Key);
                        Core.SendMessage($"{this.Name}", $"{grid.DisplayName} destroyed.", Color.LightGreen, this.AssignedPlayerSteamId);
                    }
                    else
                    {
                        if (!HasPower(grid) || !HasActiveThrusters(grid) || grid.BlocksCount <= StartingBlockCounts[grid.EntityId] / 2)
                        {
                            UncollectedPay += item.Value;
                            temp.Add(item.Key);
                            grid.SwitchPower();
                            Core.SendMessage($"{this.Name}", $"{grid.DisplayName} destroyed.", Color.LightGreen, this.AssignedPlayerSteamId);
                            //      Core.Log.Info("grid has no power");
                        }
                        else
                        {
                            //       Core.Log.Info("grid has power");
                        }
                    }
        
                }
                else
                {
                    try
                    {
                        if (MyAPIGateway.Entities.TryGetEntityById(item.Key, out var foundGrid))
                        {
                            MappedGrids.Add(item.Key, foundGrid as MyCubeGrid);
                        }

                    }
                    catch (Exception e)
                    {
                    }
                }
            }
            //dead grids should no longer be tracked
            GridIdsToPay = GridIdsToPay.Where(x => temp.All(z => z != x.Key)).ToDictionary(x => x.Key, x => x.Value);
            MappedGrids = MappedGrids.Where(x => temp.All(z => z != x.Key)).ToDictionary(x => x.Key, x => x.Value);

            if (DateTime.Now < NextSpawn) return false;

            if (!Waves.Any(x => x.WaveNumber > CurrentWave) && DateTime.Now > NextSpawn || Waves.Any(x => x.WaveNumber == CurrentWave && x.Repeat))
            {
              
                var result2 = TryCompleteContract(this.AssignedPlayerSteamId, null);
                if (result2)
                {
                    return true;
                }
                FailContract();
                return true;
            }

            var spawn = Waves.FirstOrDefault(x => x.WaveNumber == CurrentWave + 1);
            if (!spawn.Repeat)
            {
                CurrentWave = spawn.WaveNumber;
            }

            if (spawn.ContractReadyToComplete)
            {
                ReadyToDeliver = true;
            }
            NextSpawn = DateTime.Now.AddSeconds(spawn.SecondsBeforeNextWave);
            var player = MySession.Static.Players.TryGetPlayerBySteamId(this.AssignedPlayerSteamId);

            if (!spawn.SpawnGrids)
            {
                return false;
            }

            var spawns = 0;
            foreach (var grid in spawn.GridsInWave)
            {
                if (grid.ChanceToSpawn < 1)
                {
                    var random = CrunchEconV3.Core.random.NextDouble();
                    if (random > grid.ChanceToSpawn)
                    {
                        continue;
                    }
                }

                Vector3 Position = new Vector3D(PlayersCurrentPosition);
                if (this.SpawnAroundGps)
                {
                    Position = this.DeliverLocation;

                }
                
                var faction = MySession.Static.Factions.TryGetFactionByTag(grid.FacTagToOwnThisGrid);
                if (faction == null)
                {
                    Core.Log.Info($"{grid.FacTagToOwnThisGrid} faction not found");
                    continue;
                }
                Position.Add(new Vector3(Core.random.Next(spawn.MinDistance, spawn.MaxDistance),
                    Core.random.Next(spawn.MinDistance, spawn.MaxDistance),
                    Core.random.Next(spawn.MinDistance, spawn.MaxDistance)));

                if (this.WaterModSpawn)
                {
                    if (WaterModAPI.Registered)
                    {
                        var pos = WaterModAPI.GetClosestSurfacePoint(Position, null);
                       
                        if (pos != null && !pos.Equals(Vector3D.Zero))
                        {
                            Position = pos;
                        }
                    }
                }

                if (!File.Exists($"{Core.path}//Grids//{grid.GridName}")) continue;


                var Ids = GridManagerUpdated.LoadGrid($"{Core.path}//Grids//{grid.GridName}", Position, false,
                    (ulong)faction.Members.FirstOrDefault().Key, grid.GridName.Replace(".sbc", ""), false);
                foreach (var tempGrid in Ids)
                {
                    tempGrid.GridGeneralDamageModifier.ValidateAndSet(grid.TakenDamagerModifier);
                }
                if (!Ids.Any())
                {
                    Core.Log.Info($"Could not load grid {grid.GridName}");
                    
                }
                else
                {
                    var main = Ids.OrderByDescending(x => x.BlocksCount).FirstOrDefault();
                    if (main == null)
                    {
                        continue;
                    }
                    var isPay = GridsToDestroy.FirstOrDefault(x => x.GridToDestroy.Replace(".sbc", "") == grid.GridName.Replace(".sbc", ""))?.Payment ?? 0;
                    if (GridIdsToPay.ContainsKey(main.EntityId))
                    {
                        Core.Log.Info("How the fuck did this happen");
                    }
                    GridIdsToPay.Add(main.EntityId, isPay);
                    StartingBlockCounts.Add(main.EntityId, main.BlocksCount);
                    spawns += 1;
                }
            }

            if (spawns <= 0) return false;

            var playerData = Core.PlayerStorage.GetData(this.AssignedPlayerSteamId);
         //   playerData.PlayersContracts[this.ContractId] = this;
            Task.Run(async () => { CrunchEconV3.Core.PlayerStorage.Save(playerData); });

            foreach (var onlinePlayer in MySession.Static.Players.GetOnlinePlayers())
            {
                Vector3D playerPosition = onlinePlayer.Character?.PositionComp.GetPosition() ?? Vector3D.Zero;

                if (playerPosition == Vector3D.Zero) continue;
                double distance = Vector3D.Distance(PlayersCurrentPosition, playerPosition);

                if (distance <= 10000)
                {
                    Core.SendMessage(spawn.WaveMessageSender, spawn.WaveMessage, Color.Red,
                        onlinePlayer.Id.SteamId);
                }
            }
            return false;
        }

        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition)
        {
            if (UncollectedPay >= this.RewardMoney)
            {
                var pay = 0l;
                var temp = this.UncollectedPay + this.RewardMoney;
                pay = temp > this.MaximumReward ? this.MaximumReward : temp;

                EconUtils.addMoney(this.AssignedPlayerIdentityId, pay);
                Core.SendMessage("Contracts", $"{this.Name} completed!, you have been paid.", Color.Green, this.AssignedPlayerSteamId);
                if (this.ReputationGainOnComplete != 0)
                {
                    MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId,
                        this.FactionId, this.ReputationGainOnComplete, ReputationChangeReason.Contract, true);
                }

                var playerData = Core.PlayerStorage.GetData(this.AssignedPlayerSteamId);
                playerData.ContractFinished?.Invoke(true, this);
                return true;
            }

            return false;
        }

        public void FailContract()
        {
            if (this.ReputationLossOnAbandon != 0)
            {
                MySession.Static.Factions.AddFactionPlayerReputation(this.AssignedPlayerIdentityId, this.FactionId, ReputationLossOnAbandon *= -1,ReputationChangeReason.Contract);
            }
            var playerData = Core.PlayerStorage.GetData(this.AssignedPlayerSteamId);
            playerData.ContractFinished?.Invoke(false, this);
            this.DeleteDeliveryGPS();
            CrunchEconV3.Core.SendMessage("Contracts", DateTime.Now > ExpireAt ? $"{this.Name} failed, time expired." : $"{this.Name} failed.", Color.Red, this.AssignedPlayerSteamId);
        }

        public void SendDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"{this.Name} Location");
            MyGps gpsRef = new MyGps();
            gpsRef.Coords = DeliverLocation;
            gpsRef.Name = $"{this.Name} Location";
            gpsRef.GPSColor = Color.Red;
            gpsRef.ShowOnHud = true;
            gpsRef.AlwaysVisible = true;
            gpsRef.DiscardAt = new TimeSpan?();
            gpsRef.Description = sb.ToString();
            gpsRef.UpdateHash();
            gpscol.SendAddGpsRequest(AssignedPlayerIdentityId, ref gpsRef);

            GpsId = gpsRef.Hash;
        }

        public void DeleteDeliveryGPS()
        {
            MyGpsCollection gpscol = (MyGpsCollection)MyAPIGateway.Session?.GPS;
            gpscol.SendDeleteGpsRequest(this.AssignedPlayerIdentityId, GpsId);
        }

        public long MaximumDistanceFromLocationToCountDamage { get; set; }
        public int ReputationRequired { get; set; }
        public long BlockId { get; set; }
        public long AssignedPlayerIdentityId { get; set; }
        public ulong AssignedPlayerSteamId { get; set; }
        public int ReputationGainOnComplete { get; set; }
        public int ReputationLossOnAbandon { get; set; }
        public long FactionId { get; set; }
        public long RewardMoney { get; set; }
        public long MaximumReward { get; set; }
        public long DistanceReward { get; set; }
        public Vector3 DeliverLocation { get; set; }
        public DateTime ExpireAt { get; set; }
        public string DefinitionId { get; set; } = "MyObjectBuilder_ContractTypeDefinition/Escort";
        public string Name { get; set; }
        public string Description { get; set; }
        public long SecondsToComplete { get; set; }
        public int GpsId { get; set; }
        public bool ReadyToDeliver { get; set; }
        public long CollateralToTake { get; set; }
        public long DeliveryFactionId { get; set; }
        public bool SpawnAroundGps { get; set; }
        public bool WaterModSpawn { get; set; }



        public List<GridDestruction> GridsToDestroy = new List<GridDestruction>();

        public double PayPerDamage { get; set; }
        public long UncollectedPay = 0;

        public List<SpawnWave> Waves = new List<SpawnWave>();
        public List<long> DestroyedIds = new List<long>();
    }

    public class CrunchGridDeathCombatConfig : IContractConfig
    {
        public void Setup()
        {
            DeliveryGPSes = new List<string>() { "Put a gps here" };
            Waves = new List<SpawnWave>();
            Waves.Add(new SpawnWave()
            {
                WaveNumber = 1,
                SecondsBeforeNextWave = 60,
                GridsInWave = new List<GridSpawnModel>()
                {
                    new GridSpawnModel()
                }
            });
            Waves.Add(new SpawnWave()
            {
                WaveNumber = 2,
                SecondsBeforeNextWave = 60,
                GridsInWave = new List<GridSpawnModel>()
                {
                    new GridSpawnModel()
                }
            });
            GridsToDestroy = new List<GridDestruction>() { new GridDestruction() };
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
            var contract = new CrunchGridDeathCombatContractImplementation();
            var description = new StringBuilder();
            var contractContractType = "CrunchGridDeath";
            contract.ContractType = contractContractType;
            contract.BlockId = idUsedForDictionary;
            contract.RewardMoney = MinimumPay;
            contract.ReputationGainOnComplete = Core.random.Next(this.ReputationGainOnCompleteMin, this.ReputationGainOnCompleteMax);
            contract.ReputationLossOnAbandon = this.ReputationLossOnAbandon;
            contract.SecondsToComplete = this.SecondsToComplete;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Escort";
            contract.Name = this.ContractName;
            contract.ReputationRequired = this.ReputationRequired;
            contract.CollateralToTake = (Core.random.Next((int)this.CollateralMin, (int)this.CollateralMax));
            var result = AssignDeliveryGPS(__instance, keenstation, idUsedForDictionary);
            contract.MaximumDistanceFromLocationToCountDamage = this.MaximumDistanceFromLocationToCountDamageInMetres;
            contract.DeliverLocation = result.Item1;
            contract.DeliveryFactionId = result.Item2;
            contract.PayPerDamage = this.PayPerDamage;
            contract.MaximumReward = this.MaximumPay;
            
            if (contract.DeliverLocation == null || contract.DeliverLocation.Equals(Vector3.Zero))
            {
                return null;
            }

            contract.WaterModSpawn = this.WaterModSpawn;
            contract.SpawnAroundGps = this.SpawnAroundGps;
            contract.Waves = this.Waves;
            contract.GridsToDestroy = this.GridsToDestroy;
            description.AppendLine($"{this.Description}");
            contract.ReadyToDeliver = false;
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }

        public Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            var min = MinSpawnRangeInKM * 1000;
            var max = MaxSpawnRangeInKM * 1000;
            if (this.DeliveryGPSes.Any())
            {
                if (this.DeliveryGPSes != null && this.DeliveryGPSes.Any())
                {
                    var random = this.DeliveryGPSes.GetRandomItemFromList();
                    var GPS = GPSHelper.ScanChat(random);
                    if (GPS != null)
                    {
                        if (__instance != null)
                        {
                            var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                            if (faction != null)
                            {
                                return Tuple.Create(GPS.Coords, faction.FactionId);
                            }

                        }
                        if (keenstation != null)
                        {
                            return Tuple.Create(GPS.Coords, keenstation.FactionId);
                        }
                        return Tuple.Create(GPS.Coords, 0l);
                    }
                }
            }
            if (keenstation != null)
            {
                for (int i = 0; i < 10; i++)
                {
          
                    Vector3D randomDirection = MyUtils.GetRandomVector3Normalized();

                    // Generate a random distance within the specified range
                    double randomDistance = MyUtils.GetRandomDouble(min, max);

                    // Calculate the new position by adding the random direction multiplied by the random distance
                    Vector3 Position = keenstation.Position + randomDirection * randomDistance;

                    if (MyGravityProviderSystem.IsPositionInNaturalGravity(Position))
                    {
                        min += 100;
                        max += 100;
                        continue;
                    }
                    return Tuple.Create(new Vector3D(Position), keenstation.FactionId);
                }
            }


            if (__instance != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    // Generate a random direction vector
                    Vector3D randomDirection = MyUtils.GetRandomVector3Normalized();

                    // Generate a random distance within the specified range
                    double randomDistance = MyUtils.GetRandomDouble(min, max);

                    // Calculate the new position by adding the random direction multiplied by the random distance
                    Vector3D Position = __instance.CubeGrid.PositionComp.GetPosition() + randomDirection * randomDistance;
                    if (MyGravityProviderSystem.IsPositionInNaturalGravity(Position))
                    {
                        min += 100;
                        max += 100;
                        continue;
                    }
                    var faction = MySession.Static.Factions.TryGetFactionByTag(__instance.GetOwnerFactionTag());
                    if (faction != null)
                    {
                        return Tuple.Create(new Vector3D(Position), faction.FactionId);
                    }
                }
            }
            var keenEndResult = StationHandler.KeenStations.GetRandomItemFromList();
            if (keenEndResult != null)
            {
                var foundFaction = MySession.Static.Factions.TryGetFactionById(keenEndResult.FactionId);
                if (foundFaction != null)
                {
                    return Tuple.Create(keenEndResult.Position, foundFaction.FactionId);
                }
            }
            return Tuple.Create(Vector3D.Zero, 0l);
        }

        public double PayPerDamage { get; set; } = 0.05;
        public long MaximumDistanceFromLocationToCountDamageInMetres { get; set; } = 50000;
        public int AmountOfContractsToGenerate { get; set; } = 3;
        public float ChanceToAppear { get; set; } = 0.5f;
        public long CollateralMin { get; set; } = 1;
        public long CollateralMax { get; set; } = 3;
        public List<string> DeliveryGPSes { get; set; }
        public long SecondsToComplete { get; set; } = 1200;
        public int ReputationRequired { get; set; } = 0;
        public int ReputationGainOnCompleteMin { get; set; } = 1;
        public int ReputationGainOnCompleteMax { get; set; } = 3;
        public int ReputationLossOnAbandon { get; set; } = 5;
        public long MinimumPay { get; set; }
        public long MaximumPay { get; set; }
        public bool SpawnAroundGps { get; set; }
        public bool WaterModSpawn { get; set; }
        public int MinSpawnRangeInKM { get; set; } = 50;
        public int MaxSpawnRangeInKM { get; set; } = 75;
        public string ContractName { get; set; } = "Anti Piracy Operations Grid Death";
        public string Description { get; set; } = "Destroy enemy power sources to kill the grid!";

        public List<SpawnWave> Waves = new List<SpawnWave>();
        public List<GridDestruction> GridsToDestroy = new List<GridDestruction>();

    }
    public class SpawnWave
    {
        public List<GridSpawnModel> GridsInWave = new List<GridSpawnModel>();
        public string WaveMessage = "Something a pirate would say";
        public string WaveMessageSender = "Jack Sparrow";
        public int WaveNumber = 1;
        public int SecondsBeforeNextWave = 60;
        public int MinDistance = 1000;
        public int MaxDistance = 5000;
        public bool SpawnGrids = true;
        public bool Repeat = false;
        public bool ContractReadyToComplete = true;
    }

    public class GridSpawnModel
    {
        public string GridName = $"pirate.sbc";
        public double ChanceToSpawn = 0.5;
        public string FacTagToOwnThisGrid = "SPRT";
        public float TakenDamagerModifier = 1;
    }

    public class GridDestruction
    {
        public string GridToDestroy = "pirate.sbc";
        public long Payment = 50000;
    }
}
