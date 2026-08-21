using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Additive Magnet booster. Plans a full soft-obstacle route to a matching nest and
/// drives successive BlockMover segments until match. Other movable blocks are soft
/// obstacles (temporarily cleared from occupancy for hops only). Hard gates remain:
/// fixed direction, Ice, closed shutters, board bounds. One charge per successful match.
/// </summary>
public class MagnetBooster : MonoBehaviour
{
    public enum MagnetPhase
    {
        Idle,
        Selecting,
        Executing
    }

    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    [SerializeField]
    private LevelManager levelManager;

    [SerializeField]
    private BoardManager boardManager;

    [SerializeField]
    [Min(0)]
    [Tooltip("Test inventory. Consumed only after a successful Magnet match.")]
    private int magnetCharges = 3;

    [SerializeField]
    private bool enableKeyboardActivate = true;

    [SerializeField]
    private bool debugLog = true;

    private MagnetPhase phase = MagnetPhase.Idle;
    private Coroutine pullRoutine;
    private Block highlightedBlock;

    public MagnetPhase Phase => phase;
    public bool IsSelecting => phase == MagnetPhase.Selecting;
    public bool IsBusy => phase != MagnetPhase.Idle;
    public int MagnetCharges => magnetCharges;

    /// <summary>Fired when charge count changes (UI sync).</summary>
    public event Action<int> OnChargesChanged;

    /// <summary>Fired when Magnet phase changes (Idle/Selecting/Executing).</summary>
    public event Action<MagnetPhase> OnPhaseChanged;

    private void Awake()
    {
        if (levelManager == null)
        {
            levelManager = FindFirstObjectByType<LevelManager>();
        }

        if (boardManager == null)
        {
            boardManager = FindFirstObjectByType<BoardManager>();
        }
    }

    private void OnDisable()
    {
        ResetMagnetState("disabled");
    }

    /// <summary>
    /// Clears selection/execution after level load or restart.
    /// Does not change charge inventory.
    /// </summary>
    public void ResetMagnetState(string reason = null)
    {
        if (pullRoutine != null)
        {
            StopCoroutine(pullRoutine);
            pullRoutine = null;
        }

        ClearHighlight();
        if (phase != MagnetPhase.Idle)
        {
            SetPhase(MagnetPhase.Idle);
        }

        if (!string.IsNullOrEmpty(reason))
        {
            Log($"Magnet reset: {reason}");
        }
    }

    private void Update()
    {
        if (!enableKeyboardActivate)
        {
            return;
        }

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame)
        {
            ToggleMagnet();
        }
    }

    /// <summary>Test/UI entry point. Toggles selection mode when idle.</summary>
    [ContextMenu("Activate Magnet")]
    public void ActivateMagnet()
    {
        if (phase == MagnetPhase.Executing)
        {
            return;
        }

        if (phase == MagnetPhase.Selecting)
        {
            CancelMagnet("Cancelled");
            return;
        }

        if (levelManager != null && !levelManager.IsGameplayInputAllowed)
        {
            Log("Magnet ignored: gameplay input not allowed");
            return;
        }

        if (magnetCharges <= 0)
        {
            Log("Magnet ignored: no charges");
            return;
        }

        SetPhase(MagnetPhase.Selecting);
        Log($"Magnet selecting (charges={magnetCharges}). Tap a block.");
    }

    public void ToggleMagnet()
    {
        ActivateMagnet();
    }

    public void CancelMagnet(string reason = null)
    {
        if (phase == MagnetPhase.Executing)
        {
            return;
        }

        ClearHighlight();
        SetPhase(MagnetPhase.Idle);
        if (!string.IsNullOrEmpty(reason))
        {
            Log($"Magnet cancelled: {reason}");
        }
    }

    /// <summary>
    /// Called by InputManager while selecting. Returns true if the press was consumed.
    /// </summary>
    public bool TryHandleSelectionPress(Block block)
    {
        if (phase != MagnetPhase.Selecting)
        {
            return false;
        }

        if (block == null)
        {
            Log("Magnet: tap a block to pull");
            return true;
        }

        TryUseMagnetOnBlock(block);
        return true;
    }

    public bool TryUseMagnetOnBlock(Block block)
    {
        if (phase != MagnetPhase.Selecting || pullRoutine != null)
        {
            return false;
        }

        if (!TryBuildMagnetPlan(block, out _, out string failReason))
        {
            Log($"Magnet failed: {failReason}");
            // Stay in selecting mode so the player can try another block.
            return false;
        }

        ClearHighlight();
        highlightedBlock = block;
        block.ShowDragSelection();
        SetPhase(MagnetPhase.Executing);
        pullRoutine = StartCoroutine(ExecuteMagnetJourney(block));
        return true;
    }

    /// <summary>True if Magnet could legally pull this block right now.</summary>
    public bool CanMagnetPull(Block block)
    {
        return TryBuildMagnetPlan(block, out _, out _);
    }

    private bool TryBuildMagnetPlan(Block block, out MagnetPlan plan, out string failReason)
    {
        plan = default;
        failReason = null;

        if (block == null || !block.isActiveAndEnabled)
        {
            failReason = "invalid block";
            return false;
        }

        if (block.IsSettled)
        {
            failReason = "block settled";
            return false;
        }

        if (block.IsFrozen)
        {
            failReason = "block frozen by Ice";
            return false;
        }

        BlockMover mover = block.GetComponent<BlockMover>();
        if (mover == null || mover.IsMoving || mover.IsDragging)
        {
            failReason = "block busy";
            return false;
        }

        BoardManager board = boardManager != null ? boardManager : block.Board;
        if (board == null)
        {
            failReason = "no board";
            return false;
        }

        if (board.IsBlockUnderClosedShutter(block))
        {
            failReason = "block under closed shutter";
            return false;
        }

        if (levelManager != null && !levelManager.IsPieceInputAllowed)
        {
            failReason = "piece input not allowed";
            return false;
        }

        if (!TryFindPathTowardMatchingNest(board, block, mover, out List<Vector2Int> path, out _))
        {
            failReason = "no legal route toward a matching nest";
            return false;
        }

        if (!TryBuildPlanFromPath(board, block, mover, path, out plan))
        {
            failReason = "route found but no executable first move";
            return false;
        }

        return true;
    }

    /// <summary>
    /// BFS toward a matching nest. Other movable blocks are SOFT obstacles (ignored for
    /// routing). Hard gates: fixed direction, board bounds, closed shutters, non-matching nests.
    /// </summary>
    private static bool TryFindPathTowardMatchingNest(
        BoardManager board,
        Block block,
        BlockMover mover,
        out List<Vector2Int> path,
        out bool pathEndsInNestEntry)
    {
        path = null;
        pathEndsInNestEntry = false;

        Vector2Int origin = block.GridPosition;
        if (IsMagnetGoalCell(board, block, origin))
        {
            path = new List<Vector2Int> { origin };
            pathEndsInNestEntry = false;
            return true;
        }

        int capacity = Mathf.Max(16, board.Width * board.Height);
        var visited = new HashSet<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>(capacity);
        var queue = new Queue<Vector2Int>(capacity);
        visited.Add(origin);
        queue.Enqueue(origin);

        Vector2Int goal = origin;
        bool found = false;

        while (queue.Count > 0)
        {
            Vector2Int pos = queue.Dequeue();

            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int direction = CardinalDirections[i];
                if (!mover.IsDirectionAllowed(direction))
                {
                    continue;
                }

                Vector2Int next = pos + direction;

                // Matching nest under next anchor (soft: ignore other blocks on the nest).
                if (HasMagnetNestMatchSoft(board, block, next))
                {
                    cameFrom[next] = pos;
                    goal = next;
                    pathEndsInNestEntry = true;
                    found = true;
                    queue.Clear();
                    break;
                }

                if (!CanMagnetSoftHopInto(board, block, next) || visited.Contains(next))
                {
                    continue;
                }

                visited.Add(next);
                cameFrom[next] = pos;

                if (IsMagnetGoalCell(board, block, next))
                {
                    goal = next;
                    pathEndsInNestEntry = false;
                    found = true;
                    queue.Clear();
                    break;
                }

                queue.Enqueue(next);
            }
        }

        if (!found)
        {
            return false;
        }

        path = ReconstructPath(origin, goal, cameFrom);
        return path != null && path.Count > 0;
    }

    private static bool TryBuildPlanFromPath(
        BoardManager board,
        Block block,
        BlockMover mover,
        List<Vector2Int> path,
        out MagnetPlan plan)
    {
        plan = default;
        Vector2Int origin = block.GridPosition;
        if (path == null || path.Count == 0 || path[0] != origin)
        {
            return false;
        }

        // Already at an adjacent/occupying goal: nudge one allowed step so DragRoutine
        // sees remainingSteps > 0 and can trigger the existing adjacent-match path.
        if (path.Count == 1)
        {
            if (!TryFindMatchNudgeDirection(board, block, mover, origin, out Vector2Int nudge))
            {
                return false;
            }

            plan = new MagnetPlan
            {
                direction = nudge,
                requestCell = origin + nudge,
                hopsBeforeMatch = 0
            };
            return true;
        }

        Vector2Int firstDir = path[1] - path[0];
        if (firstDir == Vector2Int.zero || !mover.IsDirectionAllowed(firstDir))
        {
            return false;
        }

        // Collapse the first straight segment; BlockMover may multi-hop along it.
        int endIndex = 1;
        while (endIndex + 1 < path.Count && path[endIndex + 1] - path[endIndex] == firstDir)
        {
            endIndex++;
        }

        Vector2Int requestCell = path[endIndex];
        plan = new MagnetPlan
        {
            direction = firstDir,
            requestCell = requestCell,
            hopsBeforeMatch = endIndex
        };
        return true;
    }

    private static bool TryFindMatchNudgeDirection(
        BoardManager board,
        Block block,
        BlockMover mover,
        Vector2Int origin,
        out Vector2Int direction)
    {
        direction = Vector2Int.zero;

        // Prefer the cardinal that enters the matching nest cell when possible.
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector2Int dir = CardinalDirections[i];
            if (!mover.IsDirectionAllowed(dir))
            {
                continue;
            }

            if (board.HasNestMatch(block, origin + dir)
                || HasMagnetNestMatchSoft(board, block, origin + dir))
            {
                direction = dir;
                return true;
            }
        }

        // Otherwise any allowed direction works: DragRoutine checks adjacent match
        // when remainingSteps > 0 after BeginDrag.
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector2Int dir = CardinalDirections[i];
            if (!mover.IsDirectionAllowed(dir))
            {
                continue;
            }

            direction = dir;
            return true;
        }

        return false;
    }

    private static List<Vector2Int> ReconstructPath(
        Vector2Int origin,
        Vector2Int goal,
        Dictionary<Vector2Int, Vector2Int> cameFrom)
    {
        var path = new List<Vector2Int>();
        Vector2Int current = goal;
        path.Add(current);
        while (current != origin)
        {
            if (!cameFrom.TryGetValue(current, out Vector2Int parent))
            {
                return null;
            }

            current = parent;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static bool IsMagnetGoalCell(BoardManager board, Block block, Vector2Int anchor)
    {
        if (HasMagnetNestMatchSoft(board, block, anchor))
        {
            return true;
        }

        return HasAdjacentMatchingNestSoft(board, block, anchor);
    }

    /// <summary>
    /// Soft hop: board bounds + closed shutters are hard. Other blocks are ignored.
    /// Non-matching target cells remain blocked (matching nests are handled as entry edges).
    /// </summary>
    private static bool CanMagnetSoftHopInto(BoardManager board, Block block, Vector2Int nextAnchor)
    {
        if (!IsMagnetSoftFootprintValid(board, block, nextAnchor))
        {
            return false;
        }

        if (HasMagnetNestMatchSoft(board, block, nextAnchor))
        {
            return false;
        }

        return !board.FootprintTouchesTarget(block, nextAnchor);
    }

    private static bool IsMagnetSoftFootprintValid(BoardManager board, Block block, Vector2Int toAnchor)
    {
        if (block == null || board == null)
        {
            return false;
        }

        if (board.DoesFootprintTouchClosedShutter(block, toAnchor))
        {
            return false;
        }

        int count = Mathf.Max(1, block.CellCount);
        for (int i = 0; i < count; i++)
        {
            if (!board.IsInsideBoard(toAnchor + block.GetLocalCell(i)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Like BoardManager.HasNestMatch, but ignores other movable blocks (soft obstacles).
    /// </summary>
    private static bool HasMagnetNestMatchSoft(BoardManager board, Block block, Vector2Int proposedAnchor)
    {
        if (!IsMagnetSoftFootprintValid(board, block, proposedAnchor))
        {
            return false;
        }

        int count = Mathf.Max(1, block.CellCount);
        for (int i = 0; i < count; i++)
        {
            Target target = board.GetTargetAt(proposedAnchor + block.GetLocalCell(i));
            if (target == null)
            {
                continue;
            }

            if (target.RequiredShape == block.GetActiveShape(i))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAdjacentMatchingNestSoft(BoardManager board, Block block, Vector2Int anchor)
    {
        for (int i = 0; i < CardinalDirections.Length; i++)
        {
            Vector2Int candidate = anchor + CardinalDirections[i];
            if (HasMagnetNestMatchSoft(board, block, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static int MagnetAxisSteps(Vector2Int delta, Vector2Int direction)
    {
        if (direction.x != 0)
        {
            return direction.x > 0 ? delta.x : -delta.x;
        }

        if (direction.y != 0)
        {
            return direction.y > 0 ? delta.y : -delta.y;
        }

        return 0;
    }

    /// <summary>
    /// Temporarily unregisters other blocks along a Magnet segment so BlockMover can hop.
    /// Soft blocks are not moved; occupancy is restored after the segment.
    /// </summary>
    private static List<Block> SuspendSoftBlocksForSegment(
        BoardManager board,
        Block magnetBlock,
        Vector2Int origin,
        Vector2Int direction,
        Vector2Int requestCell)
    {
        var suspended = new List<Block>();
        if (board == null || magnetBlock == null || direction == Vector2Int.zero)
        {
            return suspended;
        }

        var seen = new HashSet<Block>();
        int targetSteps = Mathf.Max(1, MagnetAxisSteps(requestCell - origin, direction));
        Vector2Int cursor = origin;

        for (int step = 1; step <= targetSteps; step++)
        {
            cursor = origin + direction * step;
            CollectSoftOccupantsAtAnchor(board, magnetBlock, cursor, seen, suspended);
        }

        for (int i = 0; i < suspended.Count; i++)
        {
            board.UnregisterBlock(suspended[i]);
        }

        return suspended;
    }

    private static void CollectSoftOccupantsAtAnchor(
        BoardManager board,
        Block magnetBlock,
        Vector2Int anchor,
        HashSet<Block> seen,
        List<Block> destination)
    {
        int count = Mathf.Max(1, magnetBlock.CellCount);
        for (int i = 0; i < count; i++)
        {
            Block occupant = board.GetBlockAt(anchor + magnetBlock.GetLocalCell(i));
            if (occupant == null || occupant == magnetBlock || !seen.Add(occupant))
            {
                continue;
            }

            destination.Add(occupant);
        }
    }

    private static void RestoreSoftBlocks(BoardManager board, Block magnetBlock, List<Block> suspended)
    {
        if (board == null || suspended == null)
        {
            return;
        }

        for (int i = 0; i < suspended.Count; i++)
        {
            Block soft = suspended[i];
            if (soft == null || !soft || soft.IsSettled || !soft.isActiveAndEnabled)
            {
                continue;
            }

            // Skip if the magnet block still occupies any of those cells.
            if (magnetBlock != null && magnetBlock && FootprintsOverlap(magnetBlock, soft))
            {
                continue;
            }

            board.TryRegisterBlock(soft, soft.GridPosition);
        }
    }

    private static bool FootprintsOverlap(Block a, Block b)
    {
        int countA = Mathf.Max(1, a.CellCount);
        int countB = Mathf.Max(1, b.CellCount);
        for (int i = 0; i < countA; i++)
        {
            Vector2Int cellA = a.GridPosition + a.GetLocalCell(i);
            for (int j = 0; j < countB; j++)
            {
                if (cellA == b.GridPosition + b.GetLocalCell(j))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator ExecuteMagnetJourney(Block block)
    {
        BlockMover mover = block != null ? block.GetComponent<BlockMover>() : null;
        int matchesBefore = levelManager != null ? levelManager.SuccessfulMatchCount : 0;
        bool returnToSelecting = false;
        bool anySegmentMoved = false;

        try
        {
            if (block == null || mover == null)
            {
                returnToSelecting = true;
                yield break;
            }

            BoardManager board = boardManager != null ? boardManager : block.Board;
            int maxSegments = board != null
                ? Mathf.Max(8, board.Width * board.Height + 4)
                : 32;

            for (int segment = 0; segment < maxSegments; segment++)
            {
                if (block == null || !block || !block.isActiveAndEnabled || block.IsSettled)
                {
                    break;
                }

                if (HasMagnetSucceeded(block, matchesBefore))
                {
                    break;
                }

                // Re-plan from the live board after every segment (cascades/shutters/etc.).
                if (!TryBuildMagnetPlan(block, out MagnetPlan plan, out string failReason))
                {
                    Log(anySegmentMoved
                        ? $"Magnet stopped mid-journey: {failReason}"
                        : $"Magnet failed before first move: {failReason}");
                    returnToSelecting = magnetCharges > 0;
                    yield break;
                }

                Vector2Int posBefore = block.GridPosition;
                yield return ExecuteMagnetSegment(block, mover, plan);

                if (block == null || !block || !block.isActiveAndEnabled || block.IsSettled)
                {
                    break;
                }

                if (block.GridPosition != posBefore)
                {
                    anySegmentMoved = true;
                }
                else if (!HasMagnetSucceeded(block, matchesBefore))
                {
                    Log("Magnet stopped: segment produced no movement");
                    returnToSelecting = magnetCharges > 0;
                    yield break;
                }

                if (HasMagnetSucceeded(block, matchesBefore))
                {
                    break;
                }
            }

            if (HasMagnetSucceeded(block, matchesBefore))
            {
                SetCharges(magnetCharges - 1);
                Log($"Magnet journey complete (match). Charges left={magnetCharges}");
            }
            else
            {
                Log(anySegmentMoved
                    ? "Magnet journey ended without match (not consumed)"
                    : "Magnet journey failed (not consumed)");
                returnToSelecting = magnetCharges > 0;
            }
        }
        finally
        {
            BoardManager board = boardManager != null
                ? boardManager
                : (block != null ? block.Board : null);
            if (board != null)
            {
                board.RebindChildBlockOccupancy();
            }

            ClearHighlight();
            pullRoutine = null;
            SetPhase(returnToSelecting ? MagnetPhase.Selecting : MagnetPhase.Idle);
        }
    }

    private bool HasMagnetSucceeded(Block block, int matchesBefore)
    {
        if (levelManager != null && levelManager.SuccessfulMatchCount > matchesBefore)
        {
            return true;
        }

        if (block == null || !block || !block.isActiveAndEnabled)
        {
            return true;
        }

        return block.IsSettled;
    }

    private IEnumerator ExecuteMagnetSegment(Block block, BlockMover mover, MagnetPlan plan)
    {
        if (block == null || mover == null)
        {
            yield break;
        }

        if (!mover.IsDirectionAllowed(plan.direction))
        {
            Log("Magnet segment skipped: direction not allowed");
            yield break;
        }

        BoardManager board = boardManager != null ? boardManager : block.Board;
        List<Block> suspended = SuspendSoftBlocksForSegment(
            board,
            block,
            block.GridPosition,
            plan.direction,
            plan.requestCell);

        try
        {
            bool began = mover.TryBeginDrag(plan.direction);
            if (!began)
            {
                Log("Magnet segment skipped: TryBeginDrag rejected");
                yield break;
            }

            mover.SetDragRequest(plan.requestCell);
            yield return null;
            if (mover != null)
            {
                mover.EndDrag();
            }

            float timeout = 8f;
            float deadline = Time.realtimeSinceStartup + timeout;
            while (mover != null && (mover.IsMoving || mover.IsDragging) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (mover != null && (mover.IsMoving || mover.IsDragging))
            {
                Log("Magnet segment timeout: forcing EndDrag");
                mover.EndDrag();
                float releaseDeadline = Time.realtimeSinceStartup + 1.5f;
                while (mover != null && (mover.IsMoving || mover.IsDragging) && Time.realtimeSinceStartup < releaseDeadline)
                {
                    yield return null;
                }
            }

            deadline = Time.realtimeSinceStartup + timeout;
            while (levelManager != null && Time.realtimeSinceStartup < deadline)
            {
                bool alignedBusy = levelManager.IsAlignedMatchRunning;
                bool moverBusy = mover != null && (mover.IsMoving || mover.IsDragging);
                if (!alignedBusy && !moverBusy)
                {
                    break;
                }

                yield return null;
            }

            deadline = Time.realtimeSinceStartup + timeout;
            while (levelManager != null
                   && !levelManager.IsPieceInputAllowed
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
        finally
        {
            RestoreSoftBlocks(board, block, suspended);
        }
    }

    /// <summary>Test helper to grant charges without economy UI.</summary>
    public void SetMagnetCharges(int count)
    {
        SetCharges(count);
    }

    private void SetPhase(MagnetPhase next)
    {
        if (phase == next)
        {
            return;
        }

        phase = next;
        OnPhaseChanged?.Invoke(phase);
    }

    private void SetCharges(int count)
    {
        int clamped = Mathf.Max(0, count);
        if (magnetCharges == clamped)
        {
            return;
        }

        magnetCharges = clamped;
        OnChargesChanged?.Invoke(magnetCharges);
    }

    private void ClearHighlight()
    {
        if (highlightedBlock != null)
        {
            highlightedBlock.HideDragSelection();
            highlightedBlock = null;
        }
    }

    private void Log(string message)
    {
        if (debugLog)
        {
            Debug.Log($"[Magnet] {message}", this);
        }
    }

    private struct MagnetPlan
    {
        public Vector2Int direction;
        public Vector2Int requestCell;
        public int hopsBeforeMatch;
    }
}
