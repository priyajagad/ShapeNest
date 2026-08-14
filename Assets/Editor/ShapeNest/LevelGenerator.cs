using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

internal sealed class LevelGeneratorSettings
{
    public int Seed = 12345;
    public int LevelCount = 20;
    public int BoardWidth = 5;
    public int BoardHeight = 5;
    public int MinBlocks = 3;
    public int MaxBlocks = 5;
    public int StartingLevelNumber = 2;
    public int MaxAttemptsPerLevel = 1000;
    public int MinSolutionMoves = 1;
    public int MaxSolutionMoves = 100;
    public int MaxSolverDepth = 100;
    public int MaxSolverStates = 100000;
    public DifficultyTier Difficulty = DifficultyTier.Progressive;
    public MechanicMode FixedDirections = MechanicMode.Progressive;
    public MechanicMode Collisions = MechanicMode.Progressive;
    public MechanicMode TargetStopping = MechanicMode.Progressive;
    public ExistingAssetPolicy ExistingPolicy = ExistingAssetPolicy.Skip;
    public bool AllowImmediateSettlement;
}

internal static class LevelGenerator
{
    private static readonly ShapeType[] Shapes = (ShapeType[])Enum.GetValues(typeof(ShapeType));
    private static readonly MoveDirection[] FixedDirections =
    {
        MoveDirection.Up,
        MoveDirection.Down,
        MoveDirection.Left,
        MoveDirection.Right
    };

    public static GeneratedLevelResult TryCandidate(
        Random rng,
        LevelGeneratorSettings settings,
        DifficultyTier tier,
        float progress,
        string levelName)
    {
        return BuildCandidate(rng, settings, tier, progress, levelName);
    }

    public static GeneratedLevelResult TryGenerateOne(
        Random rng,
        LevelGeneratorSettings settings,
        DifficultyTier tier,
        float progress,
        string levelName)
    {
        int attempts = Mathf.Max(1, settings.MaxAttemptsPerLevel);
        GeneratedLevelResult lastReject = null;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            GeneratedLevelResult candidate = BuildCandidate(rng, settings, tier, progress, levelName);
            if (candidate.Outcome == GenerationOutcome.Accepted)
            {
                return candidate;
            }

            lastReject = candidate;
        }

        if (lastReject == null)
        {
            lastReject = new GeneratedLevelResult
            {
                LevelName = levelName,
                Outcome = GenerationOutcome.FailedAttempts,
                TargetTier = tier,
                Message = "No candidates were produced."
            };
        }
        else
        {
            lastReject.Outcome = GenerationOutcome.FailedAttempts;
            lastReject.Message = $"Could not find an accepted candidate in {attempts} attempts. Last: {lastReject.OutcomeLabel}";
        }

        return lastReject;
    }

    private static GeneratedLevelResult BuildCandidate(
        Random rng,
        LevelGeneratorSettings settings,
        DifficultyTier tier,
        float progress,
        string levelName)
    {
        var result = new GeneratedLevelResult
        {
            LevelName = levelName,
            TargetTier = tier
        };

        int width = Mathf.Max(1, settings.BoardWidth);
        int height = Mathf.Max(1, settings.BoardHeight);
        int blockCount = LevelDifficulty.ChooseBlockCount(rng, tier, settings.MinBlocks, settings.MaxBlocks);
        if (blockCount > width * height)
        {
            result.Outcome = GenerationOutcome.RejectedInvalid;
            result.Message = "Block count exceeds board size.";
            return result;
        }

        var usedBlockCells = new HashSet<Vector2Int>();
        var usedTargetCells = new HashSet<Vector2Int>();
        var blocks = new List<LevelBlockData>(blockCount);
        var targets = new List<LevelTargetData>(blockCount);

        float restrictChance = LevelDifficulty.RestrictedDirectionChance(tier, settings.FixedDirections, progress);
        bool preferSharedRow = ShouldUseCollisions(tier, settings.Collisions);
        int restricted = 0;

        Vector2Int[] blockCells = PickUniqueCells(rng, width, height, blockCount, usedBlockCells, preferSharedRow);
        ShapeType[] shapes = AssignShapes(rng, blockCount, tier);

        for (int i = 0; i < blockCount; i++)
        {
            MoveDirection direction = MoveDirection.Any;
            if (rng.NextDouble() < restrictChance)
            {
                direction = FixedDirections[rng.Next(FixedDirections.Length)];
                restricted++;
            }

            blocks.Add(new LevelBlockData
            {
                shapeType = shapes[i],
                moveDirection = direction,
                gridPosition = blockCells[i]
            });
        }

        for (int i = 0; i < blockCount; i++)
        {
            Vector2Int targetCell = PickTargetCell(
                rng,
                width,
                height,
                usedTargetCells,
                blockCells[i],
                settings.AllowImmediateSettlement);
            usedTargetCells.Add(targetCell);
            targets.Add(new LevelTargetData
            {
                shapeType = shapes[i],
                gridPosition = targetCell
            });
        }

        result.Blocks = blocks;
        result.Targets = targets;
        result.BlockCount = blockCount;

        LevelEditorValidationResult validation = LevelEditorValidation.Validate(levelName, blocks, targets, width, height);
        if (!validation.IsValid)
        {
            result.Outcome = GenerationOutcome.RejectedInvalid;
            result.Message = validation.Errors.Count > 0 ? validation.Errors[0] : "Structural validation failed.";
            return result;
        }

        if (!settings.AllowImmediateSettlement && CountImmediateSettlements(blocks, targets) > 0)
        {
            result.Outcome = GenerationOutcome.RejectedTrivial;
            result.Message = "A block starts on its matching target.";
            return result;
        }

        SolverLevel solverLevel = ShapeNestSolver.FromLists(width, height, blocks, targets);
        var limits = new SolverLimits
        {
            MaxDepth = settings.MaxSolverDepth,
            MaxStates = settings.MaxSolverStates
        };
        SolverResult solver = ShapeNestSolver.Solve(solverLevel, limits);
        result.MoveCount = solver.MoveCount;
        result.ExploredStates = solver.ExploredStates;
        result.Solution = solver.Solution;
        result.ReplayVerified = solver.ReplayVerified;
        result.EstimatedDifficulty = LevelDifficulty.Estimate(solver, blockCount, restricted);

        if (solver.Status == SolverStatus.LimitReached)
        {
            result.Outcome = GenerationOutcome.RejectedLimitReached;
            result.Message = "Solver limit reached.";
            return result;
        }

        if (solver.Status == SolverStatus.ReplayFailed || !solver.ReplayVerified)
        {
            result.Outcome = GenerationOutcome.RejectedReplayFailed;
            result.Message = solver.Error ?? "Solution replay failed.";
            return result;
        }

        if (solver.Status != SolverStatus.Solved)
        {
            result.Outcome = GenerationOutcome.RejectedUnsolvable;
            result.Message = "Unsolvable.";
            return result;
        }

        if (solver.MoveCount == 0)
        {
            result.Outcome = GenerationOutcome.RejectedTrivial;
            result.Message = "Already solved at start.";
            return result;
        }

        if (tier != DifficultyTier.Easy && solver.MoveCount <= 1)
        {
            result.Outcome = GenerationOutcome.RejectedTooEasy;
            result.Message = "Solution is only 1 move.";
            return result;
        }

        if (tier == DifficultyTier.Hard || tier == DifficultyTier.Expert)
        {
            if (solver.CollisionStops == 0 && solver.TargetStops <= 1 && restricted == 0)
            {
                result.Outcome = GenerationOutcome.RejectedTooEasy;
                result.Message = "Not enough interaction for this difficulty.";
                return result;
            }
        }

        if (!LevelDifficulty.MatchesTier(
                tier,
                result.EstimatedDifficulty,
                solver.MoveCount,
                blockCount,
                settings.MinSolutionMoves,
                settings.MaxSolutionMoves))
        {
            bool tooEasy = solver.MoveCount < 3 || result.EstimatedDifficulty < 20;
            result.Outcome = tooEasy ? GenerationOutcome.RejectedTooEasy : GenerationOutcome.RejectedTooHard;
            result.Message = tooEasy ? "Below requested difficulty." : "Above requested difficulty.";
            return result;
        }

        result.Outcome = GenerationOutcome.Accepted;
        result.Message = "Solvable and verified.";
        return result;
    }

    private static bool ShouldUseCollisions(DifficultyTier tier, MechanicMode mode)
    {
        if (mode == MechanicMode.Off)
        {
            return false;
        }

        if (mode == MechanicMode.On)
        {
            return true;
        }

        return tier != DifficultyTier.Easy;
    }

    private static ShapeType[] AssignShapes(Random rng, int count, DifficultyTier tier)
    {
        var shapes = new ShapeType[count];
        var bag = new List<ShapeType>(Shapes);
        Shuffle(rng, bag);
        for (int i = 0; i < count; i++)
        {
            if (i < bag.Count && (tier == DifficultyTier.Easy || i < 3))
            {
                shapes[i] = bag[i];
            }
            else
            {
                shapes[i] = Shapes[rng.Next(Shapes.Length)];
            }
        }

        return shapes;
    }

    private static Vector2Int[] PickUniqueCells(
        Random rng,
        int width,
        int height,
        int count,
        HashSet<Vector2Int> used,
        bool preferSharedRow)
    {
        var cells = new Vector2Int[count];
        int sharedAxis = rng.Next(width);
        bool shareRow = rng.Next(2) == 0;
        for (int i = 0; i < count; i++)
        {
            Vector2Int cell;
            int guard = 0;
            do
            {
                if (preferSharedRow && guard < 8)
                {
                    cell = shareRow
                        ? new Vector2Int(rng.Next(width), sharedAxis % height)
                        : new Vector2Int(sharedAxis % width, rng.Next(height));
                }
                else
                {
                    cell = new Vector2Int(rng.Next(width), rng.Next(height));
                }

                guard++;
            }
            while (used.Contains(cell) && guard < 200);

            if (used.Contains(cell))
            {
                cell = FirstFree(width, height, used);
            }

            used.Add(cell);
            cells[i] = cell;
        }

        return cells;
    }

    private static Vector2Int PickTargetCell(
        Random rng,
        int width,
        int height,
        HashSet<Vector2Int> usedTargets,
        Vector2Int blockCell,
        bool allowImmediate)
    {
        int guard = 0;
        Vector2Int cell;
        do
        {
            cell = new Vector2Int(rng.Next(width), rng.Next(height));
            guard++;
        }
        while ((usedTargets.Contains(cell) || (!allowImmediate && cell == blockCell)) && guard < 200);

        if (usedTargets.Contains(cell) || (!allowImmediate && cell == blockCell))
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (!usedTargets.Contains(candidate) && (allowImmediate || candidate != blockCell))
                    {
                        return candidate;
                    }
                }
            }

            return FirstFree(width, height, usedTargets);
        }

        return cell;
    }

    private static Vector2Int FirstFree(int width, int height, HashSet<Vector2Int> used)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!used.Contains(cell))
                {
                    return cell;
                }
            }
        }

        return Vector2Int.zero;
    }

    private static int CountImmediateSettlements(List<LevelBlockData> blocks, List<LevelTargetData> targets)
    {
        int count = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            LevelBlockData block = blocks[i];
            for (int t = 0; t < targets.Count; t++)
            {
                LevelTargetData target = targets[t];
                if (target.gridPosition == block.gridPosition && target.shapeType == block.shapeType)
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private static void Shuffle<T>(Random rng, IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
