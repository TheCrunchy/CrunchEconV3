using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game.ObjectBuilders.Definitions;

namespace CrunchEconContractModels.DynamicEconomy
{
    [PatchShim]
    public static class KeenStoreSender
    {

        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Adding keen patch");
            Core.UpdateCycle += Update;
        }

        private static int Ticks;
        public static void Update()
        {
            Ticks++;
            if (Ticks % 600 == 0)
            {
                var itemsToSend = new List<KeenNPCStoreEntry>();
                foreach (KeyValuePair<long, MyFaction> faction in MySession.Static.Factions)
                {
                    foreach (MyStation station in faction.Value.Stations)
                    {
                        if (station.StoreItems == null)
                        {
                            continue;
                        }
                        
                        foreach (var item in station.StoreItems)
                        {
                            try
                            {
                                itemsToSend.Add(ToKeenNPCStoreEntry(item, station, faction.Value.Tag));
                            }
                            catch (Exception e)
                            {
                            }
                        }
                    }
                }

                SendStoreData(itemsToSend);
            }
        }
        public static KeenNPCStoreEntry ToKeenNPCStoreEntry(MyStoreItem Item,MyStation station, string ownerFactionTag)
        {
            return new KeenNPCStoreEntry
            {
                TypeId = Item?.Item.Value.TypeIdString ?? "",
                SubTypeId = Item?.Item.Value.SubtypeName ?? "",
                Amount = Item.Amount,
                Price = Item.PricePerUnit,
                SaleType = Item.StoreItemType == StoreItemTypes.Offer ? StoreSaleType.SellToPlayers : StoreSaleType.BuyFromPlayers,
                GridName = station.PrefabName ?? string.Empty,
                ExpireAt = DateTime.Now.AddSeconds(60),
                KeenStationId = station.Id,
                OwnerFactionTag = ownerFactionTag
            };
        }
        public static Guid ServerId = Guid.Empty;
        public static void SendStoreData(List<KeenNPCStoreEntry> entries)
        {
            Task.Run(async () =>
            {
                var client = new HttpClient();
                var url = $"https://localhost:7257/api/KeenStores/PostKeenStores";
                var message = new APIMessage
                {
                    ServerId = ServerId, // Assigning a new GUID for demonstration
                    ServerName = "Crunch Local",
                    APIKEY = "YourAPIKey"
                };

                // Serialize the list of KeenNPCStoreEntry to JSON
                var jsonSerializer = new DataContractJsonSerializer(typeof(List<KeenNPCStoreEntry>));
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    jsonSerializer.WriteObject(memoryStream, entries);
                    message.JsonMessage = Encoding.UTF8.GetString(memoryStream.ToArray());
                }

                // Serialize the APIMessage to JSON
                var messageSerializer = new DataContractJsonSerializer(typeof(APIMessage));
                string jsonMessage;
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    messageSerializer.WriteObject(memoryStream, message);
                    jsonMessage = Encoding.UTF8.GetString(memoryStream.ToArray());
                }

                // Create the HTTP request
                var content = new StringContent(jsonMessage, Encoding.UTF8, "application/json");
                HttpResponseMessage response = null;
                try
                {
                    response = await client.PostAsync(url, content); // Synchronously sending the request
                    response.EnsureSuccessStatusCode();

                    // Output the response for verification
                    var responseBody = await response.Content.ReadAsStringAsync();
                    Core.Log.Info(responseBody);
                }
                catch (Exception ex)
                {
                    Core.Log.Error($"Request failed: {ex.Message}");
                }
                finally
                {
                    if (response != null)
                    {
                        response.Dispose();
                    }

                    client.Dispose();
                }
            });
        }
    }

    [DataContract]
    public class APIMessage
    {
        [DataMember]
        public Guid ServerId { get; set; }
        [DataMember]
        public string ServerName { get; set; }
        [DataMember]
        public string APIKEY = "";
        [DataMember]
        public string JsonMessage = "";
    }

    public class StoreEntry
    {
        public string TypeId { get; set; }
        public string SubTypeId { get; set; }
        public int Amount { get; set; }
        public int Price { get; set; }
        public StoreSaleType SaleType { get; set; }
        public string GridName { get; set; }
        public DateTime ExpireAt { get; set; }

    }

    public class KeenNPCStoreEntry : StoreEntry
    {
        public long KeenStationId { get; set; }
        public string OwnerFactionTag { get; set; }
    }

    public enum StoreSaleType
    {
        BuyFromPlayers,
        SellToPlayers
    }

}
