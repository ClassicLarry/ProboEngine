using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class bUUEvent
    {
        public int bUUEventID { get; set; }
        public double startTime { get; set; }
        public double endTime { get; set; }
        public int bUUDataID { get; set; }
        public int pairAsChildID { get; set; }
        public int pairAsParentID { get; set; }
        public bool fromWG { get; set; }
        public string name { get; set; } //added for debugging purposes
        public bUUEvent(int mybUUEventID, double myStartTime, double myEndTime, int myBUUDataID, int myPairAsChildID, int myPairAsParentID)
        {
            bUUEventID = mybUUEventID;
            startTime = myStartTime;
            endTime = myEndTime;
            bUUDataID = myBUUDataID;
            pairAsChildID = myPairAsChildID;
            pairAsParentID = myPairAsParentID;
            fromWG = false;
        }
        public bUUEvent(int mybUUEventID, double myStartTime, double myEndTime, int myBUUDataID, int myPairAsChildID, int myPairAsParentID, string myName)
        {
            bUUEventID = mybUUEventID;
            startTime = myStartTime;
            endTime = myEndTime;
            bUUDataID = myBUUDataID;
            pairAsChildID = myPairAsChildID;
            pairAsParentID = myPairAsParentID;
            fromWG = false;
            name = myName;
        }
    }
}
