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
    public List<LevelShutterData> shutters = new List<LevelShutterData>();

    public void ResetNew(string nextName, int boardColumns, int boardRows)
    {
        levelName = nextName;
        columns = Mathf.Max(1, boardColumns);
        rows = Mathf.Max(1, boardRows);
        isDirty = false;
        sourceAsset = null;
        blocks.Clear();
        targets.Clear();
        shutters.Clear();
    }

    public LevelBlockData FindBlock(Vector2Int cell)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            LevelBlockData block = blocks[i];
            if (block != null && ShapeLayout.OccupiesWorldCell(block.gridPosition, block.cells, cell))
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
            if (target != null && ShapeLayout.OccupiesWorldCell(target.gridPosition, target.cells, cell))
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
            if (block != null && ShapeLayout.OccupiesWorldCell(block.gridPosition, block.cells, cell))
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
            if (target != null && ShapeLayout.OccupiesWorldCell(target.gridPosition, target.cells, cell))
            {
                targets.RemoveAt(i);
                return true;
            }
        }

        return false;
    }
    public LevelShutterData FindShutter(Vector2Int cell)
    {
        for (int i = 0; i < shutters.Count; i++)
        {
            LevelShutterData shutter = shutters[i];
            if (shutter != null && shutter.cells != null && shutter.cells.Contains(cell))
            {
                return shutter;
            }
        }

        return null;
    }

    public bool RemoveShutterAt(Vector2Int cell)
    {
        for (int i = 0; i < shutters.Count; i++)
        {
            LevelShutterData shutter = shutters[i];
            if (shutter != null && shutter.cells != null && shutter.cells.Contains(cell))
            {
                shutters.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

}
