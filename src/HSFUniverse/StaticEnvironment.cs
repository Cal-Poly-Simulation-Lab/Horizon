using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSFUniverse
{
    internal class StaticEnvironment: Domain
    {
        public StaticEnvironment() { }

        public override T GetObject<T>(string s)
        {
            return (T)(new object());
        }
    }
}
