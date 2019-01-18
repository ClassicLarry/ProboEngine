using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProboEngine_Stand_Alone_Version
{
	public class IDWithList
    {
        public int ID { get; set; }
        public List<int> IDList { get; set; }
        public IDWithList(int myID, List<int> myIDList)
        {
            ID = myID;
            IDList = myIDList;
        }
    }
}
