// ============================================================================
// Nightflow - Editor Input Dialog
// Simple input dialog for getting text input in the editor
// ============================================================================

using UnityEngine;
using UnityEditor;

namespace Nightflow.Editor
{
    /// <summary>
    /// Simple input dialog for getting text input from the user in the editor.
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string inputText = "";
        private string message = "";
        private bool confirmed = false;
        private bool initialized = false;

        private static string result = null;

        /// <summary>
        /// Shows an input dialog and returns the entered text, or null if cancelled.
        /// </summary>
        public static string Show(string title, string message, string defaultValue = "")
        {
            result = null;

            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window.message = message;
            window.inputText = defaultValue;
            window.minSize = new Vector2(350, 120);
            window.maxSize = new Vector2(500, 120);

            // Center on screen
            var position = window.position;
            position.x = (Screen.currentResolution.width - position.width) / 2;
            position.y = (Screen.currentResolution.height - position.height) / 2;
            window.position = position;

            window.ShowModalUtility();

            return result;
        }

        private void OnGUI()
        {
            // Focus the text field on first frame
            if (!initialized)
            {
                initialized = true;
                EditorGUI.FocusTextInControl("InputField");
            }

            GUILayout.Space(10);

            // Message
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);

            // Input field
            GUI.SetNextControlName("InputField");
            inputText = EditorGUILayout.TextField(inputText);

            GUILayout.Space(10);

            // Handle Enter key
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Confirm();
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Escape)
                {
                    Cancel();
                    e.Use();
                }
            }

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                Cancel();
            }

            if (GUILayout.Button("Create", GUILayout.Width(80)))
            {
                Confirm();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void Confirm()
        {
            if (!string.IsNullOrWhiteSpace(inputText))
            {
                result = inputText.Trim();
                confirmed = true;
                Close();
            }
        }

        private void Cancel()
        {
            result = null;
            Close();
        }
    }
}
