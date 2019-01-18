using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class warpGateInsertionGuide
    {
        public List<string> warpGateInsertionSpots { get; set; }

        public warpGateInsertionGuide(List<string> myWarpGateInsertionSpots)
        {
            warpGateInsertionSpots = myWarpGateInsertionSpots;
        }
    }
}
