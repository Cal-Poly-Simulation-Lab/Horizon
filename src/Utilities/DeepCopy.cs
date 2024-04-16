// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json; //So maybe use NewtonSoft instead. 

namespace Utilities
{
    public class DeepCopy
    {
        //Make a deep copy of any class as long as it is marked as [Serializable]
        //http://www.codeproject.com/Articles/28952/Shallow-Copy-vs-Deep-Copy-in-NET
        public static T Copy<T>(T item)
        {
            // JsonSerializerOptions securely replaces obselete, insecure "BinaryFormatter" used by legacy HSF. 
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
                // Other Options as necessary
            };
            string jsonString = JsonSerializer.Serialize(item, options);
            T result = JsonSerializer.Deserialize<T>(jsonString, options);
            return result;

        }
    }
}
