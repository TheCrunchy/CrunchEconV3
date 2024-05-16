using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers.PatchManager;
using VRage.Game.ModAPI;

namespace CrunchEconContractModels.DynamicEconomy
{
    public class StoreLogCommands : CommandModule
    {
        [Command("runlog", "run log analysis")]
        [Permission(MyPromoteLevel.Admin)]
        public void ExportStore()
        {
            StoreLogReader.Process();
        }
    }

    [PatchShim]
        public static class StoreLogReader
        {

            public static void Patch(PatchContext ctx)
            {
                Directory.CreateDirectory(NexusPath);

                Process();
            }

            public static void Process()
            {
                var files = ProcessFilesInParallel(NexusPath);
                foreach (var item in files)
                {
                    Core.Log.Info($"{item.Key} {item.Value.StoreName} {item.Value.Action} {item.Value.TotalPrice} {item.Value.Amount} ");

                }

            }

            public static ConcurrentDictionary<string, StoreTransaction> ProcessFilesInParallel(string nexusPath)
            {
                var transactions = new ConcurrentDictionary<string, StoreTransaction>();

                var files = Directory.GetFiles(nexusPath);

                Parallel.ForEach(files, file =>
                {
                    var item = File.ReadAllText(file);
                    var parsed = ParseStoreTransaction(item);
                    var key = $"{parsed.StoreName}-{parsed.TypeIdString}-{parsed.SubtypeName}-{parsed.Action}-{parsed.Time:dd-MM-yyyy}";

                    transactions.AddOrUpdate(
                        key,
                        parsed,
                        (existingKey, existingValue) =>
                        {
                            existingValue.Amount += parsed.Amount;
                            existingValue.TotalPrice += parsed.TotalPrice;
                            return existingValue;
                        });
                });

                return transactions;
            }

            public static string NexusPath = $"{Core.path}//EconEntries//";

            public static StoreTransaction ParseStoreTransaction(string data)
            {
                // Split the input string by commas
                var parts = data.Split(',');

                // Ensure the parts length is as expected (9 parts)
                if (parts.Length != 11)
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
                    CubeGridDisplayName = parts[8],
                    StoreName = parts[9],
                    Time = DateTime.Parse(parts[10])
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
                public string StoreName { get; set; }
                public DateTime Time { get; set; }
            }
        }
    }
