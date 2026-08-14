using System.Collections.Generic;
using UnityEngine;

internal class LevelEditorSession : ScriptableObject
{
    public string levelName = "Level1";
    public int columns = 5;
    public int rows = 5;
    public bool isDirty;
    public LevelData sourceAsset;
    public List<LevelBlockData> blocks = new List<LevelBlockData>();
    public List<LevelTargetData> targets = new List<LevelTargetData>();

    public void ResetNew(string nextName, int boardColumns, int boardRows)
    {
        levelName = nextName;
        columns = Mathf.Max(1, boardColumns);
        rows = Mathf.Max(1, boardRows);
        isDirty = false;
        sourceAsset = null;
        blocks.Clear();
        targets.Clear();
    }

    public LevelBlockData FindBlock(Vector2Int cell)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            LevelBlockData block = blocks[i];
            if (block != null && block.gridPosition == cell)
            {
                return block;
            }
        }

        return null;
    }

    public LevelTargetData FindTarget(Vector2Int cell)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            LevelTargetData target = targets[i];
            if (target != null && target.gridPosition == cell)
            {
                return target;
            }
        }

        return null;
    }

    public bool RemoveBlockAt(Vector2Int cell)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            LevelBlockData block = blocks[i];
            if (block != null && block.gridPosition == cell)
            {
                blocks.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool RemoveTargetAt(Vector2Int cell)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            LevelTargetData target = targets[i];
            if (target != null && target.gridPosition == cell)
            {
                targets.RemoveAt(i);
                return true;
            }
        }

        return false;
    }
}
