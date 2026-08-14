using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelValidator))]
public class LevelValidatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        LevelValidator validator = (LevelValidator)target;

        EditorGUILayout.Space();

        if (GUILayout.Button("Validate Current Level"))
        {
            LevelData level = serializedObject.FindProperty("levelData").objectReferenceValue as LevelData;
            validator.ValidateLevel(level);
        }

        if (GUILayout.Button("Validate Level Database"))
        {
            validator.ValidateLevelDatabase();
        }
    }
}
