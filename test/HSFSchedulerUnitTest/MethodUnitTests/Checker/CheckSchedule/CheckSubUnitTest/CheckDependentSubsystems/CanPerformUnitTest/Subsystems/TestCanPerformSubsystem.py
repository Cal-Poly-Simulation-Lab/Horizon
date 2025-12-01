# Copyright (c) 2025 California Polytechnic State University
# Authors: Jason Ebeals (jebeals@calpoly.edu)

import sys
import clr
import System.Collections.Generic
import System
clr.AddReference('System.Core')
clr.AddReference('IronPython')
clr.AddReferenceByName('Utilities')
clr.AddReferenceByName('HSFUniverse')
clr.AddReferenceByName('UserModel')
clr.AddReferenceByName('MissionElements')
clr.AddReferenceByName('HSFSystem')

import HSFSystem
import MissionElements
import Utilities
import HSFUniverse
import UserModel
from HSFSystem import *
from Utilities import *
from HSFUniverse import *
from UserModel import *
from MissionElements import *
from System import Func, Delegate
from System.Collections.Generic import Dictionary

class TestCanPerformSubsystem(HSFSystem.Subsystem):
    """
    Simple STATELESS test subsystem - tracks iteration count in SystemState
    Demonstrates proper stateless subsystem design for parallel execution
    """
    
    def CanPerform(self, event, universe):
        try:
            print(f"Python CanPerform: NewState = {self.NewState}, iteration_key = {self.iteration_key}")
            # Read current iteration from STATE via self.NewState (set by base class)
            currentIteration = self.NewState.GetLastValue(self.iteration_key).Item2
            print(f"Python: Read iteration = {currentIteration}")
            
            # Calculate new iteration
            newIteration = currentIteration + 1
            print(f"Python: New iteration = {newIteration}")
            
            # Write new value to STATE at task start time
            taskStart = event.GetTaskStart(self.Asset)
            print(f"Python: Task start = {taskStart}")
            prof = Utilities.HSFProfile[System.Int32](taskStart, newIteration)
            self.NewState.AddValues(self.iteration_key, prof)
            print(f"Python: Added value to state")
            
            # Return false if max reached (parameter set as attribute by loader)
            maxIter = self.maxIterations if hasattr(self, 'maxIterations') else 5
            result = (newIteration < maxIter)
            print(f"Python: Returning {result} (newIter={newIteration}, max={maxIter})")
            return result
        except Exception as e:
            print(f"Python CanPerform ERROR: {e}")
            raise
    
    def GetDependencyCollector(self):
        # Required by ScriptedSubsystem constructor
        return Func[Event, Utilities.HSFProfile[System.Double]](self.DependencyCollector)
    
    def DependencyCollector(self, currentEvent):
        # No dependencies for test subsystem, return empty profile
        return Utilities.HSFProfile[System.Double]()
