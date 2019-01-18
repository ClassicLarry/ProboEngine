using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class bUUDataObject
    {
        public int bUUDataID { get; set; }
        public string name { get; set; }
        public int duration { get; set; }
        public int mineralCost { get; set; }

        public int gasCost { get; set; }

        public int supplyCost { get; set; }
        public bool isProductionBuilding { get; set; }
        public bool isUnit { get; set; }

        public string producedOutOf { get; set; }

        public List<string> buildingReqList { get; set; }

        public int supplyProvided { get; set; }

        public bool isBuilding { get; set; }
        public int bUUEventIDForPrimaryBuilding { get; set; }
        public int durationWithwarpGateResearch { get; set; }

        //function for units and upgrades (many have two building reqs)
        public bUUDataObject(string myName, int myDuration, int myMineralCost, int myGasCost, bool myIsProductionBuilding, bool myIsUnit, string myProducedOutOf, List<string> myBuildingReqList, int mySupplyCost)
        {
            name = myName;
            duration = myDuration;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            isProductionBuilding = myIsProductionBuilding;
            isUnit = myIsUnit;
            producedOutOf = myProducedOutOf;
            buildingReqList = new List<string>(myBuildingReqList);
            supplyCost = mySupplyCost;
            supplyProvided = 0;
            isBuilding = false;
        }

        //function for buildings
        public bUUDataObject(string myName, int myDuration, int myMineralCost, int myGasCost, bool myIsProductionBuilding, bool myIsUnit, int mySupplyProvided, bool myIsBuilding)
        {
            name = myName;
            duration = myDuration;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            isProductionBuilding = myIsProductionBuilding;
            isUnit = myIsUnit;
            supplyProvided = mySupplyProvided;
            //building so isn't produced out of anything
            producedOutOf = "";
            buildingReqList = new List<string>();
            supplyCost = 0;
            isBuilding = myIsBuilding;
        }

        //function for warpGateResearch units
        public bUUDataObject(string myName, int myDuration, int myDurationWithwarpGateResearch, int myMineralCost, int myGasCost, bool myIsProductionBuilding, bool myIsUnit, string myProducedOutOf, List<string> myBuildingReqList, int mySupplyCost)
        {
            name = myName;
            duration = myDuration;
            durationWithwarpGateResearch = myDurationWithwarpGateResearch;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            isProductionBuilding = myIsProductionBuilding;
            isUnit = myIsUnit;
            producedOutOf = myProducedOutOf;
            buildingReqList = new List<string>(myBuildingReqList);
            supplyCost = mySupplyCost;
            supplyProvided = 0;
            isBuilding = false;
        }
        //function to transfer all data variables
        public bUUDataObject(int myBUUDataID, int myBUUEventIDForPrimaryBuilding, string myName, int myDuration, int myDurationWithWarpGateResearch, int myMineralCost, int myGasCost, bool myIsProductionBuilding, bool myIsUnit, bool myIsBuilding, string myProducedOutOf, List<string> myBuildingReqList, int mySupplyCost, int mySupplyProvided)
        {
            bUUDataID = myBUUDataID;
            bUUEventIDForPrimaryBuilding = myBUUEventIDForPrimaryBuilding;
            name = myName;
            duration = myDuration;
            durationWithwarpGateResearch = myDurationWithWarpGateResearch;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            isProductionBuilding = myIsProductionBuilding;
            isUnit = myIsUnit;
            isBuilding = myIsBuilding;
            producedOutOf = myProducedOutOf;
            buildingReqList = myBuildingReqList;
            supplyCost = mySupplyCost;
            supplyProvided = mySupplyProvided;
        }
    }
}
