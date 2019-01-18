using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class compareBUUEvents : IComparer<bUUEvent>
    {
        public int Compare(bUUEvent x, bUUEvent y)
        {
            if (x.startTime < y.startTime) { return -1; }
            else if (x.startTime > y.startTime) { return 1; }
            else { return 0; }
        }
    }
}
