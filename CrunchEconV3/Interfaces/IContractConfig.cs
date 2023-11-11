using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;

namespace CrunchEconV3.Interfaces
{
    public interface IContractConfig
    {
        public CrunchContractTypes Type { get; set; }
    }
}
