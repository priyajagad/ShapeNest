using UnityEngine;
using UnityEditor;
using StarterKit.StateMachine;

[CustomEditor(typeof(StateMachineConfig))]
public class StateMachineConfigEditor : Editor
{
    SerializedProperty stateNamesProperty;

    void OnEnable()
    {
        stateNamesProperty = serializedObject.FindProperty("stateNames");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("State Machine Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(stateNamesProperty, true);

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Create State Scripts"))
        {
            CreateStateScripts();
        }

        if (GUILayout.Button("Setup Hierarchy"))
        {
            SetupHierarchy();
        }
    }

    void CreateStateScripts()
    {
        StateMachineConfig config = (StateMachineConfig)target;
        if (config == null)
        {
            Debug.LogError("No StateMachineConfig found.");
            return;
        }

        foreach (string stateName in config.stateNames)
        {
            string template = @"
using UnityEngine;

namespace StarterKit.StateMachine
{
    public class #STATE_NAME# : BaseState
    {
        public override void Enter()
        {
            Debug.Log(""Entering #STATE_NAME#"");
            // Additional logic for entering this state
        }

        public override void Exit()
        {
            Debug.Log(""Exiting #STATE_NAME#"");
            // Additional logic for exiting this state
        }
    }
}
";
            template = template.Replace("#STATE_NAME#", stateName);

            string folderPath = Application.dataPath + "/Scripts/StateMachine/";
            string filePath = folderPath + stateName + ".cs";

            if (!System.IO.Directory.Exists(folderPath))
            {
                System.IO.Directory.CreateDirectory(folderPath);
            }

            System.IO.File.WriteAllText(filePath, template);
            AssetDatabase.Refresh();
        }

        Debug.Log("State scripts created successfully.");
    }

    void SetupHierarchy()
    {
        StateMachineConfig config = (StateMachineConfig)target;
        if (config == null)
        {
            Debug.LogError("No StateMachineConfig found.");
            return;
        }

        GameObject controllerObject = new GameObject("FSMController");
        FSMController fsmController = controllerObject.AddComponent<FSMController>();

        foreach (string stateName in config.stateNames)
        {
            GameObject stateObject = new GameObject(stateName);
            stateObject.transform.parent = controllerObject.transform;
            BaseState stateComponent = stateObject.AddComponent(System.Type.GetType("StarterKit.StateMachine." + stateName)) as BaseState;
        }

        Debug.Log("Hierarchy setup completed.");
    }
}
