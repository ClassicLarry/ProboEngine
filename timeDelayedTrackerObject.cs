using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class timeDelayedTrackerObject
    {
        public double tierOne { get; set; }
        public double robo { get; set; }
        public double stargate { get; set; }
        public double twilightCouncil { get; set; }
        public double fleetBeacon { get; set; }
        public double templarArchives { get; set; }
        public double darkShrine { get; set; }
        public double supportBay { get; set; }
        public double warpGateResearch { get; set; }
        public double forge { get; set; }

        public timeDelayedTrackerObject()
        {
            tierOne = 0;
            robo = 0;
            stargate = 0;
            twilightCouncil = 0;
            fleetBeacon = 0;
            templarArchives = 0;
            darkShrine = 0;
            supportBay = 0;
            warpGateResearch = 0;
            forge = 0;
        }

    }
}
