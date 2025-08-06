// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Utilities;
using HSFUniverse;
using MissionElements;
using UserModel;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace HSFSystem
{
    //[ExcludeFromCodeCoverage]
    public class Power : Subsystem
    {
        #region Attributes
        // Some Default Values
        protected double _batterySize;
        protected double _fullSolarPanelPower;
        protected double _penumbraSolarPanelPower;

        protected StateVariableKey<double> DOD_KEY;
        protected StateVariableKey<double> POWIN_KEY;
        #endregion Attributes

        #region Constructors
        public Power(JObject PowerJson, Asset asset) : base(PowerJson, asset)
        {
            this.GetParameterByName<double>(PowerJson, nameof(_batterySize), out _batterySize);
            this.GetParameterByName<double>(PowerJson, nameof(_fullSolarPanelPower), out _fullSolarPanelPower);
            this.GetParameterByName<double>(PowerJson, nameof(_penumbraSolarPanelPower), out _penumbraSolarPanelPower);
        }
        #endregion Constructors

        #region Methods

        public override void SetStateVariableKey(dynamic stateKey)
        {
            if (stateKey.VariableName.Equals(Asset.Name + ".depthofdischarge"))
                this.DOD_KEY = stateKey;
            else if (stateKey.VariableName.Equals(Asset.Name + ".solarpanelpowerin"))
                this.POWIN_KEY = stateKey;
            else
                throw new ArgumentException("Attempting to set unknown Power state variable key.", stateKey.VariableName);

        }
        /// <summary>
        /// Calculate the solar panel power in depending on position
        /// </summary>
        /// <param name="shadow"></param>
        /// <returns></returns>
        protected double GetSolarPanelPower(ShadowState shadow)
        {
            switch (shadow)
            {
                case ShadowState.UMBRA:
                    return 0;
                case ShadowState.PENUMBRA:
                    return _penumbraSolarPanelPower;
                default:
                    return _fullSolarPanelPower;
            }
        }

        /// <summary>
        /// Calculate the solar panel power in over the time of the task
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="state"></param>
        /// <param name="position"></param>
        /// <param name="universe"></param>
        /// <returns></returns>
        protected HSFProfile<double> CalcSolarPanelPowerProfile(double start, double end, SystemState state, DynamicState position, Domain universe)
        {
            Sun sun = universe.GetObject<Sun>("SUN");
            // create solar panel profile for this event
            double freq = 5;
            ShadowState lastShadow = sun.castShadowOnPos(position, start);
            HSFProfile<double> solarPanelPowerProfile = new HSFProfile<double>(start, GetSolarPanelPower(lastShadow));

            for (double time = start + freq; time <= end; time += freq)
            {
                ShadowState shadow = sun.castShadowOnPos(position, time);
                // if the shadow state changes during this step, save the power data
                if (shadow != lastShadow)
                {
                    solarPanelPowerProfile[time] = GetSolarPanelPower(shadow);
                    lastShadow = shadow;
                }
            }
            state.AddValues(POWIN_KEY, solarPanelPowerProfile);
            return solarPanelPowerProfile;
        }

        /// <summary>
        /// Override of the canPerform method for the power subsystem
        /// </summary>
        /// <param name="oldState"></param>
        /// <param name="newState"></param>
        /// <param name="tasks"></param>
        /// <param name="universe"></param>
        /// <returns></returns>
        public override bool CanPerform(Event proposedEvent, Domain universe)
        {
            double es = proposedEvent.GetEventStart(Asset);
            double te = proposedEvent.GetTaskEnd(Asset);
            double ee = proposedEvent.GetEventEnd(Asset);
            double powerSubPowerOut = 10;

            if (ee > SimParameters.SimEndSeconds)
            {
                Console.WriteLine("Simulation ended");
                return false;
            }

            // get the old DOD
            double olddod = NewState.GetLastValue(DOD_KEY).Item2;

            // collect power profile out
            Delegate DepCollector;
            SubsystemDependencyFunctions.TryGetValue("DepCollector", out DepCollector);
            HSFProfile<double> powerOut = (HSFProfile<double>)DepCollector.DynamicInvoke(proposedEvent); // deps->callDoubleDependency("POWERSUB_getPowerProfile");
            powerOut = powerOut + powerSubPowerOut;
            // collect power profile in
            DynamicState position = Asset.AssetDynamicState;
            HSFProfile<double> powerIn = CalcSolarPanelPowerProfile(es, te, NewState, position, universe);
            // calculate dod rate
            HSFProfile<double> dodrateofchange = ((powerOut - powerIn) / _batterySize);

            bool exceeded = false;
            double freq = 1.0;
            HSFProfile<double> dodProf = dodrateofchange.lowerLimitIntegrateToProf(es, te, freq, 0.0, ref exceeded, 0, olddod);
            //why is exceeded not checked anywhere??

            NewState.AddValues(DOD_KEY, dodProf);
            return true;
        }
        #endregion Methods
    }

}
