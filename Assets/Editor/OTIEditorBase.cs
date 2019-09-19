using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using oti.AI;

namespace oti.Editors
{
    /// <summary>
    /// This class is part of a much larger repository, not all of this may be used here.
    /// Feel free to use them if you wish.
    /// </summary>
    public class OTIEditorBase : Editor
    {
        public void Enable()
        {
            if (!OTIEditorUtility.Instance)
            {
                OTIEditorUtility AIE = ScriptableObject.CreateInstance<OTIEditorUtility>();
                OTIEditorUtility.Instance = AIE;
            }

            EditorFormat = OTIEditorFormat._ReturnSet(this, "The component that factors into this behavior's cost during decision making");
            setFormats();
        }

        public void spaces(int numberOfSpaces)
        {
            for (int i = 0; i < numberOfSpaces; i++)
                EditorGUILayout.Space();
        }

        public OTIEditorFormat EditorFormat;
        public GUIStyle headingStyle;
        public GUIStyle subHeadingStyle;
        public GUIStyle guiStyle;
        public GUIStyle contentStyle;
        public GUIContent guiContent;
        public GUIContent gtSuccess;
        public GUIContent gtFailure;
        public GUIContent gtDestinations;
        
        void setFormats()
        {
            headingStyle = EditorFormat.HeaderFormat;
            subHeadingStyle = EditorFormat.SubHeaderFormat;
            contentStyle = EditorFormat.ContentStyle;

            guiStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };

            guiContent = new GUIContent
            {
                text = "   Costs",
                tooltip = "The component that factors into this behavior's cost during decision making"
            };

            gtSuccess = new GUIContent
            {
                text = "Successes",
            };

            gtFailure = new GUIContent
            {
                text = "  Failures",
            };

            gtDestinations = new GUIContent
            {
                text = "Destinations",
            };

            headingStyle.normal.textColor = OTIEditorFormat.OTINavy;
            contentStyle.normal.textColor = OTIEditorFormat.OTINavy;
        }

        public static string _WorldMonitoringExplanation = "Use this utility to give the agent awareness of events going on in the environment.";

        public static string _WorldMonitorSingleton = "The World Monitor: choose the parameters for your tracking system here.";
    }

    public struct OTIEditorFormat
    {
        public GUIStyle HeaderFormat;
        public GUIStyle SubHeaderFormat;
        public GUIStyle ContentStyle;
        public GUIContent GUIText;
        public static Color OTINavy = new Color(0, 0, 0.42f, 1);
        public static Color OTIOrange = new Color(1, 0.45f, 0, 1);

        public static OTIEditorFormat _ReturnSet(object sender, string _tooltip, int overrideFontSize = default(int))
        {
            OTIEditorFormat ef = new OTIEditorFormat();
            bool overFont = overrideFontSize != default(int);
            ef.HeaderFormat = new GUIStyle
            {
                fontSize = overFont ? overrideFontSize : 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            ef.SubHeaderFormat = new GUIStyle
            {
                fontSize = overFont ? overrideFontSize : 14,
                alignment = TextAnchor.LowerLeft,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            ef.ContentStyle = new GUIStyle
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            ef.GUIText = new GUIContent
            {
                text = sender.ToString(),
                tooltip = _tooltip
            };
            return ef;
        }
    }
}

