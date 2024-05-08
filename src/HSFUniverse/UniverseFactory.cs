
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HSFUniverse
{
    public class UniverseFactory
    {
        /// <summary>
        /// A method to interpret the Xml file and create a universe instance
        /// </summary>
        /// <param name="modelXmlNodel"></param>
        /// <returns></returns>
        public static Domain GetUniverseClass(JObject environmentJson)
        {
            Domain universe;
            if (JsonLoader<string>.TryGetValue("type", environmentJson, out string type))
            {
                type = type.ToLower();

                //string type = environmentJson.GetValue("type", stringCompare).ToString().ToLower();

                if (type.Equals("scripted"))
                {
                    universe = (Domain)new ScriptedUniverse(environmentJson);
                }
                else if (type.Equals("spaceenvironment"))
                {
                    universe = (Domain)new SpaceEnvironment(environmentJson);
                }
                else if (type.Equals("airborneenvironment"))
                {
                    throw new NotImplementedException("Airborne Environment needs to be implemented!");
                }
                else
                {
                    throw new ArgumentOutOfRangeException($"Evironment is not set to a HSF Environment type, type {type} was found.");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException($"Evironment must contain a TYPE.");
            }

            return universe;
        }
    }
}
