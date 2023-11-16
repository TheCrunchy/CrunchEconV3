using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;

namespace CrunchEconV3.Interfaces
{
    public interface IStationLogic
    {
        //This runs once when the plugin is generating the example jsons
        public void Setup();

        //this runs every 100 ticks, it is inside an async call so you can async/await inside here
        //Return true for the next logic in the list to run, return false to break the logic loop and move onto the next station 
        //some stuff may need to be wrapped inside a 
       // MyAPIGateway.Utilities.InvokeOnGameThread(() =>
      //  {
            //code here that needs to run on gamethread
     //  });
        public Task<Boolean> DoLogic(MyCubeGrid grid);

        //lower priority runs before higher
        public int Priority { get; set; }
    }
}
