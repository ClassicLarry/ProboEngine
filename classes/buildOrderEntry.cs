using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class buildOrderEntry
    {
        public string itemToBuild { get; set; }
        public double timeToBuild { get; set; }
        public buildOrderEntry(string myItemToBuild, double myTimeToBuild)
        {
            itemToBuild = myItemToBuild;
            timeToBuild = myTimeToBuild;
        }
    }
}
