using UnityEditor;
using UnityEngine;

namespace NHSRemont.Utility
{
    public class ReadOnlyInEditorAttribute : PropertyAttribute
    {
 
    }
 
    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(ReadOnlyInEditorAttribute))]
    public class ReadOnlyInEditorDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property,
            GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
 
        public override void OnGUI(Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
    #endif
}