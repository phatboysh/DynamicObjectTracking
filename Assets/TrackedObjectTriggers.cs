using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace oti.AI
{
    public class TrackedObjectTriggers : MonoBehaviour
    {
        /// <summary>
        /// How many agents are tracking this object
        /// </summary>
        public int Owners;

        /// <summary>
        /// The uID for this tracked object.
        /// </summary>
        public int TrackedObjectID;

        /// <summary>
        /// The agents following this tracked object
        /// </summary>
        public List<WorldMonitors> wms = new List<WorldMonitors>();

        /// <summary>
        /// Record of how many conflicts occurring
        /// </summary>
        private int insiders = 0;

        /// <summary>
        /// Agents who find this object in a field will add themselves as owners here
        /// </summary>
        public void AcceptOwner(WorldMonitors wm)
        {
            Owners++; // quicker than wms.Count
            wms.Add(wm);
        }

        /// <summary>
        /// Agents notify objects when they are destroyed
        /// </summary>
        public void LoseOwner(WorldMonitors wm)
        {
            Owners--;
            wms.Remove(wm);
        }

        /// <summary>
        /// Set up unity components to user config
        /// </summary>
        public void Initialize()
        {
            SphereCollider collider;

            if (!GetComponent<SphereCollider>())
                gameObject.AddComponent<SphereCollider>();

            collider = GetComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.enabled = true;

            if(!GetComponent<Rigidbody>())
                gameObject.AddComponent<Rigidbody>();

            GetComponent<Rigidbody>().useGravity = WorldMonitor.Instance.TrackedObjectsUseGravity;

            TrackedObjectID = WorldMonitor.Instance.GameObjectIDReference[gameObject];

            TrackedObjectData todata;
            WorldMonitor.Instance.TrackedObjectDataRef.TryGetValue(TrackedObjectID, out todata);

            collider.radius = WorldMonitor.Instance.TriggersMimicOctree ? 0.75f * todata.Threshold : todata.Threshold; // allow triggers to mimic point octree
        }

        // MonoBehaviour method
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject)
            {
                GameObject[] goPass = new GameObject[1];
                goPass[0] = other.gameObject;

                int childID;
                int parentID;

                if (WorldMonitor.Instance.GameObjectIDReference.TryGetValue(goPass[0], out childID)) // not all gameObjects may be in tracking system
                {
                    if (!WorldMonitor.Instance.GameObjectIDReference.TryGetValue(gameObject, out parentID))
                        Debug.LogError("TrackedObjectTriggers cannot be used on objects not located in a field of a WorldMonitors instance");

                    insiders++;

                    string[] sPass = new string[1];
                    sPass[0] = WorldMonitor.Instance.TrackedObjectAffiliations[childID];

                    if (string.Compare(WorldMonitor.Instance.TrackedObjectAffiliations[TrackedObjectID], sPass[0]) != 0)
                    {
                        foreach (WorldMonitors wm in wms)
                            wm.RaiseConflictEnterers(gameObject, goPass, sPass);
                    }
                }
            }
        }

        // MonoBehaviour method
        private void OnTriggerExit(Collider other)
        {
            ConflictEndMode cem = WorldMonitor.Instance.ConflictEndMode;

            if (cem == ConflictEndMode.NoConflictEndEvents)
                return;

            if (other.gameObject)
            {
                GameObject[] goPass = new GameObject[1];
                goPass[0] = other.gameObject;

                int childID;
                int parentID;

                if (WorldMonitor.Instance.GameObjectIDReference.TryGetValue(goPass[0], out childID)) // not all gameObjects may be in tracking system
                {
                    if (!WorldMonitor.Instance.GameObjectIDReference.TryGetValue(gameObject, out parentID))
                        Debug.LogError("TrackedObjectTriggers cannot be used on objects not located in a field of a WorldMonitors instance");

                    insiders--;

                    string[] sPass = new string[1];
                    sPass[0] = WorldMonitor.Instance.TrackedObjectAffiliations[childID];

                    if (string.Compare(WorldMonitor.Instance.TrackedObjectAffiliations[TrackedObjectID], sPass[0]) != 0)
                    {
                        if (cem == ConflictEndMode.OnAllConflictsEnded && insiders > 0)
                            return;
                        // else: both OnAll and OnIndividual modes will be handled the same from here out

                        foreach (WorldMonitors wm in wms)
                            wm.RaiseConflictLeavers(gameObject, goPass, sPass);
                    }
                }
            }
        }
    }
}