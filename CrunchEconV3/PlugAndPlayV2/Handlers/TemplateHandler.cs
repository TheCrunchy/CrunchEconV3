using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrunchEconV3.Interfaces;
using CrunchEconV3.Models;
using CrunchEconV3.PlugAndPlayV2.StationLogics;
using CrunchEconV3.Utils;
using EmptyKeys.UserInterface.Generated;

namespace CrunchEconV3.PlugAndPlayV2.Handlers
{
    public static class TemplateHandler
    {
        private static bool HasTriedToLoad = false;
        private static Dictionary<string, StationConfig> Templates = new();
        public static StationConfig? GetTemplateFromName(string templateName)
        {
            if (!HasTriedToLoad)
            {
                LoadTemplates();
            }

            return Templates.TryGetValue(templateName, out var templated) ? templated : null;
        }

        private static void LoadTemplates()
        {
            FileUtils utils = new FileUtils();
            var path = $"{Core.path}//TemplateStations";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                var template = new StationConfig();
                template.FileName = "BaseTemplate.json";
                template.Enabled = true;
                template.Logics = new List<IStationLogic>()
                {
                    new StoreLogic()
                };

                utils.WriteToJsonFile($"{path}//BaseTemplate.Json", template);
                //make a template with store logic and default setup contracts 
            }
          
        }
    }
}
