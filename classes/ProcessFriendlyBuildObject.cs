using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class ProcessFriendlyBuildObject
    {
        public int mineralCost { get; set; }
        public int gasCost { get; set; }
        public int supplyCost { get; set; }
        public int supplyProvided { get; set; }
        public string name { get; set; }
        public double minTime { get; set; }
        public ProcessFriendlyBuildObject(int myMineralCost, int myGasCost, int mySupplyCost, int mySupplyProvided, string myName, double myMinTime)
        {
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            supplyCost = mySupplyCost;
            supplyProvided = mySupplyProvided;
            name = myName;
            minTime = myMinTime;

            //if object is unit with tech req, make minTime the time the tech req is done
            //tech reqs: cybercore, fleetBeacon, supportBay
        }
    }
}
