namespace oti.AI
{
    using UnityEngine;
    using System.Threading;
    using System.Collections.Generic;

    public class OctreeThreadParameters
    {
        public int TotalTrackedObjects;
        public List<int> ObjectIDs;
        public Dictionary<int, KeyValuePair<float, Vector3>> Coordinates;
        public Dictionary<int, string> DynamicObjects;

    }

    public class OTIJob
    {
        private bool JobIsDone = false;

        private readonly object JobHandle = new object();

        private Thread JobThread = null;
        
        public bool IsDone
        {
            get
            {
                bool tmp;
                lock (JobHandle)
                {
                    tmp = JobIsDone;
                }
                return tmp;
            }
            set
            {
                lock (JobHandle)
                {
                    JobIsDone = value;
                }
            }
        }

        public virtual void StartOctree(OctreeThreadParameters otp)
        {
            JobThread = new Thread(new ParameterizedThreadStart(runOctree));
            JobThread.Start(otp);
        }

        public virtual void Abort()
        {
            JobThread?.Abort();
        }

        protected virtual void ThreadOctree(OctreeThreadParameters otp) { }

        protected virtual void OctreeThreadFinished() { }

        public virtual bool UpdateOctree()
        {
            if (IsDone)
            {
                OctreeThreadFinished();
                return true;
            }
            return false;
        }

        private void runOctree(object otp)
        {
            ThreadOctree((OctreeThreadParameters)otp);
        }
    }
}

