using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class LevelAssetUtility
{
    public const string LevelsFolder = "Assets/Levels";
    public const string DatabasePath = LevelsFolder + "/LevelDatabase.asset";

    public static LevelDatabase FindLevelDatabase()
    {
        LevelDatabase preferred = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
        if (preferred != null)
        {
            return preferred;
        }

        string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<LevelDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    public static LevelDatabase CreateLevelDatabase()
    {
        LevelDatabase existing = FindLevelDatabase();
        if (existing != null)
        {
            return existing;
        }

        EnsureLevelsFolder();
        LevelDatabase database = ScriptableObject.CreateInstance<LevelDatabase>();
        AssetDatabase.CreateAsset(database, DatabasePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return database;
    }

    public static void AddToLevelDatabase(LevelData level)
    {
        LevelDatabase database = FindLevelDatabase() ?? CreateLevelDatabase();
        if (database == null || level == null)
        {
            return;
        }

        var serialized = new SerializedObject(database);
        SerializedProperty levels = serialized.FindProperty("levels");
        if (levels == null)
        {
            Debug.LogError("Could not find the levels list on LevelDatabase.");
            return;
        }

        for (int i = 0; i < levels.arraySize; i++)
        {
            if (levels.GetArrayElementAtIndex(i).objectReferenceValue == level)
            {
                return;
            }
        }

        Undo.RecordObject(database, "Add Level to Database");
        int index = levels.arraySize;
        levels.arraySize++;
        levels.GetArrayElementAtIndex(index).objectReferenceValue = level;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
    }

    public static void EnsureLevelsFolder()
    {
        if (!AssetDatabase.IsValidFolder(LevelsFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Levels");
        }
    }

    public static bool AssetExists(string levelName)
    {
        return AssetDatabase.LoadAssetAtPath<LevelData>($"{LevelsFolder}/{levelName}.asset") != null;
    }

    public static string NextAvailableLevelName(int startNumber)
    {
        int number = Mathf.Max(1, startNumber);
        while (AssetExists("Level" + number))
        {
            number++;
        }

        return "Level" + number;
    }

    public static LevelData SaveLevelData(
        string levelName,
        IList<LevelBlockData> blocks,
        IList<LevelTargetData> targets,
        bool overwrite)
    {
        EnsureLevelsFolder();
        string path = $"{LevelsFolder}/{levelName}.asset";
        LevelData asset = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (asset != null && !overwrite)
        {
            return null;
        }

        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<LevelData>();
            asset.blocks = new List<LevelBlockData>();
            asset.targets = new List<LevelTargetData>();
            AssetDatabase.CreateAsset(asset, path);
        }

        Undo.RecordObject(asset, "Save Generated Level");
        if (asset.blocks == null)
        {
            asset.blocks = new List<LevelBlockData>();
        }

        if (asset.targets == null)
        {
            asset.targets = new List<LevelTargetData>();
        }

        CopyBlocks(blocks, asset.blocks);
        CopyTargets(targets, asset.targets);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddToLevelDatabase(asset);
        return asset;
    }

    private static void CopyBlocks(IList<LevelBlockData> source, List<LevelBlockData> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            LevelBlockData block = source[i];
            if (block == null)
            {
                continue;
            }

            destination.Add(new LevelBlockData
            {
                shapeType = block.shapeType,
                moveDirection = block.moveDirection,
                gridPosition = block.gridPosition
            });
        }
    }

    private static void CopyTargets(IList<LevelTargetData> source, List<LevelTargetData> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            LevelTargetData target = source[i];
            if (target == null)
            {
                continue;
            }

            destination.Add(new LevelTargetData
            {
                shapeType = target.shapeType,
                gridPosition = target.gridPosition
            });
        }
    }
}
