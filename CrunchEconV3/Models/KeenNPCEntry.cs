using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrunchEconV3.Models
{
    public class KeenNPCEntry
    {
        public List<string> NPCFactionTags { get; set; }
        public List<string> ContractFiles { get; set; }
    }

    public class KeenStoreFileEntry
    {
        public List<string> NPCFactionTags { get; set; }
        public string StoreFileName { get; set; }
    }
}
