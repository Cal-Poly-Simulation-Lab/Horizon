// Copyright (c) 2025 California Polytechnic State University
// Authors: Jason Ebeals (jebeals@calpoly.edu)

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace HSFSystem
{
    /// <summary>
    /// Shared static tracking for subsystem CanPerform calls (thread-safe).
    /// Used by both source and dynamically compiled subsystems.
    /// Tracks call order, asset name, subsystem name, task type, and mutation status.
    /// </summary>
    public static class SubsystemCallTracker
    {
        private static readonly ConcurrentBag<CallRecord> _callTracking = new ConcurrentBag<CallRecord>();
        private static long _callOrderCounter = 0; // Thread-safe counter for call order
        private static bool _enableConsoleOutput = false; // Default: muted (no console output)
        
        public class CallRecord
        {
            public long CallOrder { get; set; }
            public string AssetName { get; set; } = "";
            public string SubsystemName { get; set; } = "";
            public string TaskType { get; set; } = "";
            public bool Mutated { get; set; }
            
            public override string ToString()
            {
                string mutationStatus = Mutated ? "YES" : "NO";
                return $"[Order:{CallOrder}] {AssetName}.{SubsystemName}.CanPerform called for task: {TaskType} | State Mutation: {mutationStatus}";
            }
        }
        
        public static void Clear()
        {
            _callTracking.Clear();
            Interlocked.Exchange(ref _callOrderCounter, 0);
        }
        
        public static void Track(string assetName, string subsystemName, string taskType, bool mutated)
        {
            long callOrder = Interlocked.Increment(ref _callOrderCounter);
            var record = new CallRecord
            {
                CallOrder = callOrder,
                AssetName = assetName,
                SubsystemName = subsystemName,
                TaskType = taskType,
                Mutated = mutated
            };
            _callTracking.Add(record);
            
            // Only write to console if enabled (default: muted)
            if (_enableConsoleOutput)
            {
                System.Console.WriteLine(record.ToString());
            }
        }
        
        /// <summary>
        /// Enable or disable console output for tracked calls.
        /// Default is false (muted) to reduce log verbosity.
        /// </summary>
        public static void SetConsoleOutput(bool enable)
        {
            _enableConsoleOutput = enable;
        }
        
        /// <summary>
        /// Get current console output setting.
        /// </summary>
        public static bool IsConsoleOutputEnabled() => _enableConsoleOutput;
        
        public static List<CallRecord> GetTracking() => _callTracking.OrderBy(r => r.CallOrder).ToList();
        
        public static List<CallRecord> GetTrackingForSubsystem(string subsystemName) => 
            _callTracking.Where(r => r.SubsystemName.Equals(subsystemName, System.StringComparison.OrdinalIgnoreCase))
                        .OrderBy(r => r.CallOrder).ToList();
        
        public static List<CallRecord> GetTrackingForAsset(string assetName) => 
            _callTracking.Where(r => r.AssetName.Equals(assetName, System.StringComparison.OrdinalIgnoreCase))
                        .OrderBy(r => r.CallOrder).ToList();
    }
}

