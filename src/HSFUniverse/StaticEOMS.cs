using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utilities;

namespace HSFUniverse
{
    /// <summary>
    /// Returns all zeros to the Runge-Kutta integrator.  
    /// Therefore, if the initial conditions of the Dyanmic State are integrated, 
    /// the result will be no change to the initial conditions.
    /// 
    /// These EOMs are used by all 'STATIC' Dynamic State Types
    /// </summary>
    public class StaticEOMS: DynamicEOMS
    {
        public StaticEOMS() { }

        public override Matrix<double> this[double t, Matrix<double> y, IntegratorParameters param, Domain environment]
        {
            get
            {
                return new Matrix<double>(y.NumRows, y.NumCols, 0.0);
            }
        }
    }
}
