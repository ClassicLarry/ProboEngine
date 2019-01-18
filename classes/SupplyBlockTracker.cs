using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class SupplyBlockTracker
    {
        public int supplyCost { get; set; }
        public int supplyProvided { get; set; }
        public double timeOccur { get; set; }
        public SupplyBlockTracker(int mySupplyCost, int mySupplyProvided, double myTimeOccur)
        {
            supplyCost = mySupplyCost;
            supplyProvided = mySupplyProvided;
            timeOccur = myTimeOccur;
        }
    }
}
