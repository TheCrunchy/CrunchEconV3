using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using CoreSystems.Api;
using Newtonsoft.Json;
using ProtoBuf;

namespace CrunchEconContractModels.DynamicEconomy
{
    public class KeenStoreSender
    {
        public Guid ServerId = Guid.NewGuid();
        public void SendStoreData(List<KeenNPCStoreEntry> entries)
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
                    Console.WriteLine(responseBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Request failed: {ex.Message}");
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

    public class APIMessage
    {
        public Guid ServerId { get; set; }
        public string ServerName { get; set; }
        public string APIKEY = "";
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
