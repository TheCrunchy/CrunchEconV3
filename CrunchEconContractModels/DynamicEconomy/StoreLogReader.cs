using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconContractModels.DynamicEconomy
{
    public static class StoreLogReader
    {
        public static StoreTransaction ParseStoreTransaction(string data)
        {
            // Split the input string by commas
            var parts = data.Split(',');

            // Ensure the parts length is as expected (9 parts)
            if (parts.Length != 9)
            {
                throw new ArgumentException("Input string is not in the expected format.");
            }

            // Create an instance of StoreTransaction and populate it
            var transaction = new StoreTransaction
            {
                SteamId = parts[0],
                Action = parts[1],
                Amount = int.Parse(parts[2]),
                TypeIdString = parts[3],
                SubtypeName = parts[4],
                TotalPrice = long.Parse(parts[5]),
                CubeGridEntityId = long.Parse(parts[6]),
                OwnerFactionTag = parts[7],
                CubeGridDisplayName = parts[8]
            };

            return transaction;
        }

        public class StoreTransaction
        {
            public string SteamId { get; set; }
            public string Action { get; set; }
            public int Amount { get; set; }
            public string TypeIdString { get; set; }
            public string SubtypeName { get; set; }
            public long TotalPrice { get; set; }
            public long CubeGridEntityId { get; set; }
            public string OwnerFactionTag { get; set; }
            public string CubeGridDisplayName { get; set; }
        }
    }
}
