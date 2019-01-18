using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class PairsTable
    {
        public int pairID { get; set; }
        public int parentBUUEventID { get; set; }
        public int childBUUEventID { get; set; }
        public PairsTable(int myPairID, int myParentBUUEventID, int myChildBUUEventID)
        {
            pairID = myPairID;
            parentBUUEventID = myParentBUUEventID;
            childBUUEventID = myChildBUUEventID;
        }
        public PairsTable(int myParentBUUEventID, int myChildBUUEventID)
        {
            parentBUUEventID = myParentBUUEventID;
            childBUUEventID = myChildBUUEventID;
        }
    }
}
