using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class MaxTimeObject
    {
        public double maxTime { get; set; }
        public string buildingName { get; set; }
        public MaxTimeObject(double myMaxTime, string myBuildingName)
        {
            maxTime = myMaxTime;
            buildingName = myBuildingName;
        }
    }
}
