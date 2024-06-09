using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CrunchEconV3;
using CrunchEconV3.Utils;
using Newtonsoft.Json;
using Sandbox.Game.World;
using SpaceEngineers.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Network;
using VRageMath;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class GridSales
    {
        public static void Patch(PatchContext ctx)
        {
            Core.Log.Info("Patching button for grid sales");
            ctx.GetPattern(buttonMethod).Prefixes.Add(buttonPatch);
        }

        public static void UnPatch(PatchContext ctx)
        {
            Core.Log.Info("Unpatching button for grid sales");
            ctx.GetPattern(buttonMethod).Prefixes.Remove(buttonPatch);
        }

        internal static readonly MethodInfo buttonMethod =
            typeof(MyButtonPanel).GetMethod("ActivateButton",
                BindingFlags.Instance | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method contract");

        internal static readonly MethodInfo buttonPatch =
            typeof(GridSales).GetMethod(nameof(Activate), BindingFlags.Static | BindingFlags.Public) ??
            throw new Exception("Failed to find patch method");

        public static Dictionary<ulong, Confirm> Confirms = new Dictionary<ulong, Confirm>();

        public static bool Activate(MyButtonPanel __instance, int index)
        {
            if (string.IsNullOrEmpty(__instance.CustomData))
                return true;

          //  Core.Log.Info($"{index}");

            string customData = __instance.CustomData;
            try
            {
                var sale = JsonConvert.DeserializeObject<List<GridSale>>(customData);
                var actualSale = sale.FirstOrDefault(x => x.ButtonIndex == index);
                if (actualSale == null)
                {
                    Core.Log.Info($"No button sale found for index {index}");
                    return false; 
                }

                var path = $"{Core.path}//Grids//{actualSale.PrefabName}";

                var owningFac = MySession.Static.Factions.TryGetPlayerFaction(__instance.OwnerId);
                if (owningFac == null)
                {
                    return false;
                }

                if (Core.StationStorage.GetAll().Any(x => x.GetGrid() != null && x.GetGrid().EntityId == __instance.CubeGrid.EntityId && owningFac.Tag.Equals(x.FactionTag)))
                {
                    if (File.Exists(path))
                    {
                        ulong steamId = MyEventContext.Current.Sender.Value;

                        if (MySession.Static.Players.TryGetPlayerBySteamId(steamId, out var player))
                        {
                            if (actualSale.ReputationRequired)
                            {
                                var fac = MySession.Static.Factions.TryGetFactionByTag(actualSale.FacTagForReputation);
                                if (fac == null)
                                {
                                    Core.SendMessage("Grid Sales", "Faction for reputation requirement not found", Color.Red,steamId);
                                    return false;
                                }

                                var rep = MySession.Static.Factions.GetRelationBetweenPlayerAndFaction(
                                    player.Identity.IdentityId, fac.FactionId);

                                if (actualSale.Reputation > 0)
                                {
                                    if (rep.Item2 <= actualSale.Reputation)
                                    {
                                        var text =
                                            $"Reputation requirement not met, required {actualSale.ReputationRequired} with {actualSale.FacTagForReputation}";
                                        Core.SendMessage("Grid Sales", text, Color.Red, steamId);
                                        var message = new NotificationMessage(text, 5000, "Red");
                                        ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                        return false;
                                    }
                                }
                                else
                                {
                                    if (rep.Item2 >= actualSale.Reputation)
                                    {

                                        var text =
                                            $"Reputation requirement not met, required {actualSale.ReputationRequired} with {actualSale.FacTagForReputation}";
                                        Core.SendMessage("Grid Sales", text, Color.Red, steamId);
                                        var message = new NotificationMessage(text, 5000, "Red");
                                        ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                        return false;
                                    }
                                }

                            }
                            if (EconUtils.getBalance(player.Identity.IdentityId) >= actualSale.Price)
                            {
                                if (Confirms.TryGetValue(steamId, out var confirmation))
                                {
                                    if (DateTime.Now > confirmation.Expire)
                                    {
                                        var text = $"Confirmation expired.";
                                        Core.SendMessage("Grid Sales", text, Color.Red, steamId);
                                        var message = new NotificationMessage(text, 5000, "Red");
                                        ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                        Confirms.Remove(steamId);
                                        return false;
                                    }

                                    if (confirmation.Index != index)
                                    {
                                        var text = $"Confirmation not valid. Try again.";
                                        Core.SendMessage("Grid Sales", text, Color.Red, steamId);
                                        var message = new NotificationMessage(text, 5000, "Red");
                                        ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                        return false;
                                    }
                                    var pos = player.GetPosition();
                                    Vector3 Position = new Vector3((float)pos.X, (float)pos.Y, (float)pos.Z);
                                    if (!string.IsNullOrEmpty(actualSale.SpawnGps))
                                    {
                                        Position = GPSHelper.ScanChat(actualSale.SpawnGps).Coords;
                                    }
                                    else
                                    {
                                        Position.Add(new Vector3(
                                            Core.random.Next(actualSale.SpawnDistanceMin, actualSale.SpawnDistanceMax),
                                            Core.random.Next(actualSale.SpawnDistanceMin, actualSale.SpawnDistanceMax),
                                            Core.random.Next(actualSale.SpawnDistanceMin, actualSale.SpawnDistanceMax)));

                                    }

                                    if (GridManager.LoadGrid(path, Position, false, player.Id.SteamId,
                                            actualSale.PrefabName.Replace(".sbc", ""), false))
                                    {
                                        EconUtils.takeMoney(player.Identity.IdentityId, actualSale.Price);
                                        Confirms.Remove(steamId);
                                        return false;
                                    }
                                    else
                                    {
                                        Core.SendMessage("Grid Sales", $"Grid could not be spawned.", Color.Red, steamId);
                                    }
                                }
                                else
                                {
                                    var confirm = new Confirm()
                                    {
                                        Expire = DateTime.Now.AddSeconds(5),
                                        Index = index
                                    };

                                    Confirms.Remove(steamId);
                                    Confirms.Add(steamId, confirm);
                                    var text = $"Press button again within 5 seconds to confirm sale.";
                                    Core.SendMessage("Grid Sales",text , Color.Green, steamId);
                                    var message = new NotificationMessage(text, 5000, "Green");
                                    ModCommunication.SendMessageTo(message, player.Id.SteamId);
                                }
                            }
                            else
                            {
                                Core.SendMessage("Grid Sales", $"You cannot afford the purchase price of {actualSale.Price:##,###}", Color.Red, steamId);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Core.Log.Error($"{e}");
                return true;
            }
            return true;
        }

        public class Confirm
        {
            public int Index { get; set; }
            public DateTime Expire { get; set; }
        }
        public class GridSale
        {
            public int ButtonIndex { get; set; }
            public string PrefabName { get; set; }
            public long Price { get; set; }
            public bool ReputationRequired { get; set; }
            public string FacTagForReputation { get; set; }
            public int Reputation { get; set; }
            public int SpawnDistanceMin { get; set; }
            public int SpawnDistanceMax { get; set; }

            public string SpawnGps { get; set; } = "";
        }

        //{
        //    "prefabName": null,
        //    "price": 0
        //}
        /*
         {
             {
      "PrefabName": "pirate.sbc",
      "Price": 50000,
      "ReputationRequired": false,
      "FacTagForReputation": "DAVE",
      "Reputation": 1500,
        "SpawnDistanceMin": 1000,
        "SpawnDistanceMax": 2000,
    }
          }
        */
    }
}
