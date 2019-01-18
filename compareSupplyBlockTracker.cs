using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class compareSupplyBlockTracker :IComparer<SupplyBlockTracker>
    {
        public int Compare(SupplyBlockTracker x, SupplyBlockTracker y)
        {
            if (x.timeOccur < y.timeOccur) { return -1; }
            else if (x.timeOccur > y.timeOccur) { return 1; }
            else { return 0; }
        }
    }
}
