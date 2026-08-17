using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class ScreenshotTool : EditorWindow
{
    private string outputFolder = "Screenshots";
    private List<Texture2D> screenshots = new List<Texture2D>();
    private Vector2 scrollPosition;

    [MenuItem("+ Editor/Screenshot Utility", false, 103)]
    public static void ShowWindow()
    {
        GetWindow<ScreenshotTool>("Screenshot Tool");
    }

    private void OnEnable()
    {
        LoadScreenshots();
    }

    private void OnGUI()
    {
        GUILayout.Label("Screenshot Tool", EditorStyles.boldLabel);

        if (!EditorApplication.isPlaying)
        {
            // Output folder selection
            GUILayout.Label("Output Folder:");
            GUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField(outputFolder);
            if (GUILayout.Button("Browse"))
            {
                string path = EditorUtility.OpenFolderPanel("Select Output Folder", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    outputFolder = path;
                }
            }
            GUILayout.EndHorizontal();

            // Start button
            GUILayout.Space(10);
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Start Game", GUILayout.Height(50)))
            {
                StartGame();
            }

            // Clear folder button
            GUILayout.Space(10);
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Clear Current Folder", GUILayout.Height(30)))
            {
                ClearCurrentFolder();
                LoadScreenshots();
            }
        }
        else
        {
            // Capture button
            GUILayout.Space(10);
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Capture Screenshot", GUILayout.Height(100)))
            {
                CaptureScreenshot();
            }

            // Image previews
            GUILayout.Space(10);
            GUILayout.Label("Captured Screenshots:");
            GUILayout.Label($"Number of Captured Screenshots: {screenshots.Count}");
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
            GUILayout.BeginHorizontal();
            foreach (var screenshot in screenshots)
            {
                GUILayout.BeginVertical();
                GUILayout.Label(new GUIContent(screenshot), GUILayout.Width(300), GUILayout.Height(300));
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        // Reset the background color
        GUI.backgroundColor = Color.white;
    }

    private void StartGame()
    {
        EditorApplication.isPlaying = true;
    }

    private void CaptureScreenshot()
    {
        string folderPath = outputFolder;
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = $"Screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = Path.Combine(folderPath, fileName);
        ScreenCapture.CaptureScreenshot(filePath);
        EditorApplication.delayCall += () => LoadScreenshots();
    }

    private void LoadScreenshots()
    {
        screenshots.Clear();
        string folderPath = outputFolder;
        if (Directory.Exists(folderPath))
        {
            foreach (var filePath in Directory.GetFiles(folderPath, "*.png"))
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(fileData);
                screenshots.Add(tex);
            }
        }
    }

    private void ClearCurrentFolder()
    {
        string folderPath = outputFolder;
        if (Directory.Exists(folderPath))
        {
            var files = Directory.GetFiles(folderPath, "*.png");
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }
        LoadScreenshots();
    }
}
