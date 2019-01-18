using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace ProboEngine_Stand_Alone_Version
{
	public class trueRandom
    {
        public double randomVal { get; set; }
        public trueRandom()
        {
            using (RNGCryptoServiceProvider n = new RNGCryptoServiceProvider())
            {
                Random rand = new Random(n.GetHashCode());
                randomVal = rand.NextDouble();
            }
        }
    }
}
