// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Xml;
using Utilities;
using MissionElements;
using Newtonsoft.Json.Linq;
using UserModel;

namespace HSFSystem
{
    public class ConstraintFactory
    {
        public static Constraint GetConstraint(JObject constraintJson, List<Subsystem> subsystems, string assetName)
        {
            Subsystem constrainedSub = null;
            JsonLoader<string>.TryGetValue("subsystemName", constraintJson, out string constraintSubName);
            constrainedSub = subsystems.Find(s => s.Name == assetName + "." + constraintSubName.ToLower());
            if (constrainedSub == null)
                throw new ArgumentException("Missing Subsystem Name in Constraint");

            string type = constraintJson["state"]["type"].ToString().ToLower();
            if (type.Equals("int"))
                return new SingleConstraint<int>(constraintJson, constrainedSub);
            else if (type.Equals("double"))
                return new SingleConstraint<double>(constraintJson, constrainedSub);
            else if (type.Equals("bool"))
                return new SingleConstraint<bool>(constraintJson, constrainedSub);
            else if (type.Equals("Matrix"))
                return new SingleConstraint<Matrix<double>>(constraintJson, constrainedSub);
            else //TODO: Add functionality to create scripted constraints
                throw new NotSupportedException("Unsupported type of constraint!");
        }
    }
}
