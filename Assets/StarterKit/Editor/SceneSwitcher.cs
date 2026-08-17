using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarterKit.EditorUtils
{

    public class SceneSwitcher : EditorWindow
    {
        private Vector2 scrollPos;

        [MenuItem("+ Editor/Scene Switcher", false, 102)]
        internal static void Init()
        {

            var window = (SceneSwitcher)GetWindow(typeof(SceneSwitcher), false, "Scene Switcher");
            window.position = new Rect(window.position.xMin + 100f, window.position.yMin + 100f, 200f, 100f);
        }

        void OnGUI()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            this.scrollPos = EditorGUILayout.BeginScrollView(this.scrollPos, false, false);

            GUILayout.Label("Scenes In Build", EditorStyles.boldLabel);
            for (var i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                var scene = EditorBuildSettings.scenes[i];
                if (scene.enabled)
                {
                    var sceneName = Path.GetFileNameWithoutExtension(scene.path);
                    var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    var pressed = GUILayout.Button(i + ": " + sceneName, new GUIStyle(GUI.skin.GetStyle("Button")) { alignment = TextAnchor.MiddleLeft });
                    if (pressed && activeScene.name != sceneName)
                    {
                        if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene.path);
                        }
                    }
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }
    }
}