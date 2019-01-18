using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class bUUPairObject
    {
        public bUUDataObject parent { get; set; }
        public bUUDataObject child { get; set; }
        public bool isChildFromWarpGate { get; set; }

        public bUUPairObject(bUUDataObject myParent, bUUDataObject myChild, bool myIsChildFromWarpGate)
        {
            parent = myParent;
            child = myChild;
            isChildFromWarpGate = myIsChildFromWarpGate;
        }
        public bUUPairObject(bUUDataObject myParent, bUUDataObject myChild)
        {
            parent = myParent;
            child = myChild;
            isChildFromWarpGate = false;
        }
    }
}
