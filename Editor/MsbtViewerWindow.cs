using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using DivineDragon.Msbt;

namespace DivineDragon.Msbt.Editor
{
    public class MsbtViewer : EditorWindow
    {
        private MessageBundle messageBundle;
        private Dictionary<string, string> textEntries;
        private string[] labels;
        private int selectedLabelIndex = -1;
        private Vector2 leftScrollPosition;
        private Vector2 rightScrollPosition;
        private string loadedFilePath = "";

        [MenuItem("Divine Dragon/MSBT Viewer")]
        public static void ShowWindow()
        {
            GetWindow<MsbtViewer>("MSBT Viewer");
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("MSBT Viewer", EditorStyles.boldLabel);
            if (GUILayout.Button("Load MSBT File", GUILayout.Width(120)))
            {
                LoadMsbtFile();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(loadedFilePath))
            {
                EditorGUILayout.LabelField("Loaded: " + loadedFilePath, EditorStyles.miniLabel);
            }

            if (messageBundle != null && labels != null)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.BeginVertical(GUILayout.Width(200));
                GUILayout.Label("Messages", EditorStyles.boldLabel);
                leftScrollPosition = EditorGUILayout.BeginScrollView(leftScrollPosition);

                for (int i = 0; i < labels.Length; i++)
                {
                    bool isSelected = i == selectedLabelIndex;
                    Color originalColor = GUI.backgroundColor;
                    
                    if (isSelected)
                    {
                        GUI.backgroundColor = Color.gray;
                    }

                    if (GUILayout.Button(labels[i], GUILayout.ExpandWidth(true)))
                    {
                        selectedLabelIndex = i;
                    }

                    GUI.backgroundColor = originalColor;
                }

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                GUILayout.Label("Content", EditorStyles.boldLabel);
                rightScrollPosition = EditorGUILayout.BeginScrollView(rightScrollPosition);

                if (selectedLabelIndex >= 0 && selectedLabelIndex < labels.Length)
                {
                    string selectedLabel = labels[selectedLabelIndex];
                    if (textEntries.TryGetValue(selectedLabel, out string content))
                    {
                        EditorGUILayout.TextArea(content, GUILayout.ExpandHeight(true));
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Select a message to view its content");
                }

                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("No MSBT file loaded. Click 'Load MSBT File' to begin.");
            }

            EditorGUILayout.EndVertical();
        }

        private void LoadMsbtFile()
        {
            string path = EditorUtility.OpenFilePanel("Select MSBT File", "", "bytes");
            
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    messageBundle = MessageBundle.Load(path);
                    textEntries = messageBundle.ExtractEntriesAsText();
                    labels = textEntries.Keys.ToArray();
                    selectedLabelIndex = labels.Length > 0 ? 0 : -1;
                    loadedFilePath = System.IO.Path.GetFileName(path);
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Error", "Failed to load MSBT file: " + e.Message, "OK");
                    messageBundle = null;
                    textEntries = null;
                    labels = null;
                    selectedLabelIndex = -1;
                    loadedFilePath = "";
                }
            }
        }
    }
}