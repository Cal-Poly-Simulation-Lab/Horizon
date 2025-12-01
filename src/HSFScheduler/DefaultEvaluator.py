# Copyright (c) 2025 California Polytechnic State University
# Authors: Jason Ebeals (jebeals@calpoly.edu)

import clr
import System
clr.AddReference('System.Core')
clr.AddReference('IronPython')
clr.AddReference('System.Xml')
clr.AddReferenceByName('HSFScheduler')
clr.AddReferenceByName('MissionElements')
clr.AddReferenceByName('HSFSystem')

import HSFSystem
import MissionElements
import HSFScheduler
from HSFSystem import *
from MissionElements import *
from HSFScheduler import *
from System.Collections.Generic import Dictionary

class DefaultEvaluator(HSFScheduler.Evaluator):
    def __init__(self, evaluatorJson, keychain):
        # DefaultEvaluator doesn't need any parameters
        pass
    
    def Evaluate(self, schedule):
        # Sum task target values (same as DefaultEvaluator.cs)
        value = 0.0
        for eit in schedule.AllStates.Events:
            for task in eit.Tasks.Values:
                value += task.Target.Value
        return value

