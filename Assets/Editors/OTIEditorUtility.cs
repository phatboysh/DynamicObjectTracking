using System;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using oti.Editors;

namespace oti.AI
{
    /// <summary>
    /// This class is part of a much larger repository, many of these methods are not used in this repo.
    /// Feel free to use them if you wish.
    /// </summary>
    public class OTIEditorUtility : Editor
    {
        /// <summary>
        /// Singleton implementation
        /// </summary>
        private static OTIEditorUtility _instance = null;

        public static OTIEditorUtility Instance
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

        public void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                DestroyImmediate(this);
                return;
            }
            if (headingStyle == null)
                setFormats();
        }

        public static void _Enable()
        {
            _instance = new OTIEditorUtility();
        }

        /// <summary>
        /// Style used for OTI headers 
        /// </summary>
        private GUIStyle headingStyle;

        /// <summary>
        /// Style used for inspector content
        /// </summary>
        private GUIStyle contentStyle;

        /// <summary>
        /// Places consistent format title on each OTI inspector.
        /// </summary>
        public void OTIHead(string title, string explanation, int padding = default(int))
        {
            spaces(2);

            if (headingStyle == null)
                setFormats();

            headingStyle.normal.textColor = OTIEditorFormat.OTINavy;
            Color ctemp = contentStyle.normal.textColor;
            contentStyle.normal.textColor = OTIEditorFormat.OTINavy;

            EditorGUILayout.LabelField("| " + title + " |", headingStyle);
            contentStyle.fontStyle = FontStyle.Italic;
            EditorGUILayout.LabelField(explanation, contentStyle);
            contentStyle.fontStyle = FontStyle.Normal;

            contentStyle.normal.textColor = ctemp;
            spaces(padding);
        }

        void setFormats()
        {
            headingStyle = new GUIStyle
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            contentStyle = new GUIStyle
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
        }

        private void spaces(int numberOfSpaces)
        {
            for(int i = 0; i < numberOfSpaces; i++)
                EditorGUILayout.Space();
        }

        /// <summary>
        /// Places a horizontal bar in editor.
        /// </summary>
        /// <param name="titleBar">(opt) Places bold text directly above bar.</param>
        /// <param name="appendedText">(opt) Helpful for elaborating on the title.</param>
        /// <param name="padding">(opt for title - default = 2)</param>
        public void HorizontalLine(string titleBar = default(string), string appendedText = default(string), int padding = 2)
        {
            spaces(2);
            GUIStyle TitleStyle = new GUIStyle
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerLeft
            };
            if(titleBar != default(string))
            {
                spaces(padding);
                EditorGUILayout.LabelField(titleBar, TitleStyle);
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            if(appendedText != default(string))
            {
                Color cTemp = contentStyle.normal.textColor;
                contentStyle.normal.textColor = OTIEditorFormat.OTINavy;
                EditorGUILayout.LabelField(appendedText, contentStyle);
                contentStyle.normal.textColor = cTemp;
                spaces(1);
            }
        }

        /// <summary>
        /// Places a horizontal bar in editor with a float property.
        /// </summary>
        /// <param name="titleBar">(opt) Places bold text directly above bar.</param>
        /// <param name="appendedText">(opt) Helpful for elaborating on the title.</param>
        /// <param name="padding">(opt for title - default = 2)</param>
        public void HorizontalLineProperty(ref List<float> components, int index, string titleBar = default(string), string fieldTitle = default(string), string appendedText = default(string), int padding = 2)
        {
            spaces(2);
            EditorGUIUtility.fieldWidth = 42;

            GUIStyle TitleStyle = new GUIStyle
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            if (titleBar != default(string))
            {
                spaces(padding);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(titleBar, TitleStyle);

                TitleStyle.alignment = TextAnchor.MiddleRight;
                TitleStyle.fontStyle = FontStyle.Normal;

                EditorGUILayout.PrefixLabel(fieldTitle, TitleStyle);
                components[index] = EditorGUILayout.FloatField(components[index]);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            if (appendedText != default(string))
            {
                Color cTemp = contentStyle.normal.textColor;
                contentStyle.normal.textColor = OTIEditorFormat.OTINavy;
                EditorGUILayout.LabelField(appendedText, contentStyle);
                contentStyle.normal.textColor = cTemp;
                spaces(1);
            }
        }

    }
}