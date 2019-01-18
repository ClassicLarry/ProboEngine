using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class buildingStatusTrackerObject
    {
        public int probeCount { get; set; }
        public int nexusCount { get; set; }
        public int assimilatorCount { get; set; }
        public int futureProbeCount { get; set; }
        public int futureNexusCount { get; set; }
        public int futureAssimilatorCount { get; set; }
        public int pylonCount { get; set; }

        public int freeNexusCount { get; set; }

        public int gatewayCount { get; set; }
        public int roboCount { get; set; }
        public int stargateCount { get; set; }
        public int freeGatewayCount { get; set; }
        public int freeRoboCount { get; set; }
        public int freeStargateCount { get; set; }
        public int warpGateCount { get; set; }
        public int freeWarpGateCount { get; set; }

        public buildingStatusTrackerObject(int myProbeCount, int myNexusCount, int myAssimilatorCount)
        {
            probeCount = myProbeCount;
            futureProbeCount = myProbeCount;
            nexusCount = myNexusCount;
            futureNexusCount = myNexusCount;
            freeNexusCount = myNexusCount;
            assimilatorCount = myAssimilatorCount;
            futureAssimilatorCount = myAssimilatorCount;


            pylonCount = 0;
            roboCount = 0;
            freeRoboCount = 0;
            stargateCount = 0;
            freeStargateCount = 0;
            gatewayCount = 0;
            freeGatewayCount = 0;
            warpGateCount = 0;
            freeWarpGateCount = 0;
        }
    }
}
