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

class TaskTimeManipulatorSubsystem(HSFSystem.Subsystem):
    """
    Test subsystem that attempts to manipulate task start/end times
    Used to verify architectural constraints on time mutability
    """
    
    def CanPerform(self, event, universe):
        # Get current times
        currentTaskStart = event.GetTaskStart(self.Asset)
        currentTaskEnd = event.GetTaskEnd(self.Asset)
        currentEventStart = event.GetEventStart(self.Asset)
        currentEventEnd = event.GetEventEnd(self.Asset)
        
        # Get shift parameters (set by loader)
        taskStartShift = self.taskStartShift if hasattr(self, 'taskStartShift') else 0.0
        taskEndShift = self.taskEndShift if hasattr(self, 'taskEndShift') else 0.0
        eventStartShift = self.eventStartShift if hasattr(self, 'eventStartShift') else 0.0
        eventEndShift = self.eventEndShift if hasattr(self, 'eventEndShift') else 0.0
        
        # Modify task times
        taskStartDict = Dictionary[Asset, System.Double]()
        taskStartDict.Add(self.Asset, currentTaskStart + taskStartShift)
        event.SetTaskStart(taskStartDict)
        
        taskEndDict = Dictionary[Asset, System.Double]()
        taskEndDict.Add(self.Asset, currentTaskEnd + taskEndShift)
        event.SetTaskEnd(taskEndDict)
        
        # Modify event times
        eventStartDict = Dictionary[Asset, System.Double]()
        eventStartDict.Add(self.Asset, currentEventStart + eventStartShift)
        event.SetEventStart(eventStartDict)
        
        eventEndDict = Dictionary[Asset, System.Double]()
        eventEndDict.Add(self.Asset, currentEventEnd + eventEndShift)
        event.SetEventEnd(eventEndDict)
        
        return True  # Always succeeds
    
    def GetDependencyCollector(self):
        return Func[Event, Utilities.HSFProfile[System.Double]](self.DependencyCollector)
    
    def DependencyCollector(self, currentEvent):
        return Utilities.HSFProfile[System.Double]()

