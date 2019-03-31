using System.Collections.Generic;
using UnityEngine;
using System.Threading;

namespace oti.AI
{
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
        /// The octree. Access should be restricted to this class. Using int to ID GameObjects outside main thread.
        /// </summary>        
        public PointOctree<int> PointOctree;

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
        public Dictionary<int, List<int>> MasterList = new Dictionary<int, List<int>>();

        /// <summary>
        /// Positions of tracked objects.
        /// </summary>
        private Vector3[] updatePositions;

        /// <summary>
        /// Generate Nition PointOctree
        /// </summary>
        public void Initialize(int initialWorldSize, Vector3 initialPosition, int smallestObjectSize)
        {
            PointOctree = new PointOctree<int>(initialWorldSize, initialPosition, smallestObjectSize);
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

            ThreadRunning = true;

            IsDone = false;
            base.StartOctree(otp);
        }

        protected override void ThreadOctree(OctreeThreadParameters otp)
        {
            TrackedObjectStates = evaluateOctree(otp);
            IsDone = true; // TrackedObjectStates are defined - - eliminating race conditions
        }

        /// <summary>
        /// Non essential method used to monitor threading
        /// </summary>
        protected override void OctreeThreadFinished()
        {
            ThreadRunning = false; 
        }

        /// <summary>
        /// Runs current tracked object data through Octree.
        /// </summary>
        private TrackedObjectStates evaluateOctree(OctreeThreadParameters otp)
        {
            int alloc = WorldMonitor.Instance.AllocationSpace;
            updatePositions = new Vector3[alloc]; // otp.ObjectIDs.Count
            float[] thresholds = new float[alloc]; // otp.ObjectIDs.Count

            //for (int i = 0; i < otp.ObjectIDs.Count; i++)
            foreach (int id in otp.ObjectIDs)
            {
                KeyValuePair<float, Vector3> pos;

                if (!otp.Coordinates.TryGetValue(id, out pos))
                    Debug.LogError("unknown object position request in octree eval");

                thresholds[id] = pos.Key;
                updatePositions[id] = pos.Value;

                PointOctree.Remove(id);
                PointOctree.Add(id, pos.Value);
            }

            List<int[]> enteringIDs = new List<int[]>();
            List<int[]> leavingIDs = new List<int[]>();

            List<int> parentIDLeavers = new List<int>();
            List<int> parentIDEnterers = new List<int>();

            int ind = 0;
            foreach(int id in otp.ObjectIDs)
            {
                List<int> validConflicts = new List<int>();
                List<int> stayers = new List<int>();

                int[] areaObjects = PointOctree.GetNearby(updatePositions[id], thresholds[id]);

                for (int j = 0; j < areaObjects.Length; j++)
                {
                    string affiliateObj = WorldMonitor.Instance.TrackedObjectAffiliations[id];
                    string affiliateCompare = WorldMonitor.Instance.TrackedObjectAffiliations[areaObjects[j]];

                    /*
                     * run conflict validity checks: if not the same object && not an object of the same class type && is a new conflict
                     */

                    if (areaObjects[j] != id && !MasterList[id].Contains(areaObjects[j]) && string.Compare(affiliateObj, affiliateCompare) != 0)
                    {
                        if (!parentIDEnterers.Contains(id))
                            parentIDEnterers.Add(id);

                        MasterList[id].Add(areaObjects[j]); // add conflicting object to master list of current conflicts
                        validConflicts.Add(areaObjects[j]); // *new* conflicts
                        stayers.Add(areaObjects[j]); // use to look for conflicts that have ended
                    }
                    else if (MasterList[id].Contains(areaObjects[j]))
                    {
                        stayers.Add(areaObjects[j]); // this is an object staying in conflict
                    }
                }

                bool leaverDetected = false;
                List<int> leavers = new List<int>();

                foreach (int _id in MasterList[id]) // look at master list's record of conflicts for this parent ID - if it isn't in stayers, it has left the conflict area or been destroyed
                {
                    if (!stayers.Contains(_id))
                    {
                        leaverDetected = true;
                        leavers.Add(_id);
                    }

                    switch (WorldMonitor.Instance.ConflictEndMode)
                    {
                        case ConflictEndMode.OnAllConflictsEnded:
                            if (leavers.Count == MasterList[id].Count)
                                parentIDLeavers.Add(id);
                            break;
                        case ConflictEndMode.OnIndividualConflictEnded:
                            if (leaverDetected && !parentIDEnterers.Contains(id) && !parentIDLeavers.Contains(id))
                                parentIDLeavers.Add(id);
                            break;
                    }
                }

                foreach (int leaver in leavers)
                    MasterList[id].Remove(leaver);

                int numValid = leavers.ToArray().Length;

                if (numValid > 0)
                    leavingIDs.Add(leavers.ToArray());

                numValid = validConflicts.ToArray().Length;

                if (numValid > 0)
                    enteringIDs.Add(validConflicts.ToArray());

                ind++;
            }

            return new TrackedObjectStates
            {
                ParentIDEnterers = parentIDEnterers.ToArray(), // parent IDs of new or increased conflict states
                ParentIDLeavers = parentIDLeavers.ToArray(), // parent IDs of expired or reduced conflict states
                LeavingIDs = leavingIDs, // child IDs - ended conflict(s)
                EnteringIDs = enteringIDs, // child IDs - the conflict(s)
                PriorConflictingIDs = parentIDLeavers // parent IDs that need informed all conflicts have ceased
            };
        }
    }

    /// <summary>
    /// A reference of states for tracked objects
    /// </summary>
    public class TrackedObjectStates
    {        
        public int[] ParentIDEnterers;
        public int[] ParentIDLeavers;
        public List<int[]> EnteringIDs;
        public List<int[]> LeavingIDs;
        public List<int> PriorConflictingIDs;
    }
}
