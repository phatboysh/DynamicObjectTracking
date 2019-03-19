using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace oti.AI
{
    /// <summary>
    /// Class developed to show example usage of Dynamic Object Tracking
    /// </summary>
    public class Tracker : MonoBehaviour
    {
        /// <summary>
        /// Cached ref to WorldMonitors class
        /// </summary>
        public WorldMonitors WorldMonitors;

        /// <summary>
        /// Prints collision object and type
        /// </summary>
        public bool PrintCollisionsToConsole;

        void Start()
        {
            if (GetComponent<WorldMonitors>())
            {
                WorldMonitors = GetComponent<WorldMonitors>();
                WorldMonitors.Conflicts += listener; //subscribe to delegate for tracked object information
                return;
            }

            Debug.LogWarning("Make sure the agent has a WorldMonitors component attached to it.");
        }

        private void OnDestroy()
        {
            WorldMonitors.Conflicts -= listener;
        }

        /// <summary>
        /// This method shows how to handle event data. If the event is for an ended conflict, TrackedObject is the only argument passed
        /// </summary>
        private void listener(GameObject TrackedObject, List<GameObject> ConflictingObjects = default(List<GameObject>), List<string> ConflictingTypes = default(List<string>))
        {
            if(ConflictingObjects == default(List<GameObject>))
            {
                if(PrintCollisionsToConsole)
                    Debug.Log("GameObject " + TrackedObject + "'s conflict(s) have ended.");

                return;
            }

            if(PrintCollisionsToConsole)
                Debug.Log("Conflict has occurred for (" + gameObject.name + ") " + TrackedObject + " with:");

            for (int i = 0; i < ConflictingObjects.Count; i++)
            {
                if (PrintCollisionsToConsole)
                    Debug.Log("        -- (" + gameObject.name + ") " + ConflictingObjects[i] + " of type: " + ConflictingTypes[i]);

                ConflictingObjects[i].GetComponent<ExampleNavigator>().ObjectConflict(ConflictingObjects[i].transform.position);
            }
        }
    }
}