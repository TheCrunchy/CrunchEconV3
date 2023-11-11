using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Models;
using Torch.API;
using VRage.GameServices;

namespace CrunchEconV3.Interfaces
{
    public interface ICrunchStationStorage
    {
        public void LoadAll();
        public void Save(StationConfig PlayerData);
        public void GenerateExample();
        public List<StationConfig> GetAll();
    }
}
