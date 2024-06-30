// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UserModel;
using Utilities;


namespace HSFUniverse
{
    public class EOMFactory
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static DynamicEOMS GetEOMS(JObject eomsJson)
        {
            string msg;
            if (JsonLoader<string>.TryGetValue("type", eomsJson, out string eomsType))
                eomsType = eomsType.ToLower();
            else
            {
                msg = $"Missing a EOMS 'type' attribute for EOMS in {eomsJson}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }

            DynamicEOMS eoms;

            if (eomsType.Equals("scripted"))
            {
                eoms = (DynamicEOMS)(new ScriptedEOMS(eomsJson));
            }
            else if (eomsType.Equals("orbitaleoms"))
            {
                eoms = new OrbitalEOMS();
                eoms.Environment = new SpaceEnvironment();
            }
            else if (eomsType.Equals("orbitalperteoms"))
            {
                eoms = new OrbitalPertEOMS();
                eoms.Environment = new SpaceEnvironment();
            }
            else if (eomsType.Equals("static"))
            {
                eoms = new StaticEOMS();
                eoms.Environment = new StaticEnvironment();
            }
            else
            {
                msg = $"EOMS 'type' attribute not found for EOMS in {eomsJson}";
                Console.WriteLine(msg);
                log.Error(msg);
                throw new ArgumentOutOfRangeException(msg);
            }
            return eoms;
        }
    }
}
