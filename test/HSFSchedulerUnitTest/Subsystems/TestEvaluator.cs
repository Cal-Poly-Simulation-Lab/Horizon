// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using HSFSystem;
using System.Collections.Generic;
using Utilities;
using Newtonsoft.Json.Linq;

namespace HSFScheduler
{
    public class TestEvaluator : Evaluator
    {
        public List<StateVariableKey<int>> Ikeys { get; set; } = new List<StateVariableKey<int>>();
        /// <summary>
        /// Abstract class that analyzes the given schedule and assigns a value to it.
        /// </summary>
        /// <param name="schedule"></param>
        /// <returns></returns>
        public override double Evaluate(SystemSchedule schedule)
        {
            double value = schedule.ScheduleValue + schedule.AllStates.Events.Peek().Tasks.Values.Sum(task => task.Target.Value);
            return value;
        }


    }
}
