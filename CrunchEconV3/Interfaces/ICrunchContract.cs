using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using CrunchEconV3.Models.Contracts;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Game.ObjectBuilders.Components.Contracts;
using VRageMath;

namespace CrunchEconV3.Interfaces
{
    public interface ICrunchContract
    {
        //this is the ID assigned to the object builder of the contract, these must be the same 
        public long ContractId { get; set; }

        //this needs to be unique per contract, eg MyName_Custom_Hauling
        public string ContractType { get; set; }

        //this is used when the contract has not been accepted by a player, for the available contracts list
        public MyObjectBuilder_Contract BuildUnassignedContract(string descriptionOverride = "");

        //this is used when the contract has been accepted, and is for the accepted contracts list 
        public MyObjectBuilder_Contract BuildAssignedContract();

        //can the player accept a contract 
        public Tuple<bool, MyContractResults> TryAcceptContract(CrunchPlayerData playerData, long identityId, MyContractBlock __instance);

        //this runs once when the contract is accepted
        public void Start();
        
        //if this returns true, the contract is seen as completed and removed from the players data
        public bool Update100(Vector3 PlayersCurrentPosition); 
        
        //this runs everytime the player opens a contract block
        public bool TryCompleteContract(ulong steamId, Vector3D? currentPosition);

        //fail logic, send messages, delete stuff, etc
        public void FailContract();

        //send the player the GPS point for the contract
        public void SendDeliveryGPS();

        //delete the delivery GPS
        public void DeleteDeliveryGPS();

        //reputation with the faction required to accept the contract
        public int ReputationRequired { get; set; }

        //the block the contract was taken from, may also be the keen station id
        public long BlockId { get; set; }

        //players identity id
        public long AssignedPlayerIdentityId { get; set; }

        //players steam id
        public ulong AssignedPlayerSteamId { get; set; }

        //players are given this reputation when they complete the contract
        public int ReputationGainOnComplete { get; set; }

        //players lose this reputation when they fail the contract
        public int ReputationLossOnAbandon { get; set; }

        //faction id of the faction the contract was taken from
        public long FactionId { get; set; }

        //how much they get paid on completion
        public long RewardMoney { get; set; }

        //any bonus of distance between start and end point
        public long DistanceReward { get; set; }

        //Where the player delivers to
        public Vector3 DeliverLocation { get; set; }
        
        //when the contract should expire 
        public DateTime ExpireAt { get; set; }

        //definition id for the contract builder, eg "MyObjectBuilder_ContractTypeDefinition/ObtainAndDeliver", this defines what icon is shown in the menu 
        public string DefinitionId { get; set; }

        //name of the contract
        public string Name { get; set; }

        //description of the contract
        public string Description { get; set; }

        //how many seconds they have to complete it
        public long SecondsToComplete { get; set; }

        //id of the delivery gps
        public int GpsId { get; set; }

        //if they can deliver the contract
        public bool ReadyToDeliver { get; set; }

        //how much they have to pay to take the contract 
        public long CollateralToTake { get; set; }

    }
}
