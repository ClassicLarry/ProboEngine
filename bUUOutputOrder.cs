using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class bUUOutputOrder
    {
        public int bUUDataID { get; set; }
        public double minTime { get; set; }
        public bUUOutputOrder(int myBUUDataID, double myMinTime)
        {
            bUUDataID = myBUUDataID;
            minTime = myMinTime;
        }
    }
}
