using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class buildingsNeededClass
    {
        public bool gateway { get; set; }
        public bool cyberCore { get; set; }
        public bool twilightCouncil { get; set; }
        public bool templarArchives { get; set; }
        public bool darkShrine { get; set; }
        public bool supportBay { get; set; }
        public bool roboticsFacility { get; set; }
        public bool stargate { get; set; }
        public bool fleetBeacon { get; set; }
        public bool forge { get; set; }
        public buildingsNeededClass()
        {
            forge = false;
            gateway = false;
            cyberCore = false;
            twilightCouncil = false;
            templarArchives = false;
            darkShrine = false;
            supportBay = false;
            roboticsFacility = false;
            stargate = false;
            fleetBeacon = false;
        }
    }
}
