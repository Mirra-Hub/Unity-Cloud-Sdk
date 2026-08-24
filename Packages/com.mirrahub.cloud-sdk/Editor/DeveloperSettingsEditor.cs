using UnityEditor;
using UnityEngine;

namespace MirraCloud.Editor
{
    [CustomEditor(typeof(DeveloperSettings))]
    public class DeveloperSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _environments;
        private SerializedProperty _selectedEnvironment;

        private void OnEnable()
        {
            _environments = serializedObject.FindProperty("Environments");
            _selectedEnvironment = serializedObject.FindProperty("SelectedEnvironment");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_environments, new GUIContent("Environments"), true);
            EditorGUILayout.Space(8);

            var names = new string[_environments.arraySize + 1];
            names[0] = "Production";
            for (int i = 0; i < _environments.arraySize; i++)
            {
                var element = _environments.GetArrayElementAtIndex(i);
                var nameProp = element.FindPropertyRelative("Name");
                names[i + 1] = string.IsNullOrEmpty(nameProp.stringValue) ? $"Environment {i}" : nameProp.stringValue;
            }

            _selectedEnvironment.intValue = EditorGUILayout.Popup("Active Environment", _selectedEnvironment.intValue, names);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
