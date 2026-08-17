using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text.RegularExpressions;

namespace StarterKit.EditorUtils
{
    public class ScriptGeneratorWindow : EditorWindow
    {
        private string codeText = "";
        private string folderPath = "Assets/GeneratedScripts";


        [MenuItem("+ Editor/Script Generator", false, 102)]
        public static void ShowWindow()
        {
            GetWindow<ScriptGeneratorWindow>("Code Generator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Paste Code:", EditorStyles.boldLabel);
            codeText = EditorGUILayout.TextArea(codeText, GUILayout.Height(100));

            GUILayout.Space(10);

            GUILayout.Label("Select Folder:", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            folderPath = EditorGUILayout.TextField(folderPath);
            if (GUILayout.Button("Select Folder", GUILayout.Width(100)))
            {
                folderPath = EditorUtility.OpenFolderPanel("Select Folder", folderPath, "");
            
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("Generate Script"))
            {
                GenerateScript();
            }
        }

        private void GenerateScript()
        {
            if (!Directory.Exists(this.folderPath))
            {
                ShowDialog("Invalid Path", "Please select a valid path");
                return;
            }

            // Check if the codeText is not empty
            if (string.IsNullOrEmpty(codeText))
            {
                ShowDialog("Blank Code Field", "Code is empty. Please paste valid code.");
                return;
            }

            // Use regular expressions to extract code elements (classes, enums, structs, interfaces)
            string codeElementPattern = @"(class|enum|struct|interface)\s+(\w+)";
            var codeElementMatches = Regex.Matches(codeText, codeElementPattern);

            if (codeElementMatches.Count == 0)
            {
                ShowDialog("No Code Elements Found", "No code elements (class, enum, struct, interface) found in the code.");
                return;
            }

            foreach (Match codeElementMatch in codeElementMatches)
            {
                string elementType = codeElementMatch.Groups[1].Value;
                string elementName = codeElementMatch.Groups[2].Value;

                if (string.IsNullOrEmpty(elementType) || string.IsNullOrEmpty(elementName))
                {
                    ShowDialog("Parsing Failed", "Unable to extract element type or name. Please check your code.");
                    return;
                }

                // Extract the code for this element
                string elementCodePattern = $@"{elementType}\s+{elementName}[^{{]*{{";
                var elementCodeMatch = Regex.Match(codeText, elementCodePattern);

                if (elementCodeMatch.Success)
                {
                    string elementCode = codeText;

                    // Write the element code to a file
                    string scriptPath = folderPath + $"/{elementName}.cs";
                    File.WriteAllText(scriptPath, elementCode);

                    AssetDatabase.Refresh();

                    ShowDialog("Script Generated", $"Script generated for {elementType} '{elementName}' at: {scriptPath}");
                }
                else
                {
                    ShowDialog("Code Extraction Failed", $"Failed to extract code for {elementType} '{elementName}'. Please check your code.");
                }
            }

            codeText = "";
        }

        private void ShowDialog(string title, string message)
        {
            EditorUtility.DisplayDialog(title, message, "OK");
        }
    }
}
