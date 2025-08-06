// Copyright (c) 2016 California Polytechnic State University
// Authors: Morgan Yost (morgan.yost125@gmail.com) Eric A. Mehiel (emehiel@calpoly.edu)

using System;
using System.Collections.Generic;
using HSFUniverse;
using MissionElements;
using System.Xml;
using Utilities;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace HSFSystem
{
    //[ExcludeFromCodeCoverage]
    public class EOSensor : Subsystem
    {
        #region Attributes
        //Default Values
        public StateVariableKey<double> PIXELS_KEY;
        public StateVariableKey<double> INCIDENCE_KEY;
        public StateVariableKey<bool> EOON_KEY;
        protected double _lowQualityPixels;
        protected double _lowQualityTime;
        protected double _midQualityPixels;
        protected double _midQualityTime;
        protected double _highQualityPixels;
        protected double _highQualityTime;
        #endregion

        #region Constructors
        public EOSensor(JObject eoSensorJson, Asset asset):base(eoSensorJson, asset)
        {
            this.GetParameterByName<double>(eoSensorJson, nameof(_lowQualityPixels), out _lowQualityPixels);
            this.GetParameterByName<double>(eoSensorJson, nameof(_lowQualityTime), out _lowQualityTime);
            this.GetParameterByName<double>(eoSensorJson, nameof(_midQualityPixels), out _midQualityPixels);
            this.GetParameterByName<double>(eoSensorJson, nameof(_midQualityTime), out _midQualityTime);
            this.GetParameterByName<double>(eoSensorJson, nameof(_highQualityPixels), out _highQualityPixels);
            this.GetParameterByName<double>(eoSensorJson, nameof(_highQualityTime), out _highQualityTime);
        }
        
        public override void SetStateVariableKey(dynamic stateKey)
        {
            if (stateKey.VariableName.Equals(Asset.Name + ".incidenceangle"))
                this.INCIDENCE_KEY = stateKey;
            else if (stateKey.VariableName.Equals(Asset.Name + ".numpixels"))
                this.PIXELS_KEY = stateKey;
            else if (stateKey.VariableName.Equals(Asset.Name + ".eosensoron"))
                this.EOON_KEY = stateKey;
            else
                throw new ArgumentException("Attempting to set unknown EOSensor state variable key.", stateKey.VariableName);
        }
        #endregion

        #region Methods
        /// <summary>
        /// An override of the Subsystem CanPerform method
        /// </summary>
        /// <param name="proposedEvent"></param>
        /// <param name="environment"></param>
        /// <returns></returns>
        public override bool CanPerform(Event proposedEvent, Domain environment)
        {
            if (Task.Type == "imaging")
            {
                //set pixels and time to caputre based on target value
                int value = Task.Target.Value;
                double pixels = _lowQualityPixels;
                double timetocapture = _lowQualityTime;
                if (value <= _highQualityTime && value >= _midQualityTime) //Morgan took out magic numbers
                {
                    pixels = _midQualityPixels;
                    timetocapture = _midQualityTime;
                }
                if (value > _highQualityTime)
                {
                    pixels = _highQualityPixels;
                    timetocapture = _highQualityTime;
                }

                // get event start and task start times
                double es = proposedEvent.GetEventStart(Asset);
                double ts = proposedEvent.GetTaskStart(Asset);
                double te = proposedEvent.GetTaskEnd(Asset);
                if (ts > te)
                {
                    // TODO: Change this to Logger
                    Console.WriteLine("EOSensor lost access");
                    return false;
                }

                // set task end based upon time to capture
                te = ts + timetocapture;
                proposedEvent.SetTaskEnd(Asset, te);

                // calculate incidence angle
                // from Brown, Pp. 99
                DynamicState position = Asset.AssetDynamicState;
                double timage = ts + timetocapture / 2;
                Matrix<double> m_SC_pos_at_tf_ECI = position.PositionECI(timage);
                Matrix<double> m_target_pos_at_tf_ECI = Task.Target.DynamicState.PositionECI(timage);
                Matrix<double> m_pv = m_target_pos_at_tf_ECI - m_SC_pos_at_tf_ECI;
                Matrix<double> pos_norm = -m_SC_pos_at_tf_ECI / Matrix<double>.Norm(-m_SC_pos_at_tf_ECI);
                Matrix<double> pv_norm = m_pv / Matrix<double>.Norm(m_pv);

                double incidenceang = 90 - 180 / Math.PI * Math.Acos(Matrix<double>.Dot(pos_norm, pv_norm));

                // set state data
                NewState.AddValue(INCIDENCE_KEY, timage, incidenceang);
                NewState.AddValue(INCIDENCE_KEY, timage + 1, 0.0);

                NewState.AddValue(PIXELS_KEY, timage, pixels);
                NewState.AddValue(PIXELS_KEY, timage + 1, 0.0);

                NewState.AddValue(EOON_KEY, ts, true);
                NewState.AddValue(EOON_KEY, te, false);
            }
            return true;

        }

        /// <summary>
        /// Dependency Function for Power Subsystem
        /// </summary>
        /// <param name="currentEvent"></param>
        /// <returns></returns>
        public HSFProfile<double> Power_asset1_from_EOSensor_asset1(Event currentEvent)
        {
            HSFProfile<double> prof1 = new HSFProfile<double>();
            prof1[currentEvent.GetEventStart(Asset)] = 10;
            if (currentEvent.State.GetValueAtTime(EOON_KEY, currentEvent.GetTaskStart(Asset)).Value)
            {
                prof1[currentEvent.GetTaskStart(Asset)] = 60;
                prof1[currentEvent.GetTaskEnd(Asset)] = 10;
            }
            return prof1;
        }

        /// <summary>
        /// Dependecy function for the SSDR subsystem
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public HSFProfile<double> SSDR_asset1_from_EOSensor_asset1(Event currentEvent)
        {
            return currentEvent.State.GetProfile(PIXELS_KEY) / 500;
        }
        #endregion
    }
}
