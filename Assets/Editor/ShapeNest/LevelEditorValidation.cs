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
    public bool ShuttersValid;

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
        return Validate(levelName, blocks, targets, null, columns, rows);
    }

    public static LevelEditorValidationResult Validate(
        string levelName,
        IList<LevelBlockData> blocks,
        IList<LevelTargetData> targets,
        IList<LevelShutterData> shutters,
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

        result.CountsMatch = true;
        var blockLayers = new List<ShapeType>();
        var targetLayers = new List<ShapeType>();

        result.PositionsValid = true;
        result.ShapesMatch = true;

        var blockPositions = new Dictionary<Vector2Int, int>();
        var targetPositions = new Dictionary<Vector2Int, int>();
        var shutterPositions = new Dictionary<Vector2Int, int>();

        if (shutters != null)
        {
            result.ShuttersValid = true;
            for (int i = 0; i < shutters.Count; i++)
            {
                LevelShutterData shutter = shutters[i];
                if (shutter == null)
                {
                    result.ShuttersValid = false;
                    result.Errors.Add($"Shutter {i} is null/invalid.");
                    continue;
                }

                if (shutter.durability < 1)
                {
                    result.ShuttersValid = false;
                    result.Errors.Add($"Shutter {i} durability must be at least 1.");
                }

                if (shutter.cells == null || shutter.cells.Count == 0)
                {
                    result.ShuttersValid = false;
                    result.Errors.Add($"Shutter {i} must cover at least one cell.");
                    continue;
                }

                var local = new HashSet<Vector2Int>();
                for (int c = 0; c < shutter.cells.Count; c++)
                {
                    Vector2Int position = shutter.cells[c];
                    if (!IsInsideBoard(position, columns, rows))
                    {
                        result.ShuttersValid = false;
                        result.Errors.Add($"Shutter {i} is outside the {columns}x{rows} grid at ({position.x},{position.y}).");
                    }

                    if (!local.Add(position))
                    {
                        result.ShuttersValid = false;
                        result.Errors.Add($"Shutter {i} contains duplicate cell ({position.x},{position.y}).");
                    }

                    if (shutterPositions.TryGetValue(position, out int existingShutter))
                    {
                        result.ShuttersValid = false;
                        result.Errors.Add($"Duplicate shutter coverage at ({position.x},{position.y}): Shutter {existingShutter} and Shutter {i}.");
                    }
                    else
                    {
                        shutterPositions[position] = i;
                    }
                }
            }
        }
        else
        {
            result.ShuttersValid = true;
        }

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

                CollectLayoutErrors($"Block {i}", block.cells, result);
                if (!ShapeLayout.AreCellsFourConnected(block.cells))
                {
                    result.PositionsValid = false;
                    result.Errors.Add($"Block {i} cells are not 4-connected. Diagonal-only contact is not a chain.");
                }

                if (ShapeLayout.HasInvalidNestedLayer(block.cells, block.shapeType))
                {
                    result.Errors.Add($"Block {i} has an invalid nested innerShapes entry.");
                }

                ShapeLayout.CollectResolvableLayers(
                    block.cells,
                    block.shapeType,
                    block.composition,
                    block.outerShape,
                    blockLayers);
                CollectFootprint(
                    "Block",
                    i,
                    block.gridPosition,
                    block.cells,
                    block.shapeType,
                    columns,
                    rows,
                    blockPositions,
                    result);
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

                CollectLayoutErrors($"Target {i}", target.cells, result);
                if (!ShapeLayout.AreCellsFourConnected(target.cells))
                {
                    result.PositionsValid = false;
                    result.Errors.Add($"Target {i} cells are not 4-connected.");
                }

                if (ShapeLayout.HasInvalidNestedLayer(target.cells, target.shapeType))
                {
                    result.Errors.Add($"Target {i} has an invalid nested innerShapes entry.");
                }

                ShapeLayout.CollectResolvableLayers(
                    target.cells,
                    target.shapeType,
                    target.composition,
                    target.outerShape,
                    targetLayers);
                CollectFootprint(
                    "Target",
                    i,
                    target.gridPosition,
                    target.cells,
                    target.shapeType,
                    columns,
                    rows,
                    targetPositions,
                    result);
            }
        }

        if (blockLayers.Count != targetLayers.Count)
        {
            result.CountsMatch = false;
            result.Errors.Add(
                $"Matchable layer count from blocks ({blockLayers.Count}) does not match targets ({targetLayers.Count}).");
        }
        else if (!ShapeLayout.LayerSetsMatch(blockLayers, targetLayers))
        {
            result.ShapesMatch = false;
            result.Errors.Add("Block layer shapes do not match the available target layer shapes.");
        }

        return result;
    }

    private static void CollectLayoutErrors(string label, IReadOnlyList<ShapeCellData> cells, LevelEditorValidationResult result)
    {
        if (ShapeLayout.HasNullCell(cells))
        {
            result.PositionsValid = false;
            result.Errors.Add($"{label} has a null cell entry.");
        }

        if (ShapeLayout.HasDuplicateLocals(cells))
        {
            result.PositionsValid = false;
            result.Errors.Add($"{label} has duplicate local cell positions.");
        }

        if (!ShapeLayout.ContainsAnchorCell(cells))
        {
            result.PositionsValid = false;
            result.Errors.Add($"{label} is missing a (0,0) anchor cell.");
        }
    }

    private static void CollectFootprint(
        string kind,
        int index,
        Vector2Int anchor,
        IReadOnlyList<ShapeCellData> cells,
        ShapeType fallback,
        int columns,
        int rows,
        Dictionary<Vector2Int, int> occupied,
        LevelEditorValidationResult result)
    {
        int count = ShapeLayout.EffectiveCount(cells);
        for (int i = 0; i < count; i++)
        {
            Vector2Int position = anchor + ShapeLayout.EffectiveLocal(cells, i);
            if (!IsInsideBoard(position, columns, rows))
            {
                result.PositionsValid = false;
                result.Errors.Add(
                    $"{kind} {index} ({ShapeLayout.EffectiveShape(cells, i, fallback)}) is outside the {columns}x{rows} grid at ({position.x},{position.y}).");
            }

            if (occupied.TryGetValue(position, out int existingIndex))
            {
                result.PositionsValid = false;
                result.Errors.Add(
                    $"Duplicate {kind.ToLowerInvariant()} at ({position.x},{position.y}): {kind} {existingIndex} and {kind} {index}.");
            }
            else
            {
                occupied[position] = index;
            }
        }
    }

    private static bool IsInsideBoard(Vector2Int position, int columns, int rows)
    {
        return position.x >= 0 && position.x < columns && position.y >= 0 && position.y < rows;
    }
}
