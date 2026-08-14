using System.Text;
using UnityEditor;
using UnityEngine;

internal static class ShapeNestSolverTests
{
    [MenuItem("Tools/Shape Nest/Run Solver Tests")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        if (report.Contains("FAIL"))
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }
    }

    public static string RunAll()
    {
        var builder = new StringBuilder();
        int passed = 0;
        int failed = 0;

        Check(builder, ref passed, ref failed, "TestLevel solvable", TestLevelSolvable());
        Check(builder, ref passed, ref failed, "Replay matches BFS", TestLevelReplay());
        Check(builder, ref passed, ref failed, "Fixed direction respected", FixedDirectionRespected());
        Check(builder, ref passed, ref failed, "Collision stops before other block", CollisionStopsBeforeBlock());
        Check(builder, ref passed, ref failed, "Matching target enter and stop", MatchingTargetStopsOnCell());
        Check(builder, ref passed, ref failed, "Wrong-shape target stops before", WrongShapeTargetStopsBefore());
        Check(builder, ref passed, ref failed, "Settled blocks cannot move", SettledCannotMove());
        Check(builder, ref passed, ref failed, "All-settled recognized", AllSettledRecognized());
        Check(builder, ref passed, ref failed, "Illegal zero-length move rejected", ZeroLengthMoveRejected());

        builder.Insert(0, $"Solver tests: {passed} passed, {failed} failed\n");
        return builder.ToString();
    }

    private static void Check(StringBuilder builder, ref int passed, ref int failed, string name, bool ok)
    {
        if (ok)
        {
            passed++;
            builder.AppendLine("PASS  " + name);
        }
        else
        {
            failed++;
            builder.AppendLine("FAIL  " + name);
        }
    }

    private static SolverLevel TestLevel()
    {
        return new SolverLevel
        {
            Width = 5,
            Height = 5,
            InitialBlocks = new[]
            {
                new SolverBlock { X = 1, Y = 2, Shape = ShapeType.Square, MoveDirection = MoveDirection.Any },
                new SolverBlock { X = 3, Y = 2, Shape = ShapeType.Circle, MoveDirection = MoveDirection.Left },
                new SolverBlock { X = 2, Y = 4, Shape = ShapeType.Triangle, MoveDirection = MoveDirection.Down }
            },
            Targets = new[]
            {
                new SolverTarget { X = 4, Y = 2, Shape = ShapeType.Square },
                new SolverTarget { X = 0, Y = 2, Shape = ShapeType.Circle },
                new SolverTarget { X = 2, Y = 0, Shape = ShapeType.Triangle }
            }
        };
    }

    private static bool TestLevelSolvable()
    {
        SolverResult result = ShapeNestSolver.Solve(TestLevel(), SolverLimits.Default);
        return result.IsSolved && result.MoveCount > 0;
    }

    private static bool TestLevelReplay()
    {
        SolverLevel level = TestLevel();
        SolverResult result = ShapeNestSolver.Solve(level, SolverLimits.Default);
        if (!result.IsSolved)
        {
            return false;
        }

        return ShapeNestSolver.Replay(level, ShapeNestSolver.CreateInitialState(level), result.Solution);
    }

    private static bool FixedDirectionRespected()
    {
        var level = new SolverLevel
        {
            Width = 5,
            Height = 5,
            InitialBlocks = new[]
            {
                new SolverBlock { X = 2, Y = 2, Shape = ShapeType.Circle, MoveDirection = MoveDirection.Left }
            },
            Targets = new[]
            {
                new SolverTarget { X = 4, Y = 2, Shape = ShapeType.Circle }
            }
        };

        SolverState start = ShapeNestSolver.CreateInitialState(level);
        SolverState right = ShapeNestSolver.TryMove(level, start, 0, Vector2Int.right, out _, out _);
        SolverState left = ShapeNestSolver.TryMove(level, start, 0, Vector2Int.left, out _, out _);
        return right == null && left != null && left.Blocks[0].X == 0;
    }

    private static bool CollisionStopsBeforeBlock()
    {
        var level = new SolverLevel
        {
            Width = 5,
            Height = 5,
            InitialBlocks = new[]
            {
                new SolverBlock { X = 0, Y = 2, Shape = ShapeType.Square, MoveDirection = MoveDirection.Any },
                new SolverBlock { X = 3, Y = 2, Shape = ShapeType.Circle, MoveDirection = MoveDirection.Any }
            },
            Targets = new SolverTarget[0]
        };

        SolverState start = ShapeNestSolver.CreateInitialState(level);
        SolverState moved = ShapeNestSolver.TryMove(level, start, 0, Vector2Int.right, out bool collision, out _);
        return moved != null && moved.Blocks[0].X == 2 && collision;
    }

    private static bool MatchingTargetStopsOnCell()
    {
        var level = new SolverLevel
        {
            Width = 5,
            Height = 5,
            InitialBlocks = new[]
            {
                new SolverBlock { X = 0, Y = 2, Shape = ShapeType.Square, MoveDirection = MoveDirection.Any }
            },
            Targets = new[]
            {
                new SolverTarget { X = 3, Y = 2, Shape = ShapeType.Square }
            }
        };

        SolverState start = ShapeNestSolver.CreateInitialState(level);
        SolverState moved = ShapeNestSolver.TryMove(level, start, 0, Vector2Int.right, out _, out bool targetStop);
        return moved != null && moved.Blocks[0].X == 3 && moved.Blocks[0].Settled && targetStop;
    }

    private static bool WrongShapeTargetStopsBefore()
    {
        var level = new SolverLevel
        {
            Width = 5,
            Height = 5,
            InitialBlocks = new[]
            {
                new SolverBlock { X = 0, Y = 2, Shape = ShapeType.Square, MoveDirection = MoveDirection.Any }
            },
            Targets = new[]
            {
                new SolverTarget { X = 3, Y = 2, Shape = ShapeType.Circle }
            }
        };

        SolverState start = ShapeNestSolver.CreateInitialState(level);
        SolverState moved = ShapeNestSolver.TryMove(level, start, 0, Vector2Int.right, out _, out bool targetStop);
        return moved != null && moved.Blocks[0].X == 2 && !moved.Blocks[0].Settled && targetStop;
    }

    private static bool SettledCannotMove()
    {
        var level = new SolverLevel
        {
            Width = 5,
            Height = 5,
            InitialBlocks = new[]
            {
                new SolverBlock { X = 2, Y = 2, Shape = ShapeType.Square, MoveDirection = MoveDirection.Any }
            },
            Targets = new[]
            {
                new SolverTarget { X = 2, Y = 2, Shape = ShapeType.Square }
            }
        };

        SolverState start = ShapeNestSolver.CreateInitialState(level);
        if (!start.Blocks[0].Settled)
        {
            return false;
        }

        return ShapeNestSolver.TryMove(level, start, 0, Vector2Int.right, out _, out _) == null;
    }

    private static bool AllSettledRecognized()
    {
        var level = new SolverLevel
        {
            Width = 5,
            Height = 5,
            InitialBlocks = new[]
            {
                new SolverBlock { X = 1, Y = 1, Shape = ShapeType.Square, MoveDirection = MoveDirection.Any },
                new SolverBlock { X = 3, Y = 3, Shape = ShapeType.Circle, MoveDirection = MoveDirection.Any }
            },
            Targets = new[]
            {
                new SolverTarget { X = 1, Y = 1, Shape = ShapeType.Square },
                new SolverTarget { X = 3, Y = 3, Shape = ShapeType.Circle }
            }
        };

        SolverResult result = ShapeNestSolver.Solve(level, SolverLimits.Default);
        return result.IsSolved && result.MoveCount == 0 && ShapeNestSolver.CreateInitialState(level).AllSettled;
    }

    private static bool ZeroLengthMoveRejected()
    {
        var level = new SolverLevel
        {
            Width = 5,
            Height = 5,
            InitialBlocks = new[]
            {
                new SolverBlock { X = 0, Y = 0, Shape = ShapeType.Square, MoveDirection = MoveDirection.Any }
            },
            Targets = new[]
            {
                new SolverTarget { X = 4, Y = 4, Shape = ShapeType.Square }
            }
        };

        SolverState start = ShapeNestSolver.CreateInitialState(level);
        return ShapeNestSolver.TryMove(level, start, 0, Vector2Int.left, out _, out _) == null;
    }
}
