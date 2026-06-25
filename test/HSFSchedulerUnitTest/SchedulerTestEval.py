import sys
import clr
import System.Collections.Generic
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
import System.Xml
from System.Xml import XmlNode
from System.Collections.Generic import Dictionary
from IronPython.Compiler import CallTarget0


# Class definition - inherits from Evaluator (not TargetValueEvaluator)
class eval(HSFScheduler.Evaluator):
    def __init__(self, keychain):
        # Evaluator base class doesn't require constructor parameters
        self.keychain = keychain
        pass 

    # These are used to access state variable information
    def SelectKey(keychain, keyName):
        for i in range(len(keychain)):
            if keychain[i].VariableName == keyName:
                return keychain[i]

    # this jsut adds up all the target values
    def Evaluate(self, sched):
        sum = 0
        for eit in sched.AllStates.Events:
            for assetTask in eit.Tasks:
                task  = assetTask.Value
                asset = assetTask.Key
                sum += task.Target.Value
                
        return sum