using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace StarterKit.EditorUtils
{

    public class EditorUtilities : MonoBehaviour
    {
        [MenuItem("+ Project/Create Essential Folders")]
        public static void SetupProject()
        {
            string ParentFolder = "Project";

            AssetDatabase.CreateFolder("Assets", ParentFolder);

            List<string> Folders = new List<string> { "Scripts", "Scenes", "3D Assets", "2D Assets", "UI", "Sounds", "Prefabs" };

            foreach (var folder in Folders)
            {
                AssetDatabase.CreateFolder(Path.Combine("Assets", ParentFolder), folder);
            }
        }

        public void CreateFolder(string Parent, string name)
        {

        }
    }

}