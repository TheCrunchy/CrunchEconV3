using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Abstracts;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using VRage.Utils;
using VRageMath;

namespace CrunchEconContractModels.PlugAndPlay.Contracts.Configs
{
    public class RepairContractConfig : ContractConfigAbstract
    {
        public long RewardMin { get; set; } = 50;
        public long RewardMax { get; set; } = 75;
        public bool DeleteGridOnCompletion { get; set; } = true;
        public int MinSpawnRangeInKM { get; set; } = 50;
        public int MaxSpawnRangeInKM { get; set; } = 75;

        public override ICrunchContract GenerateTheRest(ICrunchContract cont,MyContractBlock __instance, MyStation keenstation, long idUsedForDictionary)
        {
            var contract = cont as RepairContractImplementation;
            var description = new StringBuilder();
            contract.RewardMoney = CrunchEconV3.Core.random.Next((int)this.RewardMin, (int)this.RewardMax);
            contract.BlockId = idUsedForDictionary;
            contract.DeleteGridOnComplete = this.DeleteGridOnCompletion;
            contract.DefinitionId = "MyObjectBuilder_ContractTypeDefinition/Repair";
            contract.Name = $"Repair Contract";
            contract.ReputationRequired = this.ReputationRequired;
            description.AppendLine($"Repair the grid found at the target location.");
            if (this.ReputationRequired != 0)
            {
                description.AppendLine($" ||| Reputation with owner required: {this.ReputationRequired}");
            }

            contract.Description = description.ToString();
            return contract;
        }
        public override Tuple<Vector3D, long> AssignDeliveryGPS(MyContractBlock __instance, MyStation keenstation,
          long idUsedForDictionary)
        {
            var min = MinSpawnRangeInKM;
            var max = MaxSpawnRangeInKM;
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
                            var faction = FacUtils.GetPlayersFaction(__instance.OwnerId);
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

            if (__instance != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    // Generate a random direction vector
                    Vector3D randomDirection = MyUtils.GetRandomVector3Normalized();

                    // Generate a random distance within the specified range
                    double randomDistance = MyUtils.GetRandomDouble(min, max) * 1000;

                    // Calculate the new position by adding the random direction multiplied by the random distance
                    Vector3D Position = __instance.CubeGrid.PositionComp.GetPosition() + randomDirection * randomDistance;
                    if (MyGravityProviderSystem.IsPositionInNaturalGravity(Position))
                    {
                        min += 100;
                        max += 100;
                        continue;
                    }
                    var faction = FacUtils.GetPlayersFaction(__instance.OwnerId);
                    if (faction != null)
                    {
                        return Tuple.Create(new Vector3D(Position), faction.FactionId);
                    }
                }
            }
            return Tuple.Create(Vector3D.Zero, 0l);
        }
    }
}
