using UnityEngine;

namespace oti.AI
{
    /// <summary>
    /// Moves tracked objects in Dynamic Objects Tracking example scene
    /// </summary>
    public class ExampleNavigator : MonoBehaviour
    { 
        /// <summary>
        /// represents length of size of box used for navigation
        /// </summary>
        private float boundingBoxLength = 50;

        /// <summary>
        /// direction of travel
        /// </summary>
        private Vector3 direction = Vector3.forward;

        /// <summary>
        /// velocity for transation
        /// </summary>
        private float velocity;

        /// <summary>
        /// Corrals objects that can leave bounds in Update
        /// </summary>
        private bool escapedBounds;

        void Start()
        {
            velocity = Random.Range(1, 15);
            direction = new Vector3(Random.Range(-1, 1), 0, Random.Range(-1, 1));
        }

        /// <summary>
        /// Limit gizmos to one component
        /// </summary>
        public static bool _GizmosDrawn;

        /// <summary>
        /// indicates if this component is the gizmos drawer
        /// </summary>
        private bool drawer;

        /// <summary>
        /// Method called from subscriber in Tracker
        /// </summary>
        public void ObjectConflict(Vector3 positionOfConflictor)
        {
            //do something with the information -- note that object affiliation and all conflicting objects and types are available in the event
            direction += new Vector3(Random.Range(-0.25f, 0.25f), 0, Random.Range(-0.25f, 0.25f));
        }

        void Update()
        {
            bool xCrossed = Mathf.Abs(transform.position.x) > boundingBoxLength;
            bool zCrossed = Mathf.Abs(transform.position.z) > boundingBoxLength;

            if (escapedBounds && (xCrossed || zCrossed)) //object has been outside of bounds for more than one frame so center
            {
                transform.position = Vector3.zero;
                escapedBounds = false;
            }
            else
            {
                escapedBounds = xCrossed || zCrossed;
            }

            direction = xCrossed ? new Vector3(-direction.x, 0, direction.z) : direction;
            direction = zCrossed ? new Vector3(direction.x, 0, -direction.z) : direction;

            transform.Translate(velocity * direction * Time.deltaTime, Space.World);
        }
    }
}