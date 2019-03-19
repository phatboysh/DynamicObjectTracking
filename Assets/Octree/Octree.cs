using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace oti.AI
{
    public struct TrackedObjectStates
    {
        public Dictionary<int, string[]> NewOrChangedStates;
        public List<int[]> ConflictingIDs;
        public List<int> PriorConflictingIDs;
    }

    /// <summary>
    /// OTI AI access point for tracked objects calculations run on auxilliary thread.
    /// </summary>
    public class Octree : OTIJob
    {
        /*
          Using Nition Octree https://github.com/Nition/UnityOctree       
          See license in Assets/License/UnityOctree
         */

        /// <summary>
        /// The octree. Access should be restricted to this class.
        /// </summary>        
        private PointOctree<int> pointOctree;

        /// <summary>
        /// True entire time Octree thread is running
        /// </summary>
        public bool ThreadRunning;

        /// <summary>
        /// Do not access this cache of object states as it is volatile, instead reference WorldMonitor's copy
        /// </summary>
        public TrackedObjectStates TrackedObjectStates;

        /// <summary>
        /// Record of every tracked object's current state of conflict and which categories it's in conflict with
        /// </summary>
        public Dictionary<int, string[]> MasterList = new Dictionary<int, string[]>();

        /// <summary>
        /// Positions of tracked objects.
        /// </summary>
        private Vector3[] updatePositions;

        //thread management
        public Thread IntendedAltThread;
        public Thread MainCheck;

        //thread management
        private int threadsStarted;
        private int threadsFinished;

        /// <summary>
        /// Generate Nition PointOctree
        /// </summary>
        public void Initialize(int initialWorldSize, Vector3 initialPosition, int smallestObjectSize)
        {
            pointOctree = new PointOctree<int>(initialWorldSize, initialPosition, smallestObjectSize);
        }
        
        /// <summary>
        /// Call this method to begin the threaded Octree operations.
        /// </summary>
        /// <param name="otp"<see cref="OctreeThreadParameters"/></param>
        public void ThreadOctreeInit(OctreeThreadParameters otp, bool restrictToMainThread)
        {
            if(restrictToMainThread)
            {
                TrackedObjectStates = evaluateOctree(otp);
                return;
            }

            if (threadsFinished != threadsStarted)
                Debug.LogWarning("Octree may be running on more than one thread simultaneously. finished: " + threadsFinished + ", started: " + threadsStarted);

            threadsStarted++;

            MainCheck = System.Threading.Thread.CurrentThread;
            ThreadRunning = true;

            IsDone = false;
            base.StartOctree(otp);
        }

        protected override void ThreadOctree(OctreeThreadParameters otp)
        {
            TrackedObjectStates = evaluateOctree(otp);
            IsDone = true; // TrackedObjectStates are defined - - eliminating race conditions
            
            IntendedAltThread = System.Threading.Thread.CurrentThread;
        }

        /// <summary>
        /// Non essential method used to monitor threading
        /// </summary>
        protected override void OctreeThreadFinished()
        {
            threadsFinished++;
            ThreadRunning = false;

            bool threadsEqual = Main == MainCheck;

            if (Main != System.Threading.Thread.CurrentThread)            
                Debug.LogError("unexpected thread execution - Octree results reported on background thread! Main == MainCheck ? " + threadsEqual);                       
        }

        /// <summary>
        /// Runs current tracked object data through Octree.
        /// </summary>
        private TrackedObjectStates evaluateOctree(OctreeThreadParameters otp)
        {
            updatePositions = new Vector3[otp.ObjectIDs.Count];
            float[] thresholds = new float[otp.ObjectIDs.Count];

            for (int i = 0; i < otp.ObjectIDs.Count; i++)
            {
                KeyValuePair<float, Vector3> pos;
                int objectID = otp.ObjectIDs[i];

                if (!otp.Coordinates.TryGetValue(objectID, out pos))
                    Debug.LogError("unknown object position request in octree eval");

                thresholds[i] = pos.Key;
                updatePositions[i] = pos.Value;

                pointOctree.Remove(objectID);
                pointOctree.Add(objectID, pos.Value);
            }

            List<int[]> locals = new List<int[]>();
            for(int i = 0; i < otp.ObjectIDs.Count; i++)
            {
                locals.Add(pointOctree.GetNearby(updatePositions[i], thresholds[i]));
            }         
                        
            Dictionary<int, string[]> conflicts = new Dictionary<int, string[]>();
            for(int i = 0; i < locals.Count; i++)
            {
                string localAffiliation;
                string ownerAffiliation;
                List<string> localConflicts = new List<string>();

                for (int j = 0; j < locals[i].Length; j++)
                {
                    #region Find Conflicts
                    if (otp.DynamicObjects.TryGetValue(locals[i][j], out localAffiliation) && otp.DynamicObjects.TryGetValue(otp.ObjectIDs[i], out ownerAffiliation))
                    {
                        if (ownerAffiliation != localAffiliation && locals[i].Length > 0 && !localConflicts.Contains(localAffiliation))
                        {
                            localConflicts.Add(localAffiliation);

                            if (conflicts.ContainsKey(otp.ObjectIDs[i]))
                            {
                                conflicts[otp.ObjectIDs[i]] = localConflicts.ToArray();
                            }
                            else
                            {
                                conflicts.Add(otp.ObjectIDs[i], localConflicts.ToArray());
                            }
                        }
                    }
                    #endregion
                }
            }

            return evaluateConflictResults(MasterList, conflicts, locals);
        }

        /// <summary>
        /// Interprets results to update MasterList and find expired conflicts.
        /// </summary>
        /// <param name="lastUpdate"> MasterList as of last Octree update </param>
        /// <param name="results"> Conflict states returned from this iteration </param>
        private TrackedObjectStates evaluateConflictResults(Dictionary<int, string[]> lastUpdate, Dictionary<int, string[]> results, List<int[]> conflictingIDs)
        {
            List<int> oldConflicts = new List<int>();
            
            for(int i = 0; i < lastUpdate.Count; i++)
            {
                if (MasterList[i].Length != 0)
                {
                    if (!results.ContainsKey(i)) // since key is not in results but is in Master, this conflict has ended
                    {
                        MasterList[i] = new string[0];
                        oldConflicts.Add(i);
                    }
                }
            }

            Dictionary<int, string[]> newOrStateChangedConflicts = new Dictionary<int, string[]>();

            foreach (KeyValuePair<int, string[]> currentConflict in results)
            {
                string[] oldState, conflictState;
                int key = currentConflict.Key;

                if (!lastUpdate.TryGetValue(key, out oldState)) //an object has been added to the tracking list or Octree initiating
                {
                    lastUpdate.Add(key, results[key]);
                    oldState = results[key];
                }

                if (!results.TryGetValue(key, out conflictState))
                    Debug.LogError("Undefined state for key (current)");

                if (oldState.Length > 0 && conflictState.Length > 0)
                {
                    bool changeDetected = false;
                    if (oldState.Length != conflictState.Length)
                    {
                        changeDetected = true;
                    }
                    else
                    {                        
                        for (int i = 0; i < oldState.Length; i++)
                        {
                            if (string.Compare(oldState[i], conflictState[i]) == -1)
                            {
                                changeDetected = true;
                                i = oldState.Length;
                            }
                        }
                    }
                    
                    if (changeDetected) // else the master list is currently correct
                    {
                        newOrStateChangedConflicts.Add(key, conflictState);
                        MasterList[key] = conflictState;
                    }   
                }
                else
                {
                    newOrStateChangedConflicts.Add(key, conflictState);
                    MasterList[key] = conflictState;
                }
            }

            return new TrackedObjectStates
            {
                NewOrChangedStates = newOrStateChangedConflicts,
                ConflictingIDs = conflictingIDs,
                PriorConflictingIDs = oldConflicts
            };
        }
    }
}
