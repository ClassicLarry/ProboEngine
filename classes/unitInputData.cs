using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class unitInputData
    {

        //tech buildings required for units
        public buildingsNeededClass buildingsNeeded { get; set; }
        //gateway units
        public int numZealots { get; set; }
        public int numStalkers { get; set;}
        public int numSentries { get; set; }
        public int numAdepts { get; set; }
        public int numHT { get; set; }
        public int numDT { get; set; }

        //robo units
        public int numImmortals { get; set; }
        public int numObservers { get; set; }
        public int numCollosi { get; set; }
        public int numDisruptors { get; set; }
        public int numPrisms { get; set; }

        //stargate units
        public int numOracles { get; set; }
        public int numVoidrays { get; set; }
        public int numCarriers { get; set; }
        public int numTempests { get; set; }
        public int numPhoenix { get; set; }

        public unitInputData(int myNumZealots,int myNumStalkers, int myNumSentries, int myNumAdepts, int myNumHT, int myNumDT, int myNumImmortals, int myNumObservers, int myNumCollosi, int myNumDisruptors, int myNumPrisms, int myNumOracles, int myNumVoidrays, int myNumCarriers, int myNumTempests, int myNumPhoenix)
        {
            numZealots = myNumZealots;
            numStalkers = myNumStalkers;
            numSentries = myNumSentries;
            numAdepts = myNumAdepts;
            numHT = myNumHT;
            numDT = myNumDT;
            numImmortals = myNumImmortals;
            numObservers = myNumObservers;
            numCollosi = myNumCollosi;
            numDisruptors = myNumDisruptors;
            numPrisms = myNumPrisms;
            numOracles = myNumOracles;
            numVoidrays = myNumVoidrays;
            numCarriers = myNumCarriers;
            numTempests = myNumTempests;
            numPhoenix = myNumPhoenix;
        }
    }
}
