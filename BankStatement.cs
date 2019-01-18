using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
    public class BankStatement
    {
        public double mineralBank { get; set; }
        public double gasBank { get; set; }

        public BankStatement(double myMineralBank, double myGasBank)
        {
            mineralBank = myMineralBank;
            gasBank = myGasBank;
        }
    }
}
