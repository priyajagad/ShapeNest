using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class LevelEditorValidationResult
{
    public readonly List<string> Errors = new List<string>();
    public readonly List<string> Warnings = new List<string>();
    public bool NameValid;
    public bool BlockCountValid;
    public bool TargetCountValid;
    public bool CountsMatch;
    public bool PositionsValid;
    public bool ShapesMatch;

    public bool IsValid => Errors.Count == 0;
}

internal static class LevelEditorValidation
{
    public static LevelEditorValidationResult Validate(
        string levelName,
        IList<LevelBlockData> blocks,
        IList<LevelTargetData> targets,
        int columns,
        int rows)
    {
        var result = new LevelEditorValidationResult();
        int blockCount = blocks != null ? blocks.Count : 0;
        int targetCount = targets != null ? targets.Count : 0;

        result.NameValid = !string.IsNullOrWhiteSpace(levelName);
        if (!result.NameValid)
        {
            result.Errors.Add("Level name is empty.");
        }

        result.BlockCountValid = blocks != null && blockCount > 0;
        if (!result.BlockCountValid)
        {
            result.Errors.Add("At least one block is required.");
        }

        result.TargetCountValid = targets != null && targetCount > 0;
        if (!result.TargetCountValid)
        {
            result.Errors.Add("At least one target is required.");
        }

        result.CountsMatch = blockCount == targetCount;
        if (!result.CountsMatch)
        {
            result.Errors.Add(
                $"Block count ({blockCount}) does not match target count ({targetCount}).");
        }

        result.PositionsValid = true;
        result.ShapesMatch = true;

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
                    result.PositionsValid = false;
                    result.Errors.Add($"Block {i} is null/invalid.");
                    continue;
                }

                if (!Enum.IsDefined(typeof(MoveDirection), block.moveDirection))
                {
                    result.Errors.Add($"Block {i} has an invalid MoveDirection.");
                }

                Vector2Int position = block.gridPosition;
                if (!IsInsideBoard(position, columns, rows))
                {
                    result.PositionsValid = false;
                    result.Errors.Add(
                        $"Block {i} ({block.shapeType}) is outside the board at ({position.x},{position.y}).");
                }

                if (blockPositions.TryGetValue(position, out int existingIndex))
                {
                    result.PositionsValid = false;
                    result.Errors.Add(
                        $"Duplicate block at ({position.x},{position.y}): Block {existingIndex} and Block {i}.");
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
                    result.PositionsValid = false;
                    result.Errors.Add($"Target {i} is null/invalid.");
                    continue;
                }

                Vector2Int position = target.gridPosition;
                if (!IsInsideBoard(position, columns, rows))
                {
                    result.PositionsValid = false;
                    result.Errors.Add(
                        $"Target {i} ({target.shapeType}) is outside the board at ({position.x},{position.y}).");
                }

                if (targetPositions.TryGetValue(position, out int existingIndex))
                {
                    result.PositionsValid = false;
                    result.Errors.Add(
                        $"Duplicate target at ({position.x},{position.y}): Target {existingIndex} and Target {i}.");
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
                result.ShapesMatch = false;
                result.Errors.Add($"Blocks use {pair.Key}, but no target of that shape exists.");
            }

            if (pair.Value > 1)
            {
                result.Warnings.Add($"{pair.Value} blocks use {pair.Key}. Multiple blocks of the same shape are allowed.");
            }
        }

        foreach (KeyValuePair<ShapeType, int> pair in targetShapeCounts)
        {
            if (!blockShapeCounts.ContainsKey(pair.Key))
            {
                result.ShapesMatch = false;
                result.Errors.Add($"Targets use {pair.Key}, but no block of that shape exists.");
            }

            if (pair.Value > 1)
            {
                result.Warnings.Add($"{pair.Value} targets use {pair.Key}. Multiple targets of the same shape are allowed.");
            }
        }

        return result;
    }

    private static bool IsInsideBoard(Vector2Int position, int columns, int rows)
    {
        return position.x >= 0 && position.x < columns && position.y >= 0 && position.y < rows;
    }
}
