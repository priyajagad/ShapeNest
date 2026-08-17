using System;
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

        var blockLayers = new List<ShapeType>();
        var targetLayers = new List<ShapeType>();

        if (data.gridWidth < 1 || data.gridHeight < 1)
        {
            warnings.Add(
                $"{data.name}: Grid size is missing or invalid ({data.gridWidth}x{data.gridHeight}). Validation uses {data.ResolvedGridWidth}x{data.ResolvedGridHeight}.");
        }

        int gridWidth = data.ResolvedGridWidth;
        int gridHeight = data.ResolvedGridHeight;
        if (boardManager != null && (boardManager.Width != gridWidth || boardManager.Height != gridHeight))
        {
            warnings.Add(
                $"{data.name}: Scene BoardManager is {boardManager.Width}x{boardManager.Height}. LevelData grid is {gridWidth}x{gridHeight}. Validation uses LevelData.");
        }

        var blockPositions = new Dictionary<Vector2Int, int>();
        var targetPositions = new Dictionary<Vector2Int, int>();

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

                CollectPieceLayoutErrors($"{data.name}: Block {i}", block.cells, errors);
                if (!Enum.IsDefined(typeof(ShapeType), block.shapeType)
                    || !Enum.IsDefined(typeof(MoveDirection), block.moveDirection))
                {
                    errors.Add($"{data.name}: Block {i} has an invalid ShapeType or MoveDirection.");
                }

                if (!ShapeLayout.AreCellsFourConnected(block.cells))
                {
                    errors.Add($"{data.name}: Block {i} cells are not 4-connected. Diagonal-only contact is not a chain.");
                }

                if (ShapeLayout.HasInvalidNestedLayer(block.cells, block.shapeType))
                {
                    errors.Add($"{data.name}: Block {i} has an invalid nested innerShapes entry.");
                }

                ShapeLayout.CollectResolvableLayers(
                    block.cells,
                    block.shapeType,
                    block.composition,
                    block.outerShape,
                    blockLayers);
                CollectFootprintCells(
                    data.name,
                    "Block",
                    i,
                    block.gridPosition,
                    block.cells,
                    block.shapeType,
                    gridWidth,
                    gridHeight,
                    blockPositions,
                    errors);
                if (block.composition == PieceComposition.ShapeInShape
                    && block.outerShape == ShapeLayout.AnchorShape(block.cells, block.shapeType)
                    && ShapeLayout.EffectiveCount(block.cells) == 1)
                {
                    warnings.Add(
                        $"{data.name}: Block {i} is ShapeInShape but outer and inner shapes are the same 1x1. Confirm this is intended.");
                }
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

                CollectPieceLayoutErrors($"{data.name}: Target {i}", target.cells, errors);
                if (!Enum.IsDefined(typeof(ShapeType), target.shapeType))
                {
                    errors.Add($"{data.name}: Target {i} has an invalid ShapeType.");
                }

                if (!ShapeLayout.AreCellsFourConnected(target.cells))
                {
                    errors.Add($"{data.name}: Target {i} cells are not 4-connected.");
                }

                if (ShapeLayout.HasInvalidNestedLayer(target.cells, target.shapeType))
                {
                    errors.Add($"{data.name}: Target {i} has an invalid nested innerShapes entry.");
                }

                ShapeLayout.CollectResolvableLayers(
                    target.cells,
                    target.shapeType,
                    target.composition,
                    target.outerShape,
                    targetLayers);
                CollectFootprintCells(
                    data.name,
                    "Target",
                    i,
                    target.gridPosition,
                    target.cells,
                    target.shapeType,
                    gridWidth,
                    gridHeight,
                    targetPositions,
                    errors);
            }
        }

        if (blockLayers.Count != targetLayers.Count)
        {
            errors.Add(
                $"{data.name}: Matchable layer count from blocks ({blockLayers.Count}) does not match targets ({targetLayers.Count}).");
        }
        else if (!ShapeLayout.LayerSetsMatch(blockLayers, targetLayers))
        {
            errors.Add(
                $"{data.name}: Block layer shapes do not match the available target layer shapes (any-shape matching requires the same multiset of ShapeTypes).");
        }

        return errors.Count == 0;
    }

    private static void CollectPieceLayoutErrors(string label, IReadOnlyList<ShapeCellData> cells, List<string> destination)
    {
        if (ShapeLayout.HasNullCell(cells))
        {
            destination.Add($"{label} has a null cell entry.");
        }

        if (ShapeLayout.HasDuplicateLocals(cells))
        {
            destination.Add($"{label} has duplicate local cell positions.");
        }

        if (!ShapeLayout.ContainsAnchorCell(cells))
        {
            destination.Add($"{label} is missing a (0,0) anchor cell. Empty Cells is allowed (implicit 1x1); a non-empty list must include (0,0).");
        }
    }

    private static void CollectFootprintCells(
        string levelName,
        string kind,
        int index,
        Vector2Int anchor,
        IReadOnlyList<ShapeCellData> cells,
        ShapeType fallback,
        int gridWidth,
        int gridHeight,
        Dictionary<Vector2Int, int> occupied,
        List<string> destination)
    {
        int count = ShapeLayout.EffectiveCount(cells);
        for (int i = 0; i < count; i++)
        {
            Vector2Int position = anchor + ShapeLayout.EffectiveLocal(cells, i);
            if (position.x < 0 || position.y < 0 || position.x >= gridWidth || position.y >= gridHeight)
            {
                destination.Add(
                    $"{levelName}: {kind} {index} ({ShapeLayout.EffectiveShape(cells, i, fallback)}) is outside the {gridWidth}x{gridHeight} grid at ({position.x},{position.y}).");
            }

            if (occupied.TryGetValue(position, out int existingIndex))
            {
                destination.Add(
                    $"{levelName}: {kind} {existingIndex} and {kind} {index} use the same cell ({position.x},{position.y}).");
            }
            else
            {
                occupied[position] = index;
            }
        }
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
