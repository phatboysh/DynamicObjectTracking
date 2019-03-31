using UnityEngine;
using System.Collections;
namespace oti.AI
{
    /// <summary>
    /// Class developed to show example usage of Dynamic Object Tracking
    /// </summary>
    public class Tracker : WorldMonitors
    {
        /*         
             The purpose of Tracker.cs is to demonstrate usage of WorldMonitors and Worldmonitor classes.
             Your tracking agent will need to subscribe to the ObjectConflictHandler delegate in WorldMonitors
             like this:         
                            GetComponent<WorldMonitors>().ConflictEnterers += yourFunction;
             
            
             Events in the ObjectConflictHandler:
             *** the optional arguments must be included by every method subscribing to this delegate
             
                ConflictEnterers: emitted every time a new tracked object enters a parent object's tracked area
                    Arguments(GameObject TrackedObject, GameObject[] ConflictingObjects, string[] ConflictingTypes)
                        TrackedObject: the object who's threshold has been penetrated
                        ConflictingObjects: an array of objects that have entered the tracked object's space
                        ConflictingTypes: an array of the object types ("A", "B", "C" etc) at matching index locations to ConflictingObjects[]

                ConflictLeavers:
                    Arguments(GameObject TrackedObject, GameObject[] ConflictingObjects, string[] ConflictingTypes)
                        TrackedObject: the object who's threshold has been partially or entirely vacated
                        ConflictingObjects: an array of objects that have vacated the tracked object's space
                        ConflictingTypes: an array of the object types ("A", "B", "C" etc) at matching index locations to ConflictingObjects[]

                ConflictEnd:
                    Arguments(GameObject TrackedObject, GameObject[] ConflictingObjects, string[] ConflictingTypes)
                        TrackedObject: the object who's threshold has been entirely vacated
                        ConflictingObjects & ConflictingTypes are not passed into this method, but are still required by all subscribers.
             */

        // Demonstration of event subscription
        void Start()
        {
            sColor = Color.blue;
            eColor = Color.red;

            if (GetComponent<WorldMonitors>())
            {
                WorldMonitors = GetComponent<WorldMonitors>();

                //subscribe to delegate for tracked object information
                WorldMonitors.ConflictEnterers += entererListener;
                WorldMonitors.ConflictLeavers += leaverListener;
                WorldMonitors.ConflictEnd += endListener;

                if (Demo)
                    StartCoroutine(demo());

                return;
            }

            Debug.LogWarning("Make sure the agent has a WorldMonitors component attached to it.");
        }

        /// <summary>
        /// Unsubscribe all Trackers that are destroyed
        /// </summary>
        private void OnDestroy()
        {
            WorldMonitors.ConflictEnterers -= entererListener;
            WorldMonitors.ConflictLeavers -= leaverListener;
            WorldMonitors.ConflictEnd -= endListener;
        }

        /// <summary>
        /// Cached ref to WorldMonitors class
        /// </summary>
        [HideInInspector]
        public WorldMonitors WorldMonitors;

        /// <summary>
        /// Prints conflicting object(s) and type
        /// </summary>
        [Tooltip("Shows tracking system interactions.")]
        public bool PrintConflictsToConsole;

        /// <summary>
        /// Can demonstrate how to add tracked objects at runtime
        /// </summary>
        [Tooltip("Uses a tracking system event to demonstrate how to add objects into tracking system.")]
        public bool InsertNewObjectOnEnter;

        /// <summary>
        /// This object will be inserted into tracking system if InsertNewObjectOnEnter is selected
        /// </summary>
        [Tooltip("If Insert New Object On Enter is true, this will be inserted at the origin on object entrance")]
        public GameObject ExampleInsertionObject;

        /// <summary>
        /// Will destroy objects that enter another's threshold
        /// </summary>
        [Tooltip("Uses a tracking system event to demonstrate how to remove objects from tracking system.")]
        public bool DestroyConflictingObjects;

        /// <summary>
        /// Runs an example method in example navigator class.
        /// </summary>
        [Tooltip("Uses a tracking system event to run a method for pure demo purposes You cannot destroy objects if using this method.")]
        public bool CrossProductCollisions;

        /// <summary>
        /// Triggers a coroutine to wait and change parameters for visual effects
        /// </summary>
        public bool Demo;

        /// <summary>
        /// Starting interpolation color value for added objects
        /// </summary>
        static Color sColor;

        /// <summary>
        /// Ending interpolation color value for added objects
        /// </summary>
        static Color eColor;

        /// <summary>
        /// This method shows how to handle objects raising a conflict with a TrackedObject
        /// </summary>
        private void entererListener(GameObject TrackedObject, GameObject[] ConflictingObjects, string[] ConflictingTypes)
        {
            if (PrintConflictsToConsole)
                Debug.Log("Conflict has STARTED for (" + gameObject.name + ") " + TrackedObject + " with:");

            for (int i = 0; i < ConflictingObjects.Length; i++)
            {
                if (PrintConflictsToConsole)
                    Debug.Log("        -- (" + gameObject.name + ") " + ConflictingObjects[i] + " of type: " + ConflictingTypes[i]);

                if (InsertNewObjectOnEnter && WorldMonitor.Instance.FreeSpace)
                {
                    GameObject go = Instantiate(ExampleInsertionObject, Vector3.zero, Quaternion.identity);
                    go.name = "NewGameObject_" + WorldMonitor.Instance.AllocationSpace;
                    go.AddComponent<ExampleNavigator>();
                    go.SetActive(true);
                    go.GetComponent<Renderer>().material.color = Color.Lerp(sColor, eColor, Mathf.Sin(0.5f * Time.time));
                    go.transform.position = new Vector3(Random.Range(-6, 6), Random.Range(-6, 6), Random.Range(-6, 6)); // [-6,6] ensures eventual collision with center object.
                    WorldMonitor.Instance.InsertNewTrackedObject(go, WorldMonitors, "YouCanMakeThisAnything", 2);
                }

                if (DestroyConflictingObjects)
                {
                    WorldMonitor.Instance.RemoveTrackedObject(ConflictingObjects[i]);
                    Destroy(ConflictingObjects[i]);
                }

                if (CrossProductCollisions && !DestroyConflictingObjects)
                {
                    if(ConflictingObjects[i])
                        ConflictingObjects[i].GetComponent<ExampleNavigator>().ObjectConflict(TrackedObject.transform.position);
                }
            }
        }
        
        /// <summary>
        /// This method shows how to handle objects leaving the conflict area of a Tracked Object
        /// </summary>
        /// <remarks> The user can choose to only subscribe to events raised when all conflicts have ended, or none at all. </remarks>
        private void leaverListener(GameObject TrackedObject, GameObject[] ConflictingObjects, string[] ConflictingTypes)
        {
            if (PrintConflictsToConsole)
                Debug.Log("Conflict has ENDED for (" + gameObject.name + ") " + TrackedObject + " with:");

            for (int i = 0; i < ConflictingObjects.Length; i++)
            {
                if (PrintConflictsToConsole)
                    Debug.Log("        -- (" + gameObject.name + ") " + ConflictingObjects[i] + " of type: " + ConflictingTypes[i]);
            }
        }

        /// <summary>
        /// This method shows how to handle events when a conflict ceases entirely.
        /// </summary>
        /// <remarks> The user can choose to only subscribe to events raised when any oject leaves a Tracked Object's conflict area, or none at all. </remarks>
        private void endListener(GameObject TrackedObject, GameObject[] ConflictingObjects, string[] ConflictingTypes)
        {
            if (PrintConflictsToConsole)
                Debug.Log("GameObject " + TrackedObject + "'s conflict(s) have ended and logged for " + gameObject.name);
        }

        /// <summary>
        /// Solely to look interesting :)
        /// </summary>
        private IEnumerator demo()
        {
            float t = Time.time;
            yield return new WaitWhile(() => Time.time - t < 10);

            sColor = Color.red;
            eColor = Color.yellow;

            CrossProductCollisions = true;
        }
    }
}