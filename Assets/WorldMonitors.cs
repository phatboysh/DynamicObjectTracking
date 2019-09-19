using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace oti.AI
{
    public class WorldMonitors : MonoBehaviour
    {
        /// <summary>
        /// User defined list of GameObject to track
        /// </summary>
        [HideInInspector] // hides in child classes only
        public List<TrackedObjectContainer> TrackedObjects = new List<TrackedObjectContainer>();

        /// <summary>
        /// User defined area for defining close objects
        /// </summary>
        [HideInInspector] // hides in child classes only
        public List<float> ThresholdSet = new List<float>();

        /// <summary>
        /// Delegate for tracked object conflicts.
        /// </summary>
        /// <param name="conflictingTypes">The types of objects as defined in WorldMonitors</param>
        /// <param name="conflictOrigins">The positions of the objects causing the conflict</param>
        public delegate void ObjectConflictHandler(GameObject objectWithConflict, GameObject[] conflictingObjects, string[] conflictingTypes);

        /// <summary>
        /// Event to raise awareness to listeners of the increase tracked object conflict
        /// </summary>
        public event ObjectConflictHandler ConflictEnterers;

        /// <summary>
        /// Event to raise awareness to listeners of the reduced tracked object conflict
        /// </summary>
        public event ObjectConflictHandler ConflictLeavers;

        /// <summary>
        /// Event to inform listeners all conflicting objects have ended
        /// </summary>
        public event ObjectConflictHandler ConflictEnd;

        //Provide WorldMonitor a method to raise event from
        public void RaiseConflictEnterers(GameObject objectWithConflict, GameObject[] conflictingObjects, string[] conflictingTypes)
        {
            ConflictEnterers?.Invoke(objectWithConflict, conflictingObjects, conflictingTypes);
        }

        //Provide WorldMonitor a method to raise event from
        public void RaiseConflictLeavers(GameObject objectWithConflict, GameObject[] conflictingObjects, string[] conflictingTypes)
        {
            ConflictLeavers?.Invoke(objectWithConflict, conflictingObjects, conflictingTypes);
        }

        //Provide WorldMonitor a method to raise event from
        public void EndConflicts(GameObject objectWithEndedConflict)
        {
            ConflictEnd?.Invoke(objectWithEndedConflict, default(GameObject[]), default(string[]));
        }

        private void Start()
        {
            //if user hasn't created a GameObject with WorldMonitor singleton
            if (!WorldMonitor.Instance)
            {
                new GameObject(gameObject.name + "_WMContainer", typeof(WorldMonitor));
            }

            if(WorldMonitor.Instance.TrackingMode == TrackingMode.UnityTriggers)
            {
                foreach (TrackedObjectContainer toc in TrackedObjects)
                {
                    foreach (GameObject go in toc.TrackedObjects)
                    {
                        if (!go.GetComponent<TrackedObjectTriggers>())
                            go.AddComponent<TrackedObjectTriggers>();

                        go.GetComponent<TrackedObjectTriggers>().AcceptOwner(this);
                    }
                }
            }
        }        
    }

    [System.Serializable]
    public class TrackedObjectContainer
    {
        public List<GameObject> TrackedObjects = new List<GameObject>();
    }
}