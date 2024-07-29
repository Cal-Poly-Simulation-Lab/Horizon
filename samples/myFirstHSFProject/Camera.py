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
from IronPython.Compiler import CallTarget0

class Camera(HSFSystem.Subsystem):


    def CanPerform(self, event, universe):
        
        ts = event.GetTaskStart(self.Asset)
      
        position = self.Asset.AssetDynamicState
        scPositionECI = position.PositionECI(ts)
        targetPositionECI = event.GetAssetTask(self.Asset).Target.DynamicState.PositionECI(ts)
        pointingVectorECI = targetPositionECI - scPositionECI
        
        event.State.AddValue(self.pointingvector, ts, pointingVectorECI)
        event.SetTaskStart(self.Asset, ts)
        event.SetTaskEnd(self.Asset, ts + self.imageCaptureTime)
      
        return True
    
    def CanExtend(self, event, universe, extendTo):
        return super(Camera, self).CanExtend(event, universe, extendTo)
      
    def GetDependencyCollector(self):
        return Func[Event,  Utilities.HSFProfile[System.Double]](self.DependencyCollector)
  
    def DependencyCollector(self, currentEvent):
        return super(Camera, self).DependencyCollector(currentEvent)
      
        
