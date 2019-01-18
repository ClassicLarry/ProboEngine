using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class EventInBuild
    {
        public double startTime { get; set; }
        public double endTime { get; set; }
        public int mineralCost { get; set; }

        public int gasCost { get; set; }
        public string productionBuilding { get; set; }
        public string name { get; set; }
        public bool childless { get; set; }
        public EventInBuild(string myName, double myStartTime, double myEndTime, int myMineralCost, int myGasCost, string myProductionBuilding, bool myChildless)
        {
            startTime = myStartTime;
            endTime = myEndTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            name = myName;
            productionBuilding = myProductionBuilding;
            childless = myChildless;
        }
    }
}
