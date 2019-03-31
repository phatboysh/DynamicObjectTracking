using UnityEngine;

namespace oti.AI
{
    /// <summary>
    /// Moves tracked objects in Dynamic Objects Tracking example scene
    /// </summary>
    public class ExampleNavigator : MonoBehaviour
    {
        /// <summary>
        /// Setting false will allow user to investigate collision reporting on their own accord
        /// </summary>
        public bool MoveAuto;

        /// <summary>
        /// Speed of translation
        /// </summary>
        public float Speed = 5;

        /// <summary>
        /// Max speed an object can reach after collision.
        /// </summary>
        public float MaxSpeed = 20;

        /// <summary>
        /// represents length of size of box used for navigation
        /// </summary>
        private float boundingBoxScale = 50;

        /// <summary>
        /// direction of travel
        /// </summary>
        private Vector3 direction = Vector3.forward;

        /// <summary>
        /// Corrals objects that can leave bounds in Update
        /// </summary>
        private bool escapedBounds;

        void Start()
        {
            if (!MoveAuto)
            {
                Speed = 0;
                return;
            }

            direction = new Vector3(Random.Range(-1, 1), 0, Random.Range(-1, 1));

            // ensure all objects move with some velocity >> 0
            float xMin = Mathf.Max(Mathf.Abs(direction.x), 0.5f) * Mathf.Sign(direction.x);
            float zMin = Mathf.Max(Mathf.Abs(direction.z), 0.5f) * Mathf.Sign(direction.z);

            direction = new Vector3(xMin, 0, zMin);
        }

        /// <summary>
        /// Limit gizmos to one component
        /// </summary>
        public static bool _GizmosDrawn;

        /// <summary>
        /// Method called from subscriber in Tracker
        /// </summary>
        public void ObjectConflict(Vector3 positionOfInitiator)
        {
            float fr = Random.Range(-0.01f, 0.01f);
            //do something with the information -- note that object affiliation and all conflicting objects and types are available in the event
            direction = Vector3.Cross(direction + new Vector3(fr,fr,fr), positionOfInitiator - transform.position).normalized; // jump into 3 dimensions
        }

        void Update()
        {
            if (!MoveAuto)
                return;

            bool xCrossed = Mathf.Abs(transform.position.x) > boundingBoxScale;
            bool zCrossed = Mathf.Abs(transform.position.z) > boundingBoxScale;
            bool yCrossed = Mathf.Abs(transform.position.y) > boundingBoxScale;

            if (escapedBounds && (xCrossed || zCrossed || yCrossed)) //object has been outside of bounds for more than one frame so force it back
            {
                float signX = Mathf.Sign(transform.position.x);
                float signY = Mathf.Sign(transform.position.z);
                float signZ = Mathf.Sign(transform.position.z);

                transform.position = xCrossed ? new Vector3(signX * boundingBoxScale * 0.9975f, transform.position.y, transform.position.z) : transform.position;
                transform.position = yCrossed ? new Vector3(transform.position.x, signY * boundingBoxScale * 0.9975f, transform.position.z) : transform.position;
                transform.position = zCrossed ? new Vector3(transform.position.x, transform.position.y, signZ * boundingBoxScale * 0.9975f) : transform.position;

                escapedBounds = false;
            }
            else
            {
                escapedBounds = xCrossed || zCrossed || yCrossed;
            }

            direction = xCrossed ? new Vector3(-direction.x, direction.y, direction.z) : direction;
            direction = zCrossed ? new Vector3(direction.x, direction.y, -direction.z) : direction;
            direction = yCrossed ? new Vector3(direction.x, -direction.y, direction.z) : direction;
                       

            transform.Translate(Speed * direction * Time.deltaTime, Space.World);

            if (Speed == MaxSpeed)
                Speed = 5/2;
        }
    }
}