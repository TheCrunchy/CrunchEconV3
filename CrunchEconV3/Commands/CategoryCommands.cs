using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrunchEconV3.Handlers;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CSharp;
using NLog;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace CrunchEconV3.Commands
{
    [Category("crunchecon")]
    public class CategoryCommands : CommandModule
    {

        [Command("reload", "example command usage !categorycommands example")]
        [Permission(MyPromoteLevel.Admin)]
        public void Example()
        {
            StationHandler.BlocksContracts.Clear();
            StationHandler.ReadyForRefresh();
            StationHandler.MappedStations.Clear();
            Core.ReloadConfig();
            Core.StationStorage.LoadAll();

            foreach (var station in Core.StationStorage.GetAll())
            {
                station.SetFirstLoad(true);
            }

            Context.Respond("Reloaded and cleared existing contracts");
            Context.Respond("If changing scripts, use the compile command to apply changes");
        }

        [Command("addlogic", "addlogic to a station")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddLogic(string stationName, string logicType)
        {
            if (!stationName.EndsWith(".json"))
            {
                stationName += ".json";
            }
            var station = Core.StationStorage.GetAll().FirstOrDefault(x => x.FileName == stationName);
            if (station == null)
            {
                Context.Respond("Station not found");
                return;
            }
            var configs2 = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                           where t.IsClass && t.GetInterfaces().Contains(typeof(IStationLogic))
                           select t;

            if (configs2.Any(x => x.Name == logicType))
            {
                var logic = configs2.FirstOrDefault(x => x.Name == logicType);
                IStationLogic instance = (IStationLogic)Activator.CreateInstance(logic);
                instance.Setup();
                station.Logics.Add(instance);
                Core.StationStorage.Save(station);
                Context.Respond("Logic added to station, load it with !crunchecon reload");
            }
            else
            {
                Context.Respond("Logic type not found, see all available logics with !crunchecon list");
            }
        }
        [Command("addcontract", "addlogic to a station")]
        [Permission(MyPromoteLevel.Admin)]
        public void AddContract(string contractFileName, string contractType)
        {
            foreach (var file in Directory.GetDirectories($"{Core.path}/Stations/"))
            {
                foreach (var actualFile in Directory.GetFiles(file))
                {
                    if (Path.GetFileNameWithoutExtension(actualFile) != contractFileName)
                    {
                        continue;
                    }
                    var configs2 = from t in Core.myAssemblies.Select(x => x)
                            .SelectMany(x => x.GetTypes())
                        where t.IsClass && t.GetInterfaces().Contains(typeof(IContractConfig))
                        select t;
                    var utils = new FileUtils();
                    var contracts =
                        utils.ReadFromJsonFile<List<IContractConfig>>(actualFile);

                    if (configs2.Any(x => x.Name == contractType))
                    {
                        var logic = configs2.FirstOrDefault(x => x.Name == contractType);
                        IContractConfig instance = (IContractConfig)Activator.CreateInstance(logic);
                        instance.Setup();
                        contracts.Add(instance);
                        utils.WriteToJsonFile(actualFile, contracts);
                        Context.Respond("Contract added to file, load it with !crunchecon reload");
                        return;
                    }
                    else
                    {
                        Context.Respond("Contract type not found, see all available logics with !crunchecon list");
                    }
                }
 
            }

            Context.Respond("Contract file not found");
            return;

        }

        [Command("list", "list all valid names from scripts")]
        [Permission(MyPromoteLevel.Admin)]
        public void List()
        {
            var configs = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                          where t.IsClass && t.GetInterfaces().Contains(typeof(IContractConfig))
                          select t;

            var configs2 = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                           where t.IsClass && t.GetInterfaces().Contains(typeof(IStationLogic))
                           select t;

            foreach (var config in configs)
            {
                Context.Respond($"CONTRACT CONFIG {config.Name}");
            }

            foreach (var config in configs2)
            {
                Context.Respond($"STATION LOGIC {config.Name}");
            }

            foreach (var file in Directory.GetDirectories($"{Core.path}/Stations/"))
            {
                foreach (var file2 in Directory.GetFiles(file))
                {
                    Context.Respond($"CONTRACT FILE {Path.GetFileNameWithoutExtension(file2)}");
                }
            }
            foreach (var file in Directory.GetFiles($"{Core.path}/Stations/"))
            {
                Context.Respond($"STATION FILE {Path.GetFileNameWithoutExtension(file)}");
            }
        }

        [Command("compile", "compile the .cs files")]
        [Permission(MyPromoteLevel.Admin)]
        public void Compile()
        {
            Core.myAssemblies.Clear();
            foreach (var item in Directory.GetFiles($"{Core.path}/Scripts/").Where(x => x.EndsWith(".cs")))
            {
                Compiler.Compile(item);
            }
            var configs = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                          where t.IsClass && t.GetInterfaces().Contains(typeof(IContractConfig))
                          select t;

            var configs2 = from t in Core.myAssemblies.Select(x => x)
                    .SelectMany(x => x.GetTypes())
                           where t.IsClass && t.GetInterfaces().Contains(typeof(IStationLogic))
                           select t;

            foreach (var config in configs2)
            {
                IStationLogic instance = (IStationLogic)Activator.CreateInstance(config);
                instance.Setup();
            }
            Context.Respond("done, check logs for any errors");
        }

    }
}
