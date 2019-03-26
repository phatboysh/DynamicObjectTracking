using oti.Editors;
using System.Collections.Generic;
using UnityEngine;

namespace oti.AI
{
    /// <summary>
    /// Singleton which manages objects from the WorldMonitors classes
    /// </summary>
    public class WorldMonitor : MonoBehaviour
    {
         /// <summary>
        /// O(m*N^2) method - preferrable for a small number of objects
        /// </summary>
        public bool ExhaustiveMethod;

        /// <summary>
        /// Confines algorithm to main thread
        /// </summary>
        public bool RestrictToMainThread;

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
        private Dictionary<GameObject, int> gameObjectIDReference = new Dictionary<GameObject, int>();

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
        public Vector3 WorldOrigin;

        /// <summary>
        /// Try to set this to the smallest value that encloses all objects for best start up time
        /// </summary>
        [Tooltip("Use the widest lateral distance your world traverses from the x, y, or z directions")]
        public int InitialWorldSize = 100;

        /// <summary>
        /// Set this to the (approximate) smallest amount of area a tracked object will encounter
        /// </summary>
        public int MinimumObjectSize = 1;

        /// <summary>
        /// Represents the count of non-empty tracked object slots from all WorldMonitor components
        /// </summary>
        [HideInInspector]
        public int TotalTrackedObjects;
        
        /// <summary>
        /// If the world monitor has performed its set up
        /// </summary>
        private bool initialized;

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
            if (ExhaustiveMethod)
            {
                //this is only intended for benchmarking
                exhaustiveCalculation();
                return;
            }

            if (Octree.UpdateOctree()) //job has concluded
            {
                passedFrames = 0; //in sync with update

                TrackedObjectStates = Octree.TrackedObjectStates;

                OctreeThreadParameters otp = new OctreeThreadParameters
                {
                    ObjectIDs = new List<int>(TrackedObjectDataRef.Keys),
                    TotalTrackedObjects = TotalTrackedObjects,
                    Coordinates = getUpdatedPositions(new List<int>(TrackedObjectDataRef.Keys)),
                    DynamicObjects = TrackedObjectAffiliations,
                };

                foreach(KeyValuePair<int, string[]> tos in TrackedObjectStates.NewOrChangedStates)
                {
                    TrackedObjectData TOData = new TrackedObjectData();
                    List<string> affiliations = new List<string>();

                    /*/ Cleared off of the main thread in Octree.cs: TrackedObjectDataRef[tos.Key].ConflictingObjects.Clear(); /*/

                    TrackedObjectDataRef.TryGetValue(tos.Key, out TOData);

                    //add objects to TrackedObjectStates
                    int j = TrackedObjectStates.ConflictingIDs[tos.Key].Length;
                    for (int k = 0; k < j; k++)
                    {
                        int m = TrackedObjectStates.ConflictingIDs[tos.Key][k];
                        if(m != tos.Key)
                        {
                            TOData.ConflictingObjects.Add(gameObjectReference[m]);
                            affiliations.Add(TrackedObjectAffiliations[m]);
                        }
                    }

                    foreach (WorldMonitors wm in TOData.ObjectOwners) //inform the agents monitoring this object
                        wm.RaiseConflicts(TOData.Object, TOData.ConflictingObjects, affiliations);
                }

                //find conflicts that have ended and inform the agents monitoring
                foreach (int tos in TrackedObjectStates.PriorConflictingIDs)
                {
                    TrackedObjectData TOData;
                    TrackedObjectDataRef.TryGetValue(tos, out TOData);
                    /*/ Cleared off of the main thread in Octree.cs: TrackedObjectDataRef[tos].ConflictingObjects.Clear(); /*/

                    if (TOData.ObjectOwners.Count > 0)
                    {
                        foreach (WorldMonitors wm in TOData.ObjectOwners)
                        {
                            wm.EndConflicts(gameObjectReference[tos]);
                        }
                    }
                }

                Octree.ThreadOctreeInit(otp, RestrictToMainThread);
            }
            else
            {
                passedFrames++;
                if(passedFrames > 20)
                    Debug.Log("Octree processing for " + passedFrames + " frames after starting frame.");
            }
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
                    trackedObjectPositions.Add(id, new KeyValuePair<float, Vector3>(TOData.Threshold,TOData.Object.transform.position));                
            }

            return trackedObjectPositions;
        }

        void Start()
        {
            if (!Instance)
            {
                Instance = this;
            }
            else if(Instance != this)
            {
                Destroy(gameObject);
            }

           agentMonitors = GameObject.FindObjectsOfType<WorldMonitors>();

            /*             
             Start procedure is O(i*j*k)+ and a faster solution may exist
             */

            for (int i = 0; i < agentMonitors.Length; i++)
            {
                for (int j = 0; j < agentMonitors[i].TrackedObjects.Count; j++)
                {                    
                    for (int k = 0; k < agentMonitors[i].TrackedObjects[j].TrackedObjects.Count; k++)
                    {
                        float threshold = agentMonitors[i].ThresholdSet[j];
                        GameObject go = agentMonitors[i].TrackedObjects[j].TrackedObjects[k];

                        if (go) // allows user to leave empty gameobject slots in tracked object inspector
                        {
                            int id;
                            if (gameObjectIDReference.TryGetValue(go, out id))
                            {
                                TrackedObjectData TOData;
                                TrackedObjectDataRef.TryGetValue(id, out TOData);
                                TOData.ObjectOwners.Add(agentMonitors[i]);
                                //TrackedObjectDataRef[id] = TOData;
                            }
                            else
                            {
                                gameObjectIDReference.Add(go, TotalTrackedObjects); //object ID = current number of tracked objects
                                gameObjectReference.Add(TotalTrackedObjects, go); //using IDs necessary to run aux thread

                                TrackedObjectData TOData = new TrackedObjectData
                                {
                                    Object = go,
                                    Threshold = threshold,
                                    ObjectOwners = new List<WorldMonitors>(),
                                    ConflictingObjects = new List<GameObject>(),
                                    ObjectPosition = go.transform.position
                                };
                                TOData.ObjectOwners.Add(agentMonitors[i]);

                                TrackedObjectDataRef.Add(TotalTrackedObjects, TOData);
                                TrackedObjectAffiliations.Add(TotalTrackedObjects, OTIEditorBase._Alphabetic[j]);
                                TotalTrackedObjects++;
                            }
                        }
                    }
                }
            }

            Octree = new Octree
            {
                Main = System.Threading.Thread.CurrentThread
            };

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

            //configure tracked object states at start
            for (int i = 0; i < TotalTrackedObjects; i++)
            {
                string[] locals;
                if (!TrackedObjectStates.NewOrChangedStates.TryGetValue(i, out locals))
                    locals = new string[0];

                if (Octree.MasterList.ContainsKey(i))
                {
                    Octree.MasterList[i] = locals;
                }
                else
                {
                    Octree.MasterList.Add(i, locals);
                }
            }

            //initialize another job to keep threadcount sync before update cycles
            otp = new OctreeThreadParameters
            {
                ObjectIDs = new List<int>(TrackedObjectDataRef.Keys),
                TotalTrackedObjects = TotalTrackedObjects,
                Coordinates = getUpdatedPositions(new List<int>(TrackedObjectDataRef.Keys)),
                DynamicObjects = TrackedObjectAffiliations,
            };
            
            Octree.ThreadOctreeInit(otp, RestrictToMainThread);
        }

        private void OnDestroy() //kill outstanding thread
        {
            Octree.Abort();
        }

        /// <summary>
        /// The alternative method for calculating object distances with distinguishing criteria & affiliations
        /// This can be improved by storing magnitude calculations for referenced objects (effectively dividing n by 2)
        /// If you see value in this and wish to add it please submit a PR
        /// </summary>
        private void exhaustiveCalculation()
        {
            List<GameObject> conflicts = new List<GameObject>();
            List<string> conflictAffiliates = new List<string>();

            for (int i = 0; i < agentMonitors.Length; i++) //for every agent monitoring object(s)
            {
                for (int j = 0; j < agentMonitors[i].TrackedObjects.Count; j++) //for every different set of objects monitored by an agent
                {
                    for (int k = 0; k < agentMonitors[i].TrackedObjects[j].TrackedObjects.Count; k++) //for each item inside an agent's individual set
                    {
                        float threshold = agentMonitors[i].ThresholdSet[j]; //threshold this object set is configured to raise conflicts at
                        GameObject go = agentMonitors[i].TrackedObjects[j].TrackedObjects[k]; //an individual object

                        for (int l = 0; l < agentMonitors[i].TrackedObjects.Count; l++) //examine against other objects the agent wishes to compare
                        {
                            for (int m = 0; m < agentMonitors[i].TrackedObjects[j].TrackedObjects.Count; m++) //for each individual object in the other sets
                            {
                                GameObject _go = agentMonitors[i].TrackedObjects[j].TrackedObjects[m];
                                bool validObjects = (l != j && go && _go); //confirms if not empty inspector slot(s) and in a different tracked object set

                                if (validObjects && (_go.transform.position - go.transform.position).sqrMagnitude < threshold * threshold)
                                {
                                    conflicts.Add(go);

                                    /*
                                     The following procedure with dictionaries can be improved, but if you're using this
                                     tracking system it's unlikely you'll use exhaustive method for anything but testing
                                     */

                                    int id; string a;
                                    gameObjectIDReference.TryGetValue(_go, out id);
                                    TrackedObjectAffiliations.TryGetValue(id, out a);
                                    conflictAffiliates.Add(a);
                                }
                            }
                        }

                        if (go)
                        {
                            int _id; TrackedObjectData TOData;

                            gameObjectIDReference.TryGetValue(go, out _id);
                            TrackedObjectDataRef.TryGetValue(_id, out TOData);

                            foreach (WorldMonitors wm in TOData.ObjectOwners) //tell the agent's monitoring this object of the conflict
                                wm.RaiseConflicts(go, conflicts, conflictAffiliates);
                        }
                    }
                }
            }
        }

        #region Public Methods

        /// <summary>
        /// Runtime objects should be inserted into the tracking system here.
        /// </summary>
        /// <param name="trackedObject">The object to be tracked.</param>
        /// <param name="owner">Provide the WorldMonitors component from the agent tracking this object.</param>
        /// <param name="objectAffiliation">The class of objects this item is in (e.g. "A", "B", etc.)</param>
        /// <param name="threshold">The distance before an object is considered close to this object.</param>
        /// <remarks>Due to the cost associated with this operation, perform minimal additions per frame or run from coroutine</remarks>
        public void InsertNewTrackedObject(GameObject go, WorldMonitors owner, string objectAffiliation, float threshold)
        {
            int id;
            if (gameObjectIDReference.TryGetValue(go, out id))
            {
                TrackedObjectData TOData;
                TrackedObjectDataRef.TryGetValue(id, out TOData);
                TOData.ObjectOwners.Add(owner);
            }
            else
            {
                gameObjectIDReference.Add(go, TotalTrackedObjects);
                gameObjectReference.Add(TotalTrackedObjects, go);
                TrackedObjectData TOData = new TrackedObjectData
                {
                    Object = go,
                    Threshold = threshold,
                    ObjectOwners = new List<WorldMonitors>(),
                    ConflictingObjects = new List<GameObject>(),
                    ObjectPosition = go.transform.position
                };
                TOData.ObjectOwners.Add(owner);

                TrackedObjectDataRef.Add(TotalTrackedObjects, TOData);
                TrackedObjectAffiliations.Add(TotalTrackedObjects, objectAffiliation);
                TotalTrackedObjects++;
            }
        }

        /// <summary>
        /// Use a tracked object's OnDestroy (or some other suitable method) to call this method upon removal of a tracked object.
        /// </summary>
        /// <param name="trackedObject">The object to be tracked.</param>
        /// <param name="owner">The agent tracking this object.</param>
        /// <remarks>Due to the cost associated with this operation, perform minimal additions per frame or run from coroutine</remarks>
        public void RemoveTrackedObject(GameObject trackedObject, Tracker owner)
        {
            int removalID;
            gameObjectIDReference.TryGetValue(trackedObject, out removalID);
            gameObjectIDReference.Remove(trackedObject);
            gameObjectReference.Remove(removalID);

            TrackedObjectDataRef.Remove(removalID);
            TrackedObjectAffiliations.Remove(removalID);

            TotalTrackedObjects--;
        }
        #endregion
    }

    public class TrackedObjectData
    {
        public float Threshold;
        public GameObject Object;
        public List<GameObject> ConflictingObjects;
        public List<WorldMonitors> ObjectOwners;
        public Vector3 ObjectPosition;
    }
}