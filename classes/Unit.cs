using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class Unit
    {
        public int buildTime { get; set; }
        public int mineralCost { get; set; }
        public int supply { get; set; }
        public int gasCost { get; set; }
        public string productionBuilding { get; set; }
        public List<string> highestTechBuildingList { get; set; }
        public string name { get; set; }
        public int numToBuild { get; set; }

        public int buildTimeWithWG { get; set; }
        //for units with a tech req and no warpGateResearch
        public Unit(int myBuildTime, int myMineralCost, int myGasCost, int mySupply, string myProductionBuilding, string myHighestTechBuilding1, int myNumToBuild, string myName)
        {
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            productionBuilding = myProductionBuilding;
            highestTechBuildingList = new List<string> { myHighestTechBuilding1 };
            numToBuild = myNumToBuild;
            name = myName;
            supply = mySupply;
            buildTimeWithWG = myBuildTime;

        }
        //for units with no tech req and no warpGateResearch
        public Unit(int myBuildTime, int myMineralCost, int myGasCost, int mySupply, string myProductionBuilding, int myNumToBuild, string myName)
        {
            supply = mySupply;
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            productionBuilding = myProductionBuilding;
            highestTechBuildingList = new List<string>();
            numToBuild = myNumToBuild;
            name = myName;
            buildTimeWithWG = myBuildTime;
        }
        //for units with no tech req and WG
        public Unit(int myBuildTime, int myMineralCost, int myGasCost, int mySupply, string myProductionBuilding, int myNumToBuild, string myName, int myBuildTimeWithWG)
        {
            supply = mySupply;
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            productionBuilding = myProductionBuilding;
            highestTechBuildingList = new List<string>();
            numToBuild = myNumToBuild;
            name = myName;
            buildTimeWithWG = myBuildTimeWithWG;
        }
        //for units with tech req and wg
        public Unit(int myBuildTime, int myMineralCost, int myGasCost, int mySupply, string myProductionBuilding, string myHighestTechBuilding1, int myNumToBuild, string myName, int myBuildTimeWithWG)
        {
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            productionBuilding = myProductionBuilding;
            highestTechBuildingList = new List<string>() { myHighestTechBuilding1 };
            numToBuild = myNumToBuild;
            name = myName;
            supply = mySupply;
            buildTimeWithWG = myBuildTimeWithWG;
        }

    }
}
