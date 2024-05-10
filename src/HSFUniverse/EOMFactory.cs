// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Utilities;


namespace HSFUniverse
{
    public class EOMFactory
    {
        public static DynamicEOMS GetEOMS(JToken eomsJson)
        {
            string eomsType = ((string)eomsJson["type"]).ToLower();
            if (eomsType == "scripted")
            {
                var eoms = (DynamicEOMS)(new ScriptedEOMS(eomsJson["EOMS"]));
                return eoms;
            }
            else if (eomsType.ToLower() == "orbitaleoms")
            {
                DynamicEOMS Eoms = new OrbitalEOMS();
                return Eoms;
            }
            else
            {
                return null;
            }
        }
    }
}
