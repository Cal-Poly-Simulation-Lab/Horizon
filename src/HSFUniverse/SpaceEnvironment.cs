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
    public class SpaceEnvironment : Domain
    {
        #region Attributes
        public Sun Sun { get; private set; }
        public Atmosphere Atmos { get; private set; }
        #endregion

        #region Constructors
        public SpaceEnvironment()
        {
            CreateUniverse();
        }

        public SpaceEnvironment(JObject environmentJson)
        {
            CreateUniverse(environmentJson);
        }
        #endregion

        #region Methods
        protected override void CreateUniverse()
        {
            Sun = new Sun(false);
            Atmos = new StandardAtmosphere();
        }
        protected override void CreateUniverse(JObject environmentJson)
        {
            StringComparison stringCompare = StringComparison.CurrentCultureIgnoreCase;

            if (environmentJson.GetValue("Sun", stringCompare) != null)
            {
                // Create the Sun based on the XMLNode                
                JObject sunJson = (JObject)environmentJson.GetValue("Sun", stringCompare);
                // Check the Sun XMLNode for the attribute
                if (sunJson.GetValue("isSunVectConstant", stringCompare) != null)
                {
                    bool sunVectConst = Convert.ToBoolean(sunJson.GetValue("isSunVectConstant", stringCompare));
                    Sun = new Sun(sunVectConst);
                }
                else
                {
                    Sun = new Sun();
                }
            }
            else
            {
                Sun = new Sun();
            }
            if (environmentJson.GetValue("Atmosphere", stringCompare) != null)
            {
                // Create the Sun based on the XMLNode                
                JObject atmosNode = (JObject)environmentJson.GetValue("Atmosphere", stringCompare);
                // Check the Sun XMLNode for the attribute
                string s = Convert.ToString(atmosNode.GetValue("type", stringCompare));
                switch (s)
                {
                    case "StandardAtmosphere":
                        Atmos = new StandardAtmosphere();
                        break;
                    case "RealTimeAtmosphere":
                        Atmos = new RealTimeAtmosphere();
                        break;
                }
            }
            else
            {
                Atmos = new StandardAtmosphere();
            }
        }

        /// <summary>
        /// Returns object specified by string.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="s"></param>
        /// <returns></returns>
        public override T GetObject<T>(string s)
        {
            switch (s.ToLower())
            {
                case "sun":
                    return (T)(object)(Sun);
                case "atmos":
                    return (T)(object)(Atmos);
            }
            return (T)(object)-1;
        }
        #endregion
    }
}