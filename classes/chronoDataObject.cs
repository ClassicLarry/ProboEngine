using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class chronoDataObject
    {
        public List<double> timeChronoAvailable { get; set; }
        public List<double> chronoOnEco { get; set; }
        public List<double> chronoOnWG { get; set; }
        public List<double> chronoOnChainEnds { get; set; }
        public chronoDataObject(List<double> myTimeChronoAvailable, List<double> myNumChronoOnEco, List<double> myNumChronoOnWG, List<double> myNumChronoOnChainEnds)
        {
            timeChronoAvailable = myTimeChronoAvailable;
            chronoOnEco = myNumChronoOnEco;
            chronoOnWG = myNumChronoOnWG;
            chronoOnChainEnds = myNumChronoOnChainEnds;
        }
    }
}
