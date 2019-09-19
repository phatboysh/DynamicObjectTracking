using oti.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace oti.AI
{
    public enum ConflictEndMode
    {
        OnAllConflictsEnded,
        OnIndividualConflictEnded,
        NoConflictEndEvents
    }

    public enum TrackingMode
    {
        Octree,
        UnityTriggers
    }

    /// <summary>
    /// Singleton which manages objects from the WorldMonitors classes
    /// </summary>
    public class WorldMonitor : MonoBehaviour
    {
        #region Temp Format
        // TODO: implement custom inspector similar to the WorldMonitors class
        [Header("World Monitor - System Parameters")] 
        [Space]
        [Space]
        #endregion

        [Tooltip("Choosing OnAllConflictsEnded will only raise an events when every conflicting object has left the conflict area.")]
        public ConflictEndMode ConflictEndMode = ConflictEndMode.OnIndividualConflictEnded;

        [Tooltip("Octree runs on aux thread independent of Unity. Using triggers will place trigger colliders on all tracked objects.")]
        public TrackingMode TrackingMode = TrackingMode.Octree;

        [Tooltip("If using UnityTriggers tracking mode, specify whether or not tracked objects will use rigidbodies with gravity enabled.")]
        public bool TrackedObjectsUseGravity;

        [Tooltip("Triggers set one another off when the colliders themselves collide, not just the object. This behavior can be replicated by choosing true here.")]
        public bool OctreeMimicTriggerInteraction;

        [Tooltip("The octree system will only register conflicts when an object has entered another threshold, however triggers interact based off their collider sizes. You can mimic the octree behavior here.")]
        public bool TriggersMimicOctree;

        [Tooltip("Set this value to place a cap on the total objects you wish to allow into tracking system. If you have no cap, set arbitrarily large (no larger than 2,147,483,646)")]
        public int MaximumObjectsAllowed = 5000;

        [Header("Testing Tools")]
        /// <summary>
        /// O(m*N^2) method - preferrable for a small number of objects
        /// </summary>
        public bool ExhaustiveMethod;

        /// <summary>
        /// Confines algorithm to main thread
        /// </summary>
        public bool RestrictToMainThread;
        [Space]

        /// <summary>
        /// Tracked object's ID and associated properties
        /// int Object IDs are used to reference GameObjects since they are not threadsafe
        /// </summary>
        public Dictionary<int, TrackedObjectData> TrackedObjectDataRef = new Dictionary<int, TrackedObjectData>();

        /// <summary>
        /// Relationship between object ID and object classes
        /// </summary>
        public Dictionary<int, string> TrackedObjectAffiliations = new Dictionary<int, string>();

        /// <summary>
        /// Reference to an object's ID to facilitate removal at runtime.
        /// </summary>
        public Dictionary<GameObject, int> GameObjectIDReference = new Dictionary<GameObject, int>();

        /// <summary>
        /// Reference to an object's ID to facilitate removal at runtime.
        /// </summary>
        private Dictionary<int, GameObject> gameObjectReference = new Dictionary<int, GameObject>();

        /// <summary>
        /// Cache of states
        /// </summary>
        public TrackedObjectStates TrackedObjectStates;

        /// <summary>
        /// The single Octree class to be used
        /// </summary>
        public Octree Octree;

        /// <summary>
        /// Position from which the Octree will initially surround
        /// </summary>
        [Header("** Set to Map Center **")]
        public Vector3 WorldOrigin;

        /// <summary>
        /// Try to set this to the smallest value that encloses all objects for best start up time
        /// </summary>
        [Header("** Set to Encompass all Objects **")]
        [Tooltip("Use a size that encompasses all of your initial objects (you can simply use the calculated distance between farthest objects). The Octree grows as needed.")]
        public int InitialWorldSize = 100;

        /// <summary>
        /// Set this to the (approximate) smallest amount of area a tracked object will encounter
        /// </summary>
        [Header("** Set to Smallest Tracked Object Size **")]
        public int MinimumObjectSize = 1;

        /// <summary>
        /// Represents the count of non-empty tracked object slots from all WorldMonitors components
        /// </summary>
        [HideInInspector]
        public int TotalTrackedObjects;

        /// <summary>
        /// How much space arrays will need for indexing in octree evaluation
        /// </summary>
        [HideInInspector]
        public int AllocationSpace;

        /// <summary>
        /// True when tracking less than the total object limit as defined by user
        /// </summary>
        [HideInInspector]
        public bool FreeSpace;

        /// <summary>
        /// If the world monitor has performed its set up
        /// </summary>
        private bool initialized;

        /// <summary>
        /// Indicates if user chose triggers as preferred tracking method.
        /// </summary>
        private bool usingTriggers;

        /// <summary>
        /// number of frames it has taken for the octree to update
        /// </summary>
        private int passedFrames;

        /// <summary>
        /// WorldMonitors present in the scene
        /// </summary>
        private WorldMonitors[] agentMonitors;

        /// <summary>
        /// Singleton implementation
        /// </summary>
        private static WorldMonitor _instance = null;

        public static WorldMonitor Instance
        {
            get
            {
                return _instance;
            }
            set
            {
                _instance = value;
            }
        }

        void Update()
        {
            if (ExhaustiveMethod || usingTriggers) // if you are not using these features you can remove this if statement
            {
                if(ExhaustiveMethod)
                    exhaustiveCalculation();
                return;
            }

            if (Octree.UpdateOctree()) // job has concluded
            {
                passedFrames = 0; // in sync with update

                TrackedObjectStates = Octree.TrackedObjectStates;

                OctreeThreadParameters otp = refreshThreadingParameters();

                int numStates = TrackedObjectStates.ParentIDEnterers.Length;

                for (int tos = 0; tos < numStates; tos++)
                {
                    int parentID = TrackedObjectStates.ParentIDEnterers[tos];

                    TrackedObjectData TOData;
                    if (TrackedObjectDataRef.TryGetValue(parentID, out TOData))
                    {
                        int numConflicts = TrackedObjectStates.EnteringIDs[tos].Length;

                        GameObject[] conflictors = new GameObject[numConflicts]; // allocate arrays for conflict data
                        string[] conflictorAffiliations = new string[numConflicts];

                        // fill conflict data
                        for (int i = 0; i < numConflicts; i++)
                        {
                            int m = TrackedObjectStates.EnteringIDs[tos][i];
                            conflictors[i] = gameObjectReference[m];
                            conflictorAffiliations[i] = TrackedObjectAffiliations[m];
                        }

                        foreach (WorldMonitors wm in TOData.ObjectOwners) //inform the agents monitoring this object}                        
                            wm.RaiseConflictEnterers(TOData.Object, conflictors, conflictorAffiliations);                                                   
                    }
                }

                // if user wishes for no end conflict events to be raised, the update has concluded.
                if (ConflictEndMode == ConflictEndMode.NoConflictEndEvents)
                {
                    Octree.ThreadOctreeInit(otp, RestrictToMainThread);
                    return;
                }

                handleEndConflicts(ConflictEndMode, TrackedObjectStates);

                Octree.ThreadOctreeInit(otp, RestrictToMainThread);
            }
            else
            {
                passedFrames++;
            }
        }

        /// <summary>
        /// Handles end of conflict events if user chooses to do so.
        /// </summary>
        private void handleEndConflicts(ConflictEndMode mode, TrackedObjectStates tos)
        {
            switch (mode)
            {
                case ConflictEndMode.OnIndividualConflictEnded:
                    //find conflicts that have partially or completely ended and inform agents monitoring
                    int numStates = tos.ParentIDLeavers.Length;

                    for (int endParentID = 0; endParentID < numStates; endParentID++)
                    {
                        int parentID = tos.ParentIDLeavers[endParentID];

                        TrackedObjectData TOData;// = TrackedObjectDataRef[parentID];
                        if (TrackedObjectDataRef.TryGetValue(parentID, out TOData))
                        {
                            int numConflicts = tos.LeavingIDs[endParentID].Length;

                            // allocate arrays for conflict data
                            GameObject[] leavers = new GameObject[numConflicts];
                            string[] conflictorAffiliations = new string[numConflicts];

                            // fill conflict data
                            for (int i = 0; i < numConflicts; i++)
                            {
                                int m = TrackedObjectStates.LeavingIDs[endParentID][i];
                                leavers[i] = gameObjectReference[m];
                                conflictorAffiliations[i] = TrackedObjectAffiliations[m];
                            }

                            //inform the agents monitoring this object
                            foreach (WorldMonitors wm in TOData.ObjectOwners)
                                wm.RaiseConflictLeavers(TOData.Object, leavers, conflictorAffiliations);
                        }
                    }
                    break;

                case ConflictEndMode.OnAllConflictsEnded:
                    //find conflicts that have completely ended and inform the agents monitoring

                    foreach (int endParentID in TrackedObjectStates.PriorConflictingIDs)
                    {
                        TrackedObjectData TOData;
                        if (TrackedObjectDataRef.TryGetValue(endParentID, out TOData)) // preserve the ability to have non-owned tracked objects
                        {
                            if (TOData.ObjectOwners.Count > 0)
                            {
                                foreach (WorldMonitors wm in TOData.ObjectOwners)
                                {
                                    wm.EndConflicts(gameObjectReference[endParentID]);
                                }
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// Updates parameters for use in parallel Octree thread
        /// </summary>
        private OctreeThreadParameters refreshThreadingParameters()
        {
            return new OctreeThreadParameters
            {
                ObjectIDs = new List<int>(TrackedObjectDataRef.Keys),
                TotalTrackedObjects = TotalTrackedObjects,
                Coordinates = getUpdatedPositions(new List<int>(TrackedObjectDataRef.Keys)),
                DynamicObjects = TrackedObjectAffiliations,
            };
        }

        /// <summary>
        /// Updates transform positions of tracked gameobjects
        /// </summary>
        private Dictionary<int, KeyValuePair<float, Vector3>> getUpdatedPositions(List<int> trackedIDs)
        {
            Dictionary<int, KeyValuePair<float, Vector3>> trackedObjectPositions = new Dictionary<int, KeyValuePair<float, Vector3>>();

            TrackedObjectData TOData;

            foreach (int id in trackedIDs)
            {
                if (TrackedObjectDataRef.TryGetValue(id, out TOData))
                    trackedObjectPositions.Add(id, new KeyValuePair<float, Vector3>(TOData.Threshold, TOData.Object.transform.position));
            }

            return trackedObjectPositions;
        }

        private void OnEnable()
        {
            if (!Instance)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void Start()
        {
            usingTriggers = TrackingMode == TrackingMode.UnityTriggers;

            agentMonitors = GameObject.FindObjectsOfType<WorldMonitors>();

            /*             
             Start procedure is O(i*j*k) and a faster solution may exist
             */

            for (int i = 0; i < agentMonitors.Length; i++)
            {
                for (int j = 0; j < agentMonitors[i].TrackedObjects.Count; j++)
                {
                    for (int k = 0; k < agentMonitors[i].TrackedObjects[j].TrackedObjects.Count; k++)
                    {
                        // double threshold size if user wishes for point octree to mimic trigger interaction distances
                        float threshold = (OctreeMimicTriggerInteraction && TrackingMode == TrackingMode.Octree) ? agentMonitors[i].ThresholdSet[j] * 3/2 : agentMonitors[i].ThresholdSet[j];

                        GameObject go = agentMonitors[i].TrackedObjects[j].TrackedObjects[k];

                        if (go) // allows user to leave empty gameobject slots in tracked object inspector
                        {
                            int id;
                            if (GameObjectIDReference.TryGetValue(go, out id))
                            {
                                TrackedObjectData TOData;
                                TrackedObjectDataRef.TryGetValue(id, out TOData);
                                TOData.ObjectOwners.Add(agentMonitors[i]);
                                TrackedObjectDataRef[id] = TOData;

                                if(usingTriggers)
                                {
                                    // WorldMonitors will declare ownership in its Start() method

                                    if(!go.GetComponent<TrackedObjectTriggers>())
                                        go.AddComponent<TrackedObjectTriggers>();

                                    go.GetComponent<TrackedObjectTriggers>().Initialize();
                                }
                            }
                            else
                            {
                                GameObjectIDReference.Add(go, TotalTrackedObjects); //object ID = current number of tracked objects
                                gameObjectReference.Add(TotalTrackedObjects, go); //using IDs necessary to run aux thread

                                TrackedObjectData TOData = new TrackedObjectData
                                {
                                    Object = go,
                                    Threshold = threshold,
                                    ObjectOwners = new List<WorldMonitors>()
                                };

                                TOData.ObjectOwners.Add(agentMonitors[i]);
                                TrackedObjectDataRef.Add(TotalTrackedObjects, TOData);
                                TrackedObjectAffiliations.Add(TotalTrackedObjects, OTIUtilities._AlphabetAssembler(j));

                                if (usingTriggers)
                                {
                                    if (!go.GetComponent<TrackedObjectTriggers>())
                                        go.AddComponent<TrackedObjectTriggers>();
                                    go.GetComponent<TrackedObjectTriggers>().Initialize();
                                }

                                TotalTrackedObjects++;
                            }
                        }
                    }
                }
            }

            AllocationSpace = TotalTrackedObjects;
            FreeSpace = MaximumObjectsAllowed - AllocationSpace >= 0;

            if (usingTriggers || ExhaustiveMethod)
                return; // no more set up required; switching between modes is not allowed at runtime.

            Octree = new Octree();

            //configure tracked object states at start
            for (int i = 0; i < TotalTrackedObjects; i++)
            {
                List<int> locals = new List<int>();

                if (Octree.MasterList.ContainsKey(i))
                {
                    Octree.MasterList[i] = locals;
                }
                else
                {
                    Octree.MasterList.Add(i, locals);
                }
            }

            OctreeThreadParameters otp = new OctreeThreadParameters
            {
                ObjectIDs = new List<int>(TrackedObjectDataRef.Keys),
                TotalTrackedObjects = TotalTrackedObjects,
                Coordinates = getUpdatedPositions(new List<int>(TrackedObjectDataRef.Keys)),
                DynamicObjects = TrackedObjectAffiliations
            };

            //construct initial octree            
            Octree.Initialize(InitialWorldSize, WorldOrigin, MinimumObjectSize);

            Octree.IsDone = true; //allows an initial pass into job start
            Octree.ThreadOctreeInit(otp, RestrictToMainThread);

            while (!Octree.UpdateOctree()) { } //wait until conflict states are established

            TrackedObjectStates = Octree.TrackedObjectStates;

            // wipe initial results so conflicts existing before start register
            for (int i = 0; i < Octree.MasterList.Count; i++)
                Octree.MasterList[i] = new List<int>();
        }

        private void OnDestroy() //kill outstanding thread
        {
            Octree?.Abort();
        }

        /// <summary>
        /// The alternative method for calculating object distances with distinguishing criteria & affiliations
        /// This method is ~ O(i^2 * j^2 * n^2 * numObjTrackers)
        /// This can be improved by storing magnitude calculations for referenced objects (effectively dividing n by 2)
        /// If you see value in this and wish to add it please submit a PR
        /// </summary>
        private void exhaustiveCalculation()
        {
            for (int i = 0; i < agentMonitors.Length; i++) //for every agent monitoring object(s)
            {
                for (int j = 0; j < agentMonitors[i].TrackedObjects.Count; j++) //for every different set of objects monitored by an agent
                {
                    for (int k = 0; k < agentMonitors[i].TrackedObjects[j].TrackedObjects.Count; k++) //for each item inside an agent's individual set
                    {
                        float threshold = agentMonitors[i].ThresholdSet[j]; //threshold this object set is configured to raise conflicts at
                        GameObject go = agentMonitors[i].TrackedObjects[j].TrackedObjects[k]; //an individual object
                        if (go) // confirms non empty inspector slot
                        {
                            for (int compareAgent = 0; compareAgent < agentMonitors.Length; compareAgent++)
                            {
                                for (int compareSet = 0; compareSet < agentMonitors[compareAgent].TrackedObjects.Count; compareSet++) //examine against other objects the agent wishes to compare
                                {
                                    for (int compareObject = 0; compareObject < agentMonitors[compareAgent].TrackedObjects[compareSet].TrackedObjects.Count; compareObject++) //for each individual object in the other sets
                                    {
                                        GameObject _go = agentMonitors[compareAgent].TrackedObjects[compareSet].TrackedObjects[compareObject];

                                        if (_go && compareSet != j) // confirms non empty inspector slot && different sets
                                        {
                                            if ((_go.transform.position - go.transform.position).sqrMagnitude < threshold * threshold)
                                            {
                                                int id = GameObjectIDReference[go];
                                                TrackedObjectData TOData;// = TrackedObjectDataRef[parentID];
                                                TrackedObjectDataRef.TryGetValue(id, out TOData);

                                                foreach (WorldMonitors wm in TOData.ObjectOwners)
                                                    wm.RaiseConflictEnterers(TOData.Object, new GameObject[1] { _go }, new string[1] { TrackedObjectAffiliations[id] }); 
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #region Public Methods

        /// <summary>
        /// Choose either a GameObject or entire field type's threshold to change. One must be used or the method will fail!
        /// </summary>
        /// <param name="trackedObject">The GameObject to change the threshold for.</param>
        /// <param name="objectType">The field name of this object (e.g. "A", "B", "C" etc from field name "Tracked Object Set"</param>
        /// <param name="threshold">The new threshold for this set</param>
        /// <remarks> Changing an entire field type's threshold is slow and only recommended for small sets of tracked objects. </remarks>
        public void ChangeThresholdSize(float threshold, GameObject trackedObject = default(GameObject), string objectType = default(string))
        {
            if (trackedObject != default(GameObject))
            {
                int id;
                if (GameObjectIDReference.TryGetValue(trackedObject, out id)) //if the user passes in a non tracked GameObject, don't try to modify threshold
                {
                    TrackedObjectDataRef[id].Threshold = threshold;
                }
            }
            else if (objectType != default(string))
            {
                foreach (int id in TrackedObjectAffiliations.Keys)
                {
                    if (string.Compare(TrackedObjectAffiliations[id], objectType) == 0)
                    {
                        TrackedObjectDataRef[id].Threshold = threshold;
                    }
                }
            }
            else
            {
                throw new System.Exception("To change threshold size, either a tracked object class type or specific GameObject must be provided as an argument.");
            }
        }

        /// <summary>
        /// Choose either a GameObject or entire field type's trigger size to change. One must be used or the method will fail!
        /// </summary>
        /// <param name="trackedObject">The GameObject to change the threshold for.</param>
        /// <param name="objectType">The field name of this object (e.g. "A", "B", "C" etc from field name "Tracked Object Set"</param>
        /// <param name="threshold">The new threshold for this set</param>
        /// <remarks> Changing an entire field type's trigger radius is slow and only recommended for small sets of tracked objects. </remarks>
        public void ChangeTriggerSize(float threshold, GameObject trackedObject = default(GameObject), string objectType = default(string))
        {
            if(!usingTriggers)
            {
                Debug.LogWarning("Changing tracked object trigger sizes will do nothing without using Unity Trigger mode!");
                return;
            }

            if (trackedObject != default(GameObject))
            {
                trackedObject.GetComponent<SphereCollider>().radius = threshold;
            }
            else if (objectType != default(string))
            {
                foreach (int id in TrackedObjectAffiliations.Keys)
                {
                    if (string.Compare(TrackedObjectAffiliations[id], objectType) == 0)
                    {
                        gameObjectReference[id].GetComponent<SphereCollider>().radius = threshold;
                    }
                }
            }
            else
            {
                throw new System.Exception("To change threshold size, either a tracked object class type or specific GameObject must be provided as an argument.");
            }
        }

        /// <summary>
        /// Runtime objects should be inserted into the tracking system here.
        /// </summary>
        /// <param name="trackedObject">The object to be tracked.</param>
        /// <param name="owner">Provide the WorldMonitors component from the agent tracking this object.</param>
        /// <param name="objectAffiliation">The class of objects this item is in (e.g. "A", "B", etc.)</param>
        /// <param name="threshold">Regardless of class type, this object can be inserted with its own threshold size.</param>
        /// <remarks>Due to the cost associated with this operation, perform minimal additions per frame or run from coroutine</remarks>
        public void InsertNewTrackedObject(GameObject trackedObject, WorldMonitors owner, string objectAffiliation, float threshold)
        {
            if (!FreeSpace)
                return;

            if (usingTriggers)
            {
                handleTriggerAddition(trackedObject, owner, objectAffiliation, threshold);
                FreeSpace = MaximumObjectsAllowed - AllocationSpace > 0;
                return;
            }

            /*
             Do not need to add directly into Octree
             Happens in the first update saving time from main thread
             */

            int id;
            if (GameObjectIDReference.TryGetValue(trackedObject, out id))
            {
                // allow for user to add new trackers to one object
                TrackedObjectData TOData;
                TrackedObjectDataRef.TryGetValue(id, out TOData);

                if (owner) // else the user has tried to add a non-owned object more than once
                    TOData.ObjectOwners.Add(owner);
            }
            else
            {
                GameObjectIDReference.Add(trackedObject, AllocationSpace);
                gameObjectReference.Add(AllocationSpace, trackedObject);

                TrackedObjectData TOData = new TrackedObjectData
                {
                    Object = trackedObject,
                    Threshold = OctreeMimicTriggerInteraction ? 3/2 * threshold : threshold,
                    ObjectOwners = new List<WorldMonitors>()
                };

                if (owner)
                    TOData.ObjectOwners.Add(owner);

                Octree.MasterList.Add(AllocationSpace, new List<int>());

                TrackedObjectDataRef.Add(AllocationSpace, TOData);
                TrackedObjectAffiliations.Add(AllocationSpace, objectAffiliation);

                TotalTrackedObjects++;
                AllocationSpace++;

                Octree.TrackedObjectStates = TrackedObjectStates;
                FreeSpace = MaximumObjectsAllowed - AllocationSpace >= 0;
            }
        }


        /// <summary>
        /// Use a tracked object's OnDestroy (or some other suitable method) to call this method upon removal of a tracked object.
        /// </summary>
        /// <param name="trackedObject">The object to be tracked.</param>
        /// <param name="owner">The agent tracking this object.</param>
        /// <param name="whoToRemove">Optional - identify a particular WorldMonitors to remove while leaving other trackers in place.</param>
        /// <param name="retainOtherTrackers">Optional - if selected, only the specified WorldMonitors (agent) will be removed from the Object's tracker list.</param>
        /// <remarks>Due to the cost associated with this operation, perform minimal additions per frame or run from coroutine</remarks>
        public void RemoveTrackedObject(GameObject trackedObject, WorldMonitors whoToRemove = default(WorldMonitors), bool retainOtherTrackers = default(bool))
        {
            if (usingTriggers)
            {
                handleTriggerRemoval(trackedObject, whoToRemove, retainOtherTrackers);
                return;
            }

            int removalID;
            GameObjectIDReference.TryGetValue(trackedObject, out removalID);

            if (whoToRemove != default(WorldMonitors))
            {
                TrackedObjectDataRef[removalID].ObjectOwners.Remove(whoToRemove);
                if (TrackedObjectDataRef[removalID].ObjectOwners.Count == 0)
                {
                    Octree.PointOctree.Remove(removalID);
                }
                else if(retainOtherTrackers)
                {
                    return;
                }
            }
            else
            {
                Octree.PointOctree.Remove(removalID);
            }

            Octree.MasterList[removalID] = new List<int>();

            // to reinsert later, TrackedObjectDataRef needs updated.
            TrackedObjectDataRef.Remove(removalID);

            /*
             TODO: test -- FreeSpace = MaximumObjectsAllowed - AllocationSpace >= 0;
             allow for space to be opened back up -- may be LINQ intensive
             */

            TotalTrackedObjects--;
        }
        #endregion

        /// <summary>
        /// Handles object removal when user is using Unity triggers
        /// </summary>
        private void handleTriggerRemoval(GameObject trackedObject, WorldMonitors whoToRemove = default(WorldMonitors), bool retainOtherTrackers = default(bool))
        {
            if (whoToRemove != default(WorldMonitors))
            {
                gameObject.GetComponent<TrackedObjectTriggers>().LoseOwner(whoToRemove);
                if (retainOtherTrackers)
                    return;
            }

            TrackedObjectTriggers tot = gameObject.GetComponent<TrackedObjectTriggers>();

            if (tot)
                tot.wms = new List<WorldMonitors>(); // empty it.

            TotalTrackedObjects--;            
            return;
        }

        /// <summary>
        /// Handles addition of new tracked object if user is using Unity triggers.
        /// </summary>
        private void handleTriggerAddition(GameObject trackedObject, WorldMonitors owner, string objectAffiliation, float threshold)
        {
            if(!trackedObject.GetComponent<TrackedObjectTriggers>())
                trackedObject.AddComponent<TrackedObjectTriggers>();

            TrackedObjectTriggers tot = trackedObject.GetComponent<TrackedObjectTriggers>();

            GameObjectIDReference.Add(trackedObject, AllocationSpace);
            gameObjectReference.Add(AllocationSpace, trackedObject);

            TrackedObjectData TOData = new TrackedObjectData
            {
                Object = trackedObject,
                Threshold = TriggersMimicOctree ? 0.75f * threshold : threshold,
                ObjectOwners = new List<WorldMonitors>()
            };

            if (owner)
            {
                TOData.ObjectOwners.Add(owner);
                tot.AcceptOwner(owner);
            }

            TrackedObjectDataRef.Add(AllocationSpace, TOData);
            TrackedObjectAffiliations.Add(AllocationSpace, objectAffiliation);

            TotalTrackedObjects++;
            AllocationSpace++;
            tot.Initialize();     
        }
    }

    /// <summary>
    /// The only threadsafe tracked object information
    /// </summary>
    public class TrackedObjectData
    {
        public float Threshold;
        public GameObject Object;
        public List<WorldMonitors> ObjectOwners;
    }
}