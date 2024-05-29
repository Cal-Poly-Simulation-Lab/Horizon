

from itertools import permutations

class Access:

    def __init__(self):

        pass

    def generateAllCombos(self,AccessList):

        print("----- All Possible Combos ------")
        combos = []

        for i in range(0,len(AccessList)):
            perms = permutations(AccessList,i+1)
            combos.extend(perms)

        for c in combos:
            s = ""
            for a in c:
                s += a.Name
                s+=', '
            print(s)
            #print(c[0].Name)

        print("\n Total num schedules: " + str(len(combos)))
        return combos


    def generatePossibleCombos(self,combos,SimTime,StepTime):

        # Total SimTime
        T = SimTime
        s = StepTime

        potential = []
        # go through all combinations and find the possible ones
        for c in combos:
            t=c[0].tStart; v=0
            Possible = True
            for a in c:

                # if a.Name == "3":
                #     print(a.Name)

                # check if start time is valid
                if t < a.tStart:
                    # Do not add the combintaiton to the list of possible
                    Possible = False
                    break         

                # Add the task time
                t+=a.dt

                # check if schedule supercedes max sim time
                if t > T: # This means the time is at the end of the simulation
                    # Do not add the combintaiton to the list of possible
                    Possible = False
                    break
            
                # If pass, add the value
                v+=a.value

            # Add to the list if it is possible
            if Possible:
                potential.append((c,v))

        return potential
                




T = 60; # Simulation time
s = 0.2*T # Step Size (time)

start_times = [0, s, 2*s, 3*s]
end_times   = [T,T,T,T]
dts         = [2*s,4*s,2*s,s] 
values      = [3,5,4,3]

Accesses = []
for i in range(0,len(start_times)):
    a = Access()
    a.Name = str(i+1)
    a.tStart = start_times[i]
    a.tEnd   = end_times[i]
    a.dt     = dts[i]
    a.value  = values[i]

    Accesses.append(a)



possibleCombos = a.generateAllCombos(Accesses)
schedList = a.generatePossibleCombos(possibleCombos,T,s)

print("------- Valid Schedules -------")
for s in schedList:
    name = "'"
    for a in s[0]:
        name += a.Name; name+=', '
    name += "' "
    print(name + "val=" + str(s[1]))

print("Break")