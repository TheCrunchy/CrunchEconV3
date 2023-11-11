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
    public interface ICrunchPlayerStorage
    {
        public void LoadAll();

        public CrunchPlayerData GetData(ulong playerSteamId, bool loadFromLogin = false);
        public CrunchPlayerData Load(ulong playerSteamId);
        public void LoadLogin(IPlayer player);

        public void Save(CrunchPlayerData PlayerData);
    }
}
