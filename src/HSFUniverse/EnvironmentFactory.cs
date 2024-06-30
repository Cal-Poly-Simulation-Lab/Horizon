
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UserModel;

namespace HSFUniverse
{
    public class EnvironmentFactory
    {
        /// <summary>
        /// A method to interpret the Xml file and create a universe instance
        /// </summary>
        /// <param name="modelXmlNodel"></param>
        /// <returns></returns>
        public static Domain GetUniverseClass(JObject environmentJson)
        {
            Domain environment;
            if (JsonLoader<string>.TryGetValue("type", environmentJson, out string type))
            {
                type = type.ToLower();

                if (type.Equals("scripted"))
                {
                    environment = (Domain)new ScriptedEnvironment(environmentJson);
                }
                else if (type.Equals("spaceenvironment"))
                {
                    environment = (Domain)new SpaceEnvironment(environmentJson);
                }
                else if (type.Equals("airborneenvironment"))
                {
                    throw new NotImplementedException("Airborne Environment needs to be implemented!");
                }
                else
                {
                    throw new ArgumentOutOfRangeException($"Evironment is not set to a HSF Environment type, type {type} was not found.");
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException($"Evironment must contain a TYPE.");
            }

            return environment;
        }
    }
}
