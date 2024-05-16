using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.Utils;
using Torch.API;

namespace CrunchEconV3.Handlers
{
    public class JsonPlayerStorageHandler : ICrunchPlayerStorage
    {
        private static Dictionary<ulong, CrunchPlayerData> LoadedData { get; set; } = new Dictionary<ulong, CrunchPlayerData>();
        private string BasePath { get; set; }
        private FileUtils FileUtils { get; set; } = new FileUtils();

        public JsonPlayerStorageHandler(string BasePath)
        {
            this.BasePath = $"{BasePath}/PlayerData/";
            Directory.CreateDirectory(this.BasePath);
            LoadAll();
        }

        public void LoadAll()
        {
            foreach (var path in Directory.GetFiles(BasePath))
            {
                var data = LoadFile(path);
                if (LoadedData.ContainsKey(data.PlayerSteamId))
                {
                    LoadedData[data.PlayerSteamId] = data;
                    continue;
                }
                LoadedData.Add(data.PlayerSteamId, data);
            }
        }

        public CrunchPlayerData GetData(ulong playerSteamId, bool loadFromLogin = false)
        {
            if (loadFromLogin)
            {
                var loadedFromFile = Load(playerSteamId);
                if (loadedFromFile != null)
                {
                    if (LoadedData.ContainsKey(playerSteamId))
                    {
                        LoadedData[playerSteamId] = loadedFromFile;
                    }
                    else
                    {
                        LoadedData.Add(playerSteamId, loadedFromFile);
                    }
                    return loadedFromFile;
                }
            }
            else
            {
                if (LoadedData.TryGetValue(playerSteamId, out var data))
                {
                    return data;
                }

                var loadedFromFile = Load(playerSteamId);
                if (loadedFromFile != null)
                {
                    return loadedFromFile;
                }
            }

            var newData = new CrunchPlayerData()
            {
                PlayersContracts = new Dictionary<long, ICrunchContract>(),
                PlayerSteamId = playerSteamId,
            };
            LoadedData.Add(playerSteamId, newData);
            Save(newData);
            return newData;
        }

        public CrunchPlayerData Load(ulong steamId)
        {
            var path = $"{BasePath}/{steamId}.json";
            return File.Exists(path) ? LoadFile(path) : null;
        }

        public void LoadLogin(IPlayer player)
        {
            GetData(player.SteamId, true);
        }

        public CrunchPlayerData LoadFile(string path)
        {
            try
            {
                var file = FileUtils.ReadFromJsonFile<CrunchPlayerData>(path);

                return file;
            }
            catch (Exception e)
            {
                return null;
            }
        }
        public void Save(CrunchPlayerData PlayerData)
        {
            try
            {
                var path = $"{BasePath}/{PlayerData.PlayerSteamId}.json";
                FileUtils.WriteToJsonFile(path, PlayerData);
            }
            catch (Exception)
            {

            }
        }
    }
}
