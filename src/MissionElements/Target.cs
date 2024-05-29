// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using HSFUniverse;
using log4net;
using Newtonsoft.Json.Linq;
using UserModel;

namespace MissionElements
{
    public class Target
    {
        // The name of the target 
        public string Name { get; private set; }

        // The type of the target (will always be converted to a lowercase string in constructor
        public string Type { get; private set; }

        // The position of the target 
        public DynamicState DynamicState { get; private set; }

        // The value of the target 
        public int Value { get; private set; }

        // Logger for log file
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #region Constructors

        /// <summary>
        /// Standard constructor.  Creates a Target from JSON data.
        /// </summary>
        /// <param name="targetJson"></param>
        public Target(JObject targetJson)
        {
            string msg;

            if (JsonLoader<string>.TryGetValue("name", targetJson, out string name))
            {
                Name = name;
            }
            else
            {
                msg = $"Target loading error.  Targets must have a NAME.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentOutOfRangeException(msg);
            }
            if (JsonLoader<string>.TryGetValue("type", targetJson, out string type))
            {
                Type = type;
            }
            else
            {
                msg = $"Target loading error.  Targets must have a TYPE for target {Name}.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentOutOfRangeException(msg);
            }
            if (JsonLoader<string>.TryGetValue("value", targetJson, out int value))
            {
                Value = value;
            }
            else
            {
                msg = $"Target loading error.  Targets must have a VALUE for target {Name}.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentOutOfRangeException(msg);
            }
            if (JsonLoader<JObject>.TryGetValue("dynamicState", targetJson, out JObject dynamicStateJson))
            {
                DynamicState = new DynamicState(dynamicStateJson);
            }
            else
            {
                msg = $"Target loading error.  Targets must have a DYNAMICS STATE for target {Name}.";
                log.Fatal(msg);
                Console.WriteLine(msg);
                throw new ArgumentOutOfRangeException(msg);
            }
        }

        #endregion

        /// <summary>
        /// Override of the Object ToString Method
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }
    }

    // This was changed to a string type instead of an enum to support custom taskTypes on 1/31/2022
    // Types of targets that HSF supports
    // public enum TargetType { FacilityTarget, LocationTarget, FlyingAlong, Recovery }
}
