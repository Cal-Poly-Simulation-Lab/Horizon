// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using Newtonsoft.Json.Linq;

namespace HSFUniverse{

    public abstract class Domain
    {
        protected virtual void CreateUniverse() { }
        protected virtual void CreateUniverse(JObject environmentJson) { }
        public virtual double GetAtmosphere(string s, double h) { return 0; }
        public virtual void SetObject<T>(string s, T val) { }
        public abstract T GetObject<T>(string s);
    }

}