using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class effficientBankReport
    {
        public List<double> mineralBankReport { get; set; }
        public List<double> gasBankReport { get; set; }
        public effficientBankReport(List<double> myMineralBankReport, List<double> myGasBankReport)
        {
            mineralBankReport = myMineralBankReport;
            gasBankReport = myGasBankReport;
        }
    }
}
