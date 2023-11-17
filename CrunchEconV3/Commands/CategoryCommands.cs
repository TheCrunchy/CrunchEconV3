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
    [Category("cruncheconv3")]
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

        [Command("compile", "compile the .cs files")]
        [Permission(MyPromoteLevel.Admin)]
        public void Compile()
        {
            Core.myAssemblies.Clear();
            foreach (var item in Directory.GetFiles($"{Core.path}/Scripts/").Where(x => x.EndsWith(".cs")))
            {
                Compiler.Compile(item);
            }

            Context.Respond("done, check logs for any errors");
        }
        
    }
}
