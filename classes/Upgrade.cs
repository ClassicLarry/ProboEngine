using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class Upgrade
    {
        public string name { get; set; }
        public int buildTime { get; set; }
        public int mineralCost { get; set; }
        public int gasCost { get; set; }
        public List<string> techReqs { get; set; }
        public string producedOutOf { get; set; }
        public bool needed { get; set; }
        
        //for two tech reqs
        public Upgrade(string myName, string myProducedOutOf, int myBuildTime, int myMineralCost, int myGasCost, string myTechReq1, string myTechReq2, bool myNeeded)
        {
            name = myName;
            producedOutOf = myProducedOutOf;
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            techReqs = new List<string>() { myTechReq1, myTechReq2 };
            needed = myNeeded;
        }
        //for one tech req
        public Upgrade(string myName, string myProducedOutOf, int myBuildTime, int myMineralCost, int myGasCost, string myTechReq, bool myNeeded)
        {
            name = myName;
            producedOutOf = myProducedOutOf;
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            techReqs = new List<string>() { myTechReq };
            needed = myNeeded;
        }
        //for no tech reqs
        public Upgrade(string myName, string myProducedOutOf, int myBuildTime, int myMineralCost, int myGasCost, bool myNeeded)
        {
            name = myName;
            producedOutOf = myProducedOutOf;
            buildTime = myBuildTime;
            mineralCost = myMineralCost;
            gasCost = myGasCost;
            techReqs = new List<string>();
            needed = myNeeded;
        }
    }
}
