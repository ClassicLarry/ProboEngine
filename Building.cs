using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
    public class Building
    {
        public string name { get; set; }
        public int buildTime { get; set; }
        public int mineralCost { get; set; }
        public int gasCost { get; set; }
        public bool isProductionBuilding { get; set; }

        public int supplyProvided { get; set; }
        public Building(string myName, int myBuildTime, int myMineralCost, int myGasCost, bool myIsProductionBuilding, int mySupplyProvided)
        {
            name = myName;
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            isProductionBuilding = myIsProductionBuilding;
            supplyProvided = mySupplyProvided;
        }
        public Building(string myName, int myBuildTime, int myMineralCost, int myGasCost, int mySupplyProvided)
        {
            name = myName;
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            isProductionBuilding = false;
            supplyProvided = mySupplyProvided;
        }
    }
}
