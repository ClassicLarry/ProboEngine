using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class buildOrderWithEndTime
    {
        public double endTime { get; set; }
        public List<buildOrderEntry> buildOrder {get;set;}
        public buildOrderWithEndTime(double myEndTime, List<buildOrderEntry> myBuildOrder)
        {
            endTime = myEndTime;
            buildOrder = myBuildOrder;
        }
    }
}
