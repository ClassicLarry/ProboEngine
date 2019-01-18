using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class coefficientPackage
    {
        public double[] prVals { get; set; }
        public double[] nVals { get; set; }
        public double[] eVals { get; set; }
        public double[] aVals { get; set; }
        public double[] pyVals { get; set; }

        public coefficientPackage(double[] myprVals, double[] mynVals, double[] myeVals, double[] myaVals, double[] mypyVals)
        {
            prVals = myprVals;
            nVals = mynVals;
            eVals = myeVals;
            aVals = myaVals;
            pyVals = mypyVals;
        }
    }
}
