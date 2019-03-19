using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using oti.Editors;

namespace oti.AI
{
    public class WorldMonitors : MonoBehaviour
    {
        /// <summary>
        /// User defined list of GameObject to track
        /// </summary>
        public List<TrackedObjectContainer> TrackedObjects = new List<TrackedObjectContainer>();

        /// <summary>
        /// User defined area for defining close objects
        /// </summary>
        public List<float> ThresholdSet = new List<float>();

        /// <summary>
        /// Delegate for tracked object conflicts.
        /// </summary>
        /// <param name="conflictingTypes">The types of objects as defined in WorldMonitors</param>
        /// <param name="conflictOrigins">The positions of the objects causing the conflict</param>
        public delegate void ObjectConflictRaiser(GameObject objectWithConflict, List<GameObject> conflictingObjects = default(List<GameObject>), List<string> conflictingTypes = default(List<string>));

        /// <summary>
        /// Event to raise awareness to listeners of the conflict
        /// </summary>
        public event ObjectConflictRaiser Conflicts;

        //Provide WorldMonitor a method to raise event from
        public void RaiseConflicts(GameObject objectWithConflict, List<GameObject> conflictingObjects, List<string> conflictingTypes)
        {
            Conflicts?.Invoke(objectWithConflict, conflictingObjects, conflictingTypes);
        }

        //Provide WorldMonitor a method to raise event from
        public void EndConflicts(GameObject objectWithEndedConflict)
        {
            Conflicts?.Invoke(objectWithEndedConflict);
        }

        private void Start()
        {
            //if user hasn't created a GameObject with WorldMonitor singleton
            if (!WorldMonitor.Instance)
            {
                new GameObject(gameObject.name + "_WMContainer", typeof(WorldMonitor));
            }
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(WorldMonitors))]
    public class WorldMonitorsEditor : OTIEditorBase
    {
        /// <summary>
        /// Number of distinct tracked object fields
        /// </summary>
        private int numberTrackedFields;

        /// <summary>
        /// A limit imposed for performance.
        /// </summary>
        private int maxNumberTrackedFields;

        /// <summary>
        /// Used to determine if field inspector should be exposed
        /// </summary>
        bool[] show = new bool[26]; //limit 26 sets of tracked objects

        /// <summary>
        /// Access to components in WorldMonitors
        /// </summary>
        WorldMonitors instance;

        private void OnEnable()
        {
            instance = target as WorldMonitors;

            maxNumberTrackedFields = show.Length;
            numberTrackedFields = Mathf.Max(1, numberTrackedFields);

            if (instance.TrackedObjects.Count == 0)
                instance.TrackedObjects.Add(new TrackedObjectContainer());

            base.Enable();
            if (!OTIEditorUtility.Instance)
            {
                OTIEditorUtility AIE = ScriptableObject.CreateInstance<OTIEditorUtility>();
                OTIEditorUtility.Instance = AIE;
            }

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            OTIEditorUtility.Instance.OTIHead("Agent World Monitoring", _WorldMonitoringExplanation);
            OTIEditorUtility.Instance.HorizontalLine();

            numberTrackedFields = Mathf.Max(1, instance.TrackedObjects.Count);

            if (numberTrackedFields >= maxNumberTrackedFields)
                Debug.LogError(OTIEditorBase._ExplanationOfTrackedObjectLimit);            

            if (instance.TrackedObjects.Count == 0)
                instance.TrackedObjects.Add(new TrackedObjectContainer());

            if (instance.ThresholdSet.Count == 0)
                instance.ThresholdSet.Add(new float());

            addMinusIntValueButtons(ref numberTrackedFields, ref instance.TrackedObjects, ref instance.ThresholdSet, "Add Tracked Field", "Remove Tracked Field", maxNumber: maxNumberTrackedFields);

            OTIEditorUtility.Instance.HorizontalLineProperty(ref instance.ThresholdSet, 0, "Tracked Object Set A", "Threshold Distance A", "Assign objects for tracking against each other. Set A will be tracked against all others.");
            trackedObjectListManager(0, guiContent, headingStyle, subHeadingStyle, "", "Tracked Objects", ref show[0], padding: 1);

            for (int i = 1; i < numberTrackedFields; i++)
            {
                OTIEditorUtility.Instance.HorizontalLineProperty(ref instance.ThresholdSet, i, "Tracked Object Set " + OTIEditorBase._Alphabetic[i], "Threshold Distance " + OTIEditorBase._Alphabetic[i], "Assign objects for tracking against each other. Set " + OTIEditorBase._Alphabetic[i] + " will be tracked against all others.");
                trackedObjectListManager(i, guiContent, headingStyle, subHeadingStyle, "", "Tracked Objects", ref show[i], padding: 1);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Similar to method in AIEditor class but modified for WorldMonitors.
        /// </summary>
        /// <param name="i">Current index of TrackedObjects list.</param>
        private void trackedObjectListManager(int i, GUIContent guiText, GUIStyle headingStyle, GUIStyle subHeadingStyle, string typeOfFactor, string head, ref bool showField, int padding = default(int))
        {
            Rect plotSpace = new Rect();
            plotSpace = GUILayoutUtility.GetLastRect();

            float viewWidth = EditorGUIUtility.currentViewWidth;
            plotSpace = new Rect(0, plotSpace.yMax + 2, 500, 1000);

            GUIStyle gs = headingStyle;
            Color ogc = gs.normal.textColor;
            gs.alignment = TextAnchor.MiddleLeft;

            EditorGUILayout.BeginHorizontal();

            string buttonDisp = showField ? "-" : "+";
            bool input = GUILayout.Button(buttonDisp, EditorStyles.miniButton, GUILayout.Width(42));
            EditorGUILayout.LabelField(head, subHeadingStyle);
            EditorGUILayout.EndHorizontal();

            if (input && showField)
            {
                showField = false;
                EditorGUILayout.EndHorizontal();
            }
            else if (input || showField)
            {
                showField = true;
                GUILayoutOption miniButtonWidth = GUILayout.Width(viewWidth / 2 - 20);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Add " + typeOfFactor, EditorStyles.miniButton, miniButtonWidth))
                {
                    instance.TrackedObjects[i].TrackedObjects.Add(null);
                }

                if (GUILayout.Button("Delete " + typeOfFactor, EditorStyles.miniButton, miniButtonWidth))
                {
                    instance.TrackedObjects[i].TrackedObjects.RemoveAt(instance.TrackedObjects[i].TrackedObjects.Count - 1);
                }

                EditorGUILayout.EndHorizontal();
                for(int j = 0; j < instance.TrackedObjects[i].TrackedObjects.Count; j++)
                {
                    EditorGUILayout.BeginHorizontal();
                    instance.TrackedObjects[i].TrackedObjects[j] = (GameObject)EditorGUILayout.ObjectField(instance.TrackedObjects[i].TrackedObjects[j], typeof(GameObject), true);//EditorGUILayout.PropertyField(so, new GUIContent(typeOfFactor), true);
                    EditorGUILayout.EndHorizontal();
                }
            }

            gs.normal.textColor = ogc;
            gs.alignment = TextAnchor.MiddleCenter;
            spaces(numberOfSpaces: padding);
        }

        /// <summary>
        /// Similar to method in AIEditor class but modified for WorldMonitors.
        /// </summary>
        private void addMinusIntValueButtons(ref int intComponent, ref List<TrackedObjectContainer> p1, ref List<float> p2, string addButtonLabel = default(string), string minusButtonLabel = default(string), int minNumber = 1, int maxNumber = 4)
        {
            if (intComponent == maxNumber)
                return;

            addButtonLabel = addButtonLabel == default(string) ? "+" : addButtonLabel;
            minusButtonLabel = minusButtonLabel == default(string) ? "-" : minusButtonLabel;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(addButtonLabel, EditorStyles.miniButtonLeft, GUILayout.Width(142)))
            {
                p1.Add(new TrackedObjectContainer());
                p2.Add(new float());
                intComponent += 1;
            }

            if (GUILayout.Button(minusButtonLabel, EditorStyles.miniButtonRight, GUILayout.Width(142)) && intComponent != minNumber - 1)
            {
                p1.RemoveAt(p1.Count - 1);
                p2.RemoveAt(p2.Count - 1);
                intComponent -= 1;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    [System.Serializable]
    public class TrackedObjectContainer
    {
        public List<GameObject> TrackedObjects = new List<GameObject>();
    }
}