using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Development-only structural validation for LevelData. Does not spawn, load, or modify levels.
/// </summary>
public class LevelValidator : MonoBehaviour
{
    [SerializeField]
    private LevelData levelData;

    [SerializeField]
    private BoardManager boardManager;

    [SerializeField]
    private LevelDatabase levelDatabase;

    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();

    [ContextMenu("Validate Current Level")]
    public void ValidateCurrentLevel()
    {
        ValidateLevel(levelData);
    }

    [ContextMenu("Validate Level Database")]
    public void ValidateLevelDatabase()
    {
        if (levelDatabase == null)
        {
            Debug.LogError("LevelValidator: Level Database is not assigned.", this);
            return;
        }

        int total = levelDatabase.Count;
        if (total == 0)
        {
            Debug.LogWarning("LEVEL DATABASE VALIDATION\n-------------------------\nThe database is empty.", this);
            return;
        }

        int validCount = 0;
        int invalidCount = 0;
        var summary = new StringBuilder();
        summary.AppendLine("LEVEL DATABASE VALIDATION");
        summary.AppendLine("-------------------------");

        for (int i = 0; i < total; i++)
        {
            LevelData level = levelDatabase.GetLevel(i);
            bool valid = RunValidation(level);
            string levelName = level != null ? level.name : "(null)";

            if (valid)
            {
                validCount++;
                summary.AppendLine($"[{i}] {levelName}: VALID");
            }
            else
            {
                invalidCount++;
                summary.AppendLine($"[{i}] {levelName}: INVALID");
                LogCollectedMessages(level);
            }
        }

        summary.AppendLine();
        summary.AppendLine($"Total: {total}");
        summary.AppendLine($"Valid: {validCount}");
        summary.AppendLine($"Invalid: {invalidCount}");

        if (invalidCount > 0)
        {
            Debug.LogError(summary.ToString(), this);
        }
        else
        {
            Debug.Log(summary.ToString(), this);
        }
    }

    public bool ValidateLevel(LevelData data)
    {
        bool valid = RunValidation(data);
        LogCollectedMessages(data);
        return valid;
    }

    private bool RunValidation(LevelData data)
    {
        errors.Clear();
        warnings.Clear();

        if (data == null)
        {
            errors.Add("LevelData is not assigned (null).");
            return false;
        }

        IList<LevelBlockData> blocks = data.blocks;
        IList<LevelTargetData> targets = data.targets;
        int blockCount = blocks != null ? blocks.Count : 0;
        int targetCount = targets != null ? targets.Count : 0;

        if (blocks == null || blockCount == 0)
        {
            errors.Add($"{data.name}: The blocks list is empty. A level needs at least one block.");
        }

        if (targets == null || targetCount == 0)
        {
            errors.Add($"{data.name}: The targets list is empty. A level needs at least one target.");
        }

        if (blockCount != targetCount)
        {
            errors.Add(
                $"{data.name}: Block count ({blockCount}) does not match target count ({targetCount}). There must be one target per block.");
        }

        if (boardManager == null)
        {
            errors.Add("BoardManager is not assigned. Grid positions cannot be validated against the board.");
        }

        var blockPositions = new Dictionary<Vector2Int, int>();
        var targetPositions = new Dictionary<Vector2Int, int>();
        var blockShapeCounts = new Dictionary<ShapeType, int>();
        var targetShapeCounts = new Dictionary<ShapeType, int>();

        if (blocks != null)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                LevelBlockData block = blocks[i];
                if (block == null)
                {
                    errors.Add($"{data.name}: Block {i} is null/invalid.");
                    continue;
                }

                Vector2Int position = block.gridPosition;
                if (boardManager != null && !boardManager.IsInsideBoard(position))
                {
                    errors.Add(
                        $"{data.name}: Block {i} ({block.shapeType}) is outside the board at ({position.x},{position.y}).");
                }

                if (blockPositions.TryGetValue(position, out int existingIndex))
                {
                    errors.Add(
                        $"{data.name}: Block {existingIndex} and Block {i} use the same cell ({position.x},{position.y}).");
                }
                else
                {
                    blockPositions[position] = i;
                }

                if (!blockShapeCounts.ContainsKey(block.shapeType))
                {
                    blockShapeCounts[block.shapeType] = 0;
                }

                blockShapeCounts[block.shapeType]++;
            }
        }

        if (targets != null)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                LevelTargetData target = targets[i];
                if (target == null)
                {
                    errors.Add($"{data.name}: Target {i} is null/invalid.");
                    continue;
                }

                Vector2Int position = target.gridPosition;
                if (boardManager != null && !boardManager.IsInsideBoard(position))
                {
                    errors.Add(
                        $"{data.name}: Target {i} ({target.shapeType}) is outside the board at ({position.x},{position.y}).");
                }

                if (targetPositions.TryGetValue(position, out int existingIndex))
                {
                    errors.Add(
                        $"{data.name}: Target {existingIndex} and Target {i} use the same cell ({position.x},{position.y}).");
                }
                else
                {
                    targetPositions[position] = i;
                }

                if (!targetShapeCounts.ContainsKey(target.shapeType))
                {
                    targetShapeCounts[target.shapeType] = 0;
                }

                targetShapeCounts[target.shapeType]++;
            }
        }

        foreach (KeyValuePair<ShapeType, int> pair in blockShapeCounts)
        {
            if (!targetShapeCounts.ContainsKey(pair.Key))
            {
                errors.Add(
                    $"{data.name}: Blocks use ShapeType {pair.Key}, but no target of that shape exists.");
            }

            if (pair.Value > 1)
            {
                warnings.Add(
                    $"{data.name}: {pair.Value} blocks use ShapeType {pair.Key}. Multiple blocks of the same shape are allowed.");
            }
        }

        foreach (KeyValuePair<ShapeType, int> pair in targetShapeCounts)
        {
            if (!blockShapeCounts.ContainsKey(pair.Key))
            {
                errors.Add(
                    $"{data.name}: Targets use ShapeType {pair.Key}, but no block of that shape exists.");
            }

            if (pair.Value > 1)
            {
                warnings.Add(
                    $"{data.name}: {pair.Value} targets use ShapeType {pair.Key}. Multiple targets of the same shape are allowed.");
            }
        }

        return errors.Count == 0;
    }

    private void LogCollectedMessages(LevelData data)
    {
        string levelName = data != null ? data.name : "(null)";

        for (int i = 0; i < warnings.Count; i++)
        {
            Debug.LogWarning(warnings[i], this);
        }

        if (errors.Count == 0)
        {
            Debug.Log($"LEVEL VALID:\n{levelName}", this);
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"LEVEL INVALID: {levelName}");
        for (int i = 0; i < errors.Count; i++)
        {
            builder.AppendLine(errors[i]);
        }

        Debug.LogError(builder.ToString(), this);
    }
}
