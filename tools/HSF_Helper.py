import sys
import clr
import System.Collections.Generic
import System
clr.AddReference('System.Core')
clr.AddReference('IronPython')
clr.AddReference('System.Xml')
clr.AddReferenceByName('Utilities')
clr.AddReferenceByName('HSFUniverse')
clr.AddReferenceByName('UserModel')
clr.AddReferenceByName('MissionElements')
clr.AddReferenceByName('HSFSystem')

import System.Xml
import HSFSystem
import MissionElements
import Utilities
import HSFUniverse
import UserModel
from HSFSystem import *
from System.Xml import XmlNode
from Utilities import *
from HSFUniverse import *
from UserModel import *
from MissionElements import *
from System import Func, Delegate
from System.Collections.Generic import *
from IronPython.Compiler import CallTarget0

class HSFHelper():
    def __init__(self):
        pass

    def SetStateVariable(self, subsystem, variableName, key):
        setattr(subsystem, variableName, key)

    # This is the way HSF_Helper seems to not create argument errors within ScriptedSubsystem.cs > "this.SetStateVariable() method."
    # Not sure if this is the proper fix yet though. -JB 4/25/24
    # def SetStateVariable(self, variableName, key):
    #     setattr(self, variableName, key)