using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Moves a block cell-by-cell. One drag coroutine; the latest requested
/// direction and destination are applied after each hop. Matching-target
/// nest entry is visual and occupancy-safe.
/// </summary>
[RequireComponent(typeof(Block))]
public class BlockMover : MonoBehaviour
{
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Duration of each normal cell-to-cell hop.")]
    private float secondsPerCell = 0.14f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Tiny wind-up on the first hop of a drag. Later hops skip this.")]
    private float normalHopAnticipateDuration = 0.04f;

    [SerializeField]
    [Range(0f, 0.08f)]
    [Tooltip("First-hop wind-up distance as a fraction of one cell, opposite the move.")]
    private float normalHopAnticipatePercent = 0.04f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Brief pause after arriving on a non-matching final cell.")]
    private float finalSettleDelay = 0.03f;

    [SerializeField]
    [Range(0.96f, 1f)]
    [Tooltip("Subtle squash during a hop, relative to the current visual scale. 1 means none.")]
    private float hopTravelScale = 0.985f;

    [SerializeField]
    [Range(0f, 0.1f)]
    [Tooltip("Visual hop arc height as a fraction of one board cell. Does not change hop duration or occupancy.")]
    private float hopLiftPercent = 0.045f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Hold on the pre-target cell so the stop is readable before nest-entry.")]
    private float matchingTargetPause = 0.22f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Anticipation lift duration after the pause, before the nest arc.")]
    private float matchingTargetAnticipateDuration = 0.08f;

    [SerializeField]
    [Range(0.02f, 0.12f)]
    [Tooltip("Anticipation lift as a fraction of one board cell.")]
    private float matchingTargetAnticipateLiftPercent = 0.06f;

    [SerializeField]
    [Range(1f, 1.15f)]
    [Tooltip("Anticipation scale. 1 means no scale change.")]
    private float matchingTargetAnticipateScale = 1.06f;

    [SerializeField]
    [Range(0.05f, 0.25f)]
    [Tooltip("Arc peak height as a fraction of one board cell.")]
    private float matchingTargetLiftPercent = 0.12f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Duration of the curved hop into the matching nest.")]
    private float matchingTargetArcDuration = 0.14f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Tiny sit into the nest after the arc, before Settle.")]
    private float matchingTargetSitDuration = 0.05f;

    [SerializeField]
    [Range(0.9f, 1f)]
    [Tooltip("Subtle scale during the hop. 1 means no scale change.")]
    private float matchingTargetHopScale = 0.97f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Duration of the nest scale pulse after the block lands.")]
    private float matchingTargetPulseDuration = 0.12f;

    [SerializeField]
    [Range(1f, 1.2f)]
    [Tooltip("Peak scale of the nest pulse. 1 means no pulse.")]
    private float matchingTargetPulseScale = 1.08f;

    [SerializeField]
    private bool debugDrag;

    [SerializeField]
    [Tooltip("Presentation-only match/merge effect. Does not affect occupancy or completion.")]
    private MatchEffect matchEffectPrefab;

    private Block block;
    private BoardManager cachedBoard;
    private bool isMoving;
    private LevelManager levelManager;
    private AudioFeedback audioFeedback;
    private HapticFeedback hapticFeedback;

    private bool dragActive;
    private bool dragReleased;
    private Vector2Int dragOrigin;
    private Vector2Int dragDirection;
    private Vector2Int desiredCell;
    private Vector2Int logicalCell;
    private bool hopAnticipatePending;
    private bool dragWantsForward;
    private Coroutine dragRoutine;
    private MatchEffect activeMatchEffect;
    private readonly List<int> nestCellIndices = new List<int>();
    private readonly List<Target> nestTargets = new List<Target>();
    private readonly List<Vector2Int> splitWorlds = new List<Vector2Int>();
    private readonly List<ShapeCellData> splitCells = new List<ShapeCellData>();
    private readonly List<Vector2Int> splitAnchors = new List<Vector2Int>();
    private readonly List<List<ShapeCellData>> splitComponents = new List<List<ShapeCellData>>();
    private bool resolvingAligned;
    private bool hasLastMatch;
    private Vector2Int lastMatchOrigin;
    private Vector2Int lastMatchTargetCell;

    public static bool LastConsumeSucceeded { get; set; }

    /// <summary>TEMP: auto-match visual sequence counter for MATCH SEQUENCE logs.</summary>
    private static int matchSequenceIndex;

    private RectTransform pendingCellTraveler;

    public static void ResetMatchSequenceIndex()
    {
        matchSequenceIndex = 0;
    }

    /// <summary>Existing nest pause used between sequential auto-matches.</summary>
    public float MatchingTargetPause => matchingTargetPause;

    /// <summary>Existing nest pause — used between sequential auto-matches after VFX cleanup.</summary>
    public IEnumerator WaitNaturalMatchGap()
    {
        yield return Pause(matchingTargetPause);
    }

    public bool IsMoving => isMoving;
    public bool IsDragging => dragActive;
    public Vector2Int LogicalCell => logicalCell;

    public void SetLevelManager(LevelManager manager)
    {
        levelManager = manager;
    }

    public void SetAudioFeedback(AudioFeedback feedback)
    {
        audioFeedback = feedback;
    }

    public void SetHapticFeedback(HapticFeedback feedback)
    {
        hapticFeedback = feedback;
    }

    private void Awake()
    {
        block = GetComponent<Block>();
    }

    private void OnDisable()
    {
        dragActive = false;
        dragReleased = false;
        isMoving = false;
        dragRoutine = null;
        resolvingAligned = false;
        hasLastMatch = false;
        if (activeMatchEffect != null)
        {
            Destroy(activeMatchEffect.gameObject);
            activeMatchEffect = null;
        }
    }

    private void OnDestroy()
    {
        if (activeMatchEffect != null)
        {
            Destroy(activeMatchEffect.gameObject);
            activeMatchEffect = null;
        }
    }

    public bool IsDirectionAllowed(Vector2Int direction)
    {
        if (block == null)
        {
            block = GetComponent<Block>();
        }

        if (block == null)
        {
            return false;
        }

        switch (block.MoveDirection)
        {
            case MoveDirection.Any:
                return direction == Vector2Int.up
                    || direction == Vector2Int.down
                    || direction == Vector2Int.left
                    || direction == Vector2Int.right;
            case MoveDirection.Up:
                return direction == Vector2Int.up;
            case MoveDirection.Down:
                return direction == Vector2Int.down;
            case MoveDirection.Left:
                return direction == Vector2Int.left;
            case MoveDirection.Right:
                return direction == Vector2Int.right;
            default:
                return false;
        }
    }

    public bool TryBeginDrag(Vector2Int direction)
    {
        if (block == null)
        {
            block = GetComponent<Block>();
        }

        if (block == null || block.IsSettled || isMoving || dragActive || !IsDirectionAllowed(direction))
        {
            LogDrag($"BeginDrag rejected: settled={block != null && block.IsSettled} moving={isMoving} dragging={dragActive} dir={direction}");
            return false;
        }

        if (levelManager != null && !levelManager.IsPieceInputAllowed)
        {
            return false;
        }

        BoardManager board = GetBoard();
        if (board == null)
        {
            return false;
        }

        cachedBoard = board;
        dragActive = true;
        dragReleased = false;
        dragOrigin = block.GridPosition;
        logicalCell = dragOrigin;
        dragDirection = direction;
        desiredCell = dragOrigin;
        hopAnticipatePending = true;
        dragWantsForward = false;
        isMoving = true;
        dragRoutine = StartCoroutine(DragRoutine(board));
        LogDrag($"BeginDrag {block.name} origin={dragOrigin} dir={direction}");
        PlayDragStartSound();
        return true;
    }

    public void SetDragDirection(Vector2Int direction)
    {
        if (!dragActive || dragReleased || block == null || !IsDirectionAllowed(direction))
        {
            return;
        }

        if (direction == dragDirection)
        {
            return;
        }

        dragDirection = direction;
        dragOrigin = logicalCell;
        desiredCell = logicalCell;
        dragWantsForward = false;
        LogDrag($"Steer {direction} origin={dragOrigin}");
    }

    public void SetDragRequest(Vector2Int requestedCell)
    {
        if (!dragActive || dragReleased || block == null)
        {
            return;
        }

        BoardManager board = cachedBoard != null ? cachedBoard : GetBoard();
        if (board == null)
        {
            return;
        }

        Vector2Int clamped = ClampDragDestination(board, dragOrigin, dragDirection, requestedCell);
        int rawSteps = AxisSteps(requestedCell - dragOrigin, dragDirection);
        if (rawSteps > 0)
        {
            dragWantsForward = true;
        }

        desiredCell = clamped;
        if (debugDrag)
        {
            LogDrag($"Request {requestedCell} -> clamped {clamped} dir={dragDirection}");
        }
    }

    public void EndDrag()
    {
        if (!dragActive)
        {
            return;
        }

        dragReleased = true;
        if (debugDrag)
        {
            LogDrag($"EndDrag desired={desiredCell} grid={block.GridPosition}");
        }
    }

    private IEnumerator DragRoutine(BoardManager board)
    {
        try
        {
            RectTransform rect = block.RectTransform;
            float duration = Mathf.Max(0.01f, secondsPerCell);

            while (true)
            {
                Vector2Int committed = logicalCell;
                int remainingSteps = AxisSteps(desiredCell - committed, dragDirection);
                if (board.HasNestMatch(block, committed) && (remainingSteps > 0 || dragReleased))
                {
                    Vector2Int focus = dragDirection != Vector2Int.zero
                        ? committed + dragDirection
                        : committed;
                    yield return EnterMatchingTarget(board, rect, committed, focus);
                    break;
                }

                if (remainingSteps <= 0)
                {
                    if (dragReleased)
                    {
                        if (finalSettleDelay > 0f)
                        {
                            yield return Pause(finalSettleDelay);
                        }

                        break;
                    }

                    yield return null;
                    continue;
                }

                if (TryGetAdjacentMatchingTarget(board, committed, out Vector2Int startNestCell))
                {
                    yield return EnterMatchingTarget(board, rect, committed, startNestCell);
                    break;
                }

                Vector2Int next = committed + dragDirection;
                if (IsMatchingTargetCell(board, next))
                {
                    yield return EnterMatchingTarget(board, rect, committed, next);
                    break;
                }

                if (!CanHopInto(board, next))
                {
                    if (dragReleased)
                    {
                        break;
                    }

                    yield return null;
                    continue;
                }

                if (!board.TryMoveBlock(block, committed, next))
                {
                    if (dragReleased)
                    {
                        break;
                    }

                    yield return null;
                    continue;
                }

                logicalCell = next;
                PlayHopSound();
                bool anticipate = hopAnticipatePending;
                hopAnticipatePending = false;
                yield return AnimateHop(board, rect, committed, next, duration, anticipate);
                block.SetGridPosition(next);

                if (TryGetAdjacentMatchingTarget(board, next, out Vector2Int nestCell))
                {
                    yield return EnterMatchingTarget(board, rect, next, nestCell);
                    break;
                }
            }
        }
        finally
        {
            dragActive = false;
            dragReleased = false;
            isMoving = false;
            dragRoutine = null;
            cachedBoard = null;
        }
    }

    private void PlayDragStartSound()
    {
        if (audioFeedback != null)
        {
            audioFeedback.PlayDragStart();
        }

        if (hapticFeedback != null)
        {
            hapticFeedback.PlayGrab();
        }
    }

    private void PlayHopSound()
    {
        if (audioFeedback != null)
        {
            audioFeedback.PlayHop();
        }

        if (hapticFeedback != null)
        {
            hapticFeedback.PlayHop();
        }
    }

    private void PlayNestEntrySound()
    {
        if (audioFeedback != null)
        {
            audioFeedback.PlayNestEntry();
        }

        if (hapticFeedback != null)
        {
            hapticFeedback.PlayNestEntry();
        }
    }

    private void PlayMatchSound()
    {
        if (audioFeedback != null)
        {
            audioFeedback.PlayMatch();
        }

        if (hapticFeedback != null)
        {
            hapticFeedback.PlayMatch();
        }
    }

    private bool CanHopInto(BoardManager board, Vector2Int next)
    {
        if (!board.CanTranslateBlock(block, next))
        {
            return false;
        }

        return !board.FootprintTouchesTarget(block, next);
    }

    private static readonly Vector2Int[] AdjacentCheckOrder =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    private bool TryGetAdjacentMatchingTarget(
        BoardManager board,
        Vector2Int blockPosition,
        out Vector2Int nestCell)
    {
        for (int i = 0; i < AdjacentCheckOrder.Length; i++)
        {
            Vector2Int candidate = blockPosition + AdjacentCheckOrder[i];
            if (!IsMatchingTargetCell(board, candidate))
            {
                continue;
            }

            nestCell = candidate;
            return true;
        }

        nestCell = blockPosition;
        return false;
    }

    private IEnumerator EnterMatchingTarget(
        BoardManager board,
        RectTransform rect,
        Vector2Int from,
        Vector2Int to)
    {
        if (levelManager != null)
        {
            levelManager.BeginPieceMatchSequence();
        }

        try
        {
            yield return EnterMatchingTargetBody(board, rect, from, to);
        }
        finally
        {
            if (levelManager != null)
            {
                levelManager.EndPieceMatchSequence();
            }
        }
    }

    private IEnumerator EnterMatchingTargetBody(
        BoardManager board,
        RectTransform rect,
        Vector2Int from,
        Vector2Int to)
    {
        if (block != null && block.CellCount == 1 && block.HasActiveInnerLayer())
        {
            yield return EnterNestedInnerThenOuter(board, rect, from, to);
            yield break;
        }

        if (block != null && block.CellCount > 1)
        {
            yield return EnterChainPartialMatch(board, block, from, to);
            yield break;
        }

        Vector2Int occupancyTo = board.HasNestMatch(block, from) ? from : to;
        dragReleased = true;
        LogDrag($"Matching magnet {from} -> {occupancyTo}");

        Target nestTarget = null;
        board.CollectNestMatches(block, occupancyTo, nestCellIndices, nestTargets);
        if (nestTargets.Count > 0)
        {
            nestTarget = nestTargets[0];
            nestTarget.ShowReadyFeedback();
        }

        if (!board.TryMoveBlock(block, from, occupancyTo))
        {
            if (nestTarget != null)
            {
                nestTarget.HideReadyFeedback();
            }

            yield break;
        }

        logicalCell = occupancyTo;

        block.CancelDragSelectionImmediate();
        PlayNestEntrySound();

        Vector2 restPosition = board.GridToLocal(from);
        Vector3 restScale = block.RestScale;
        rect.anchoredPosition = restPosition;
        block.transform.localScale = restScale;

        yield return Pause(matchingTargetPause);
        if (nestTarget != null)
        {
            nestTarget.HideReadyFeedback();
        }

        yield return AnimateAnticipation(board, rect, restPosition, restScale);
        yield return AnimateNestEntry(board, rect, from, occupancyTo, restScale);

        block.SetGridPosition(occupancyTo);

        board.CollectNestMatches(block, occupancyTo, nestCellIndices, nestTargets);
        if (nestCellIndices.Count == 0)
        {
            yield break;
        }

        yield return ResolveCellMatches(board, from, occupancyTo, nestTarget);
    }

    private IEnumerator EnterChainPartialMatch(
        BoardManager board,
        Block subject,
        Vector2Int from,
        Vector2Int focus)
    {
        dragReleased = true;
        LogDrag($"Chain partial match {from} focus={focus}");
        yield return MatchFocusedChainCell(board, subject, from, focus, false);
        EnsureSubjectOccupancy(board, subject);
        yield return ResolveAlreadyAlignedMatches(board);
    }

    private IEnumerator MatchFocusedChainCell(
        BoardManager board,
        Block subject,
        Vector2Int from,
        Vector2Int focus,
        bool occupyingOnly)
    {
        if (subject == null || board == null)
        {
            yield break;
        }

        matchSequenceIndex++;
        int matchId = matchSequenceIndex;
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} START");

        RectTransform rect = subject.RectTransform;
        Vector2Int occupancy = subject.GridPosition;
        rect.anchoredPosition = board.GridToLocal(occupancy);
        subject.transform.localScale = subject.RestScale;
        if (subject == block)
        {
            logicalCell = occupancy;
        }

        if (!CollectChainFocusedMatch(board, subject, occupancy, focus, occupyingOnly, out Vector2Int targetWorld))
        {
            Debug.Log(
                $"REJECT MatchFocusedChainCell CollectChainFocusedMatch failed: " +
                $"Block={subject.GetInstanceID()} occupancy={occupancy} focus={focus} " +
                $"occupyingOnly={occupyingOnly} CellCount={subject.CellCount}");
            for (int i = 0; i < subject.CellCount; i++)
            {
                Vector2Int world = occupancy + subject.GetLocalCell(i);
                Target t = board.GetTargetAt(world);
                Debug.Log(
                    $"  cell[{i}] world={world} shape={subject.GetActiveShape(i)} " +
                    $"target={(t != null ? t.RequiredShape.ToString() : "NULL")} " +
                    $"occ={(board.GetBlockAt(world) != null ? board.GetBlockAt(world).GetInstanceID().ToString() : "NULL")}");
            }

            yield break;
        }

        int cellIndex = nestCellIndices[0];
        Vector2Int cellWorld = occupancy + subject.GetLocalCell(cellIndex);
        Target focusedTarget = nestTargets.Count > 0 ? nestTargets[0] : null;
        bool hadInner = subject.HasInnerLayerAt(cellIndex);
        subject.CancelDragSelectionImmediate();
        PieceGameplayVisuals.ClearConnectors(rect);

        // Multi-cell final match: NEVER TryMoveBlock the whole chain.
        // Only the focused cell traveler moves toward targetWorld; siblings stay put.
        // (1×1 magnet still uses TryMoveBlock in EnterMatchingTargetBody.)
        Debug.Log(
            $"[CHAIN MATCH] focused cell = {cellWorld}\n" +
            $"[CHAIN MATCH] target = {targetWorld}\n" +
            $"[CHAIN MATCH] chain cell count = {subject.CellCount}\n" +
            "[CHAIN MATCH] WHOLE CHAIN MOVE = FALSE");
        LogChainMatchCells("BEFORE", subject);

        pendingCellTraveler = null;
        yield return PlayChainCellNestEntry(board, subject, cellIndex, hadInner, cellWorld, targetWorld);
        RectTransform landedTraveler = pendingCellTraveler;
        pendingCellTraveler = null;
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} LAND");

        // Re-resolve the focused cell after the traveler. Do not use GetTargetAt(cellWorld)
        // as the destination authority when targetWorld differs from the source cell.
        cellIndex = FindCellIndexAtWorld(subject, cellWorld);
        if (!IsFocusedChainConsumeValid(subject, cellIndex, focusedTarget))
        {
            Debug.Log(
                $"REJECT MatchFocusedChainCell post-traveler consume invalid: " +
                $"cellIndex={cellIndex} cellWorld={cellWorld} " +
                $"target={(focusedTarget != null ? focusedTarget.GetInstanceID().ToString() : "NULL")} " +
                $"required={(focusedTarget != null ? focusedTarget.RequiredShape.ToString() : "n/a")} " +
                $"active={(cellIndex >= 0 && cellIndex < subject.CellCount ? subject.GetActiveShape(cellIndex).ToString() : "n/a")} " +
                $"settled={subject.IsSettled}");
            if (!hadInner && cellIndex >= 0)
            {
                subject.ClearTravelState(cellIndex);
                subject.SetCellVisualVisible(cellIndex, true);
            }

            subject.RefreshLayoutVisuals();
            DestroyLandedTraveler(landedTraveler);
            yield break;
        }

        nestCellIndices.Clear();
        nestTargets.Clear();
        nestCellIndices.Add(cellIndex);
        nestTargets.Add(focusedTarget);

        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CONSUME");
        bool fullyConsumed = ConsumeAndRebuild(
            board,
            subject,
            subject.GridPosition,
            out Target completedTarget,
            out ShapeType consumedShape,
            out Vector2Int _,
            out bool consumedInnerLayer);
        if (subject == block)
        {
            logicalCell = block != null ? block.GridPosition : logicalCell;
        }

        EnsureSubjectOccupancy(board, subject);
        Debug.Log(
            "[AUTO CHAIN SEQUENCE]\n" +
            $"Consumed cell: {cellWorld}\n" +
            $"Consumed shape: {consumedShape}\n" +
            $"Fully consumed block: {fullyConsumed}");
        LogChainMatchCells("AFTER", subject);
        LogChainAutoMatchPostMatch(board, subject, cellWorld, consumedShape);

        yield return PlayMatchEffect(
            board,
            targetWorld,
            consumedShape,
            completedTarget,
            fullyConsumed ? subject : null,
            matchId);

        DestroyLandedTraveler(landedTraveler);
        subject.ClearTravelState(cellIndex);
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CLEANUP COMPLETE");

        // Re-assert survivor occupancy after VFX; MatchEffect must not leave the
        // board unable to see an already-aligned remaining cell.
        EnsureSubjectOccupancy(board, subject);

        // TEMP DIAGNOSTIC: state the auto-match queue will see on the next scan.
        LogPostConsumeAutoMatchTrace(board, subject, cellWorld, targetWorld, fullyConsumed);
        if (!fullyConsumed && subject != null && !subject.IsSettled)
        {
            LogPostFirstMatchState(board, subject);
        }

        RememberLastMatch(cellWorld, targetWorld);

        if (!hadInner
            || fullyConsumed
            || !consumedInnerLayer
            || subject == null
            || subject.IsSettled)
        {
            yield break;
        }

        // Outer layer: same focused cell, same nest destination as the inner match.
        int outerIndex = FindCellIndexAtWorld(subject, cellWorld);
        if (!IsFocusedChainConsumeValid(subject, outerIndex, focusedTarget))
        {
            yield break;
        }

        matchSequenceIndex++;
        matchId = matchSequenceIndex;
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} START");

        nestCellIndices.Clear();
        nestTargets.Clear();
        nestCellIndices.Add(outerIndex);
        nestTargets.Add(focusedTarget);

        PieceGameplayVisuals.ClearConnectors(subject.RectTransform);
        pendingCellTraveler = null;
        yield return PlayChainCellNestEntry(board, subject, outerIndex, false, cellWorld, targetWorld);
        landedTraveler = pendingCellTraveler;
        pendingCellTraveler = null;
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} LAND");

        outerIndex = FindCellIndexAtWorld(subject, cellWorld);
        if (!IsFocusedChainConsumeValid(subject, outerIndex, focusedTarget))
        {
            if (outerIndex >= 0)
            {
                subject.ClearTravelState(outerIndex);
                subject.SetCellVisualVisible(outerIndex, true);
            }

            subject.RefreshLayoutVisuals();
            DestroyLandedTraveler(landedTraveler);
            yield break;
        }

        nestCellIndices.Clear();
        nestTargets.Clear();
        nestCellIndices.Add(outerIndex);
        nestTargets.Add(focusedTarget);

        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CONSUME");
        fullyConsumed = ConsumeAndRebuild(
            board,
            subject,
            subject.GridPosition,
            out completedTarget,
            out consumedShape,
            out _,
            out _);
        if (subject == block)
        {
            logicalCell = block != null ? block.GridPosition : logicalCell;
        }

        EnsureSubjectOccupancy(board, subject);

        // --- FIXED CODE ---
        DestroyLandedTraveler(landedTraveler);

        yield return PlayMatchEffect(
            board,
            targetWorld,
            consumedShape,
            completedTarget,
            fullyConsumed ? subject : null,
            matchId);

        subject.ClearTravelState(outerIndex);
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CLEANUP COMPLETE");
        RememberLastMatch(cellWorld, targetWorld);
    }

    private static void DestroyLandedTraveler(RectTransform traveler)
    {
        if (traveler != null)
        {
            Object.Destroy(traveler.gameObject);
        }
    }

    private static void LogChainMatchCells(string phase, Block subject)
    {
        if (subject == null || subject.IsSettled)
        {
            Debug.Log($"[CHAIN MATCH {phase}]\n(none)");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[CHAIN MATCH {phase}]");
        int count = Mathf.Max(1, subject.CellCount);
        for (int i = 0; i < count; i++)
        {
            Vector2Int world = subject.GridPosition + subject.GetLocalCell(i);
            sb.AppendLine($"Cell {i} = {world} shape={subject.GetActiveShape(i)}");
        }

        Debug.Log(sb.ToString());
    }

    /// <summary>Repairs footprint occupancy for a live block. Used after split/rebuild and by the auto-match queue.</summary>
    public static void EnsureSubjectOccupancy(BoardManager board, Block subject)
    {
        if (board == null || subject == null || subject.IsSettled)
        {
            return;
        }

        int count = Mathf.Max(1, subject.CellCount);
        for (int i = 0; i < count; i++)
        {
            Vector2Int world = subject.GridPosition + subject.GetLocalCell(i);
            if (board.GetBlockAt(world) != subject)
            {
                board.TryRegisterBlock(subject, subject.GridPosition);
                return;
            }
        }
    }

    private static bool IsFocusedChainConsumeValid(Block subject, int cellIndex, Target focusedTarget)
    {
        return subject != null
            && !subject.IsSettled
            && cellIndex >= 0
            && cellIndex < subject.CellCount
            && focusedTarget != null
            && focusedTarget.isActiveAndEnabled
            && focusedTarget.RequiredShape == subject.GetActiveShape(cellIndex);
    }

    private bool CollectChainFocusedMatch(
        BoardManager board,
        Block subject,
        Vector2Int occupancy,
        Vector2Int focus,
        bool occupyingOnly,
        out Vector2Int targetWorld)
    {
        nestCellIndices.Clear();
        nestTargets.Clear();
        targetWorld = occupancy;
        if (subject == null || board == null)
        {
            return false;
        }

        int count = subject.CellCount;
        for (int i = 0; i < count; i++)
        {
            Vector2Int world = occupancy + subject.GetLocalCell(i);
            Target target = board.GetTargetAt(world);
            if (target == null || target.RequiredShape != subject.GetActiveShape(i))
            {
                continue;
            }

            nestCellIndices.Add(i);
            nestTargets.Add(target);
        }

        if (nestCellIndices.Count > 0)
        {
            if (occupyingOnly)
            {
                KeepOnlyCellAtWorld(subject, occupancy, focus);
            }
            else
            {
                KeepOnlyNearestMatch(subject, occupancy, focus);
            }

            if (nestCellIndices.Count == 0)
            {
                return false;
            }

            int index = nestCellIndices[0];
            targetWorld = occupancy + subject.GetLocalCell(index);
            return true;
        }

        if (occupyingOnly)
        {
            return false;
        }

        Vector2Int delta = focus - occupancy;
        bool cardinalDelta = delta == Vector2Int.up
            || delta == Vector2Int.down
            || delta == Vector2Int.left
            || delta == Vector2Int.right;

        if (cardinalDelta)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2Int world = occupancy + subject.GetLocalCell(i);
                Vector2Int dest = world + delta;
                Target target = board.GetTargetAt(dest);
                if (target == null || target.RequiredShape != subject.GetActiveShape(i))
                {
                    continue;
                }

                nestCellIndices.Add(i);
                nestTargets.Add(target);
            }
        }
        else
        {
            // Auto-match may pass nestTo as a sibling cell's dest, not occupancy+dir.
            for (int i = 0; i < count; i++)
            {
                Vector2Int world = occupancy + subject.GetLocalCell(i);
                if (!IsFourAdjacent(world, focus))
                {
                    continue;
                }

                Target target = board.GetTargetAt(focus);
                if (target == null || target.RequiredShape != subject.GetActiveShape(i))
                {
                    continue;
                }

                nestCellIndices.Add(i);
                nestTargets.Add(target);
            }
        }

        if (nestCellIndices.Count == 0)
        {
            return false;
        }

        KeepOnlyNearestMatch(subject, occupancy, focus);
        int cellIndex = nestCellIndices[0];
        Vector2Int source = occupancy + subject.GetLocalCell(cellIndex);
        targetWorld = cardinalDelta ? source + delta : focus;
        return true;
    }

    private IEnumerator PlayChainCellNestEntry(
         BoardManager board,
         Block subject,
         int cellIndex,
         bool innerLayer,
         Vector2Int cellWorld,
         Vector2Int targetWorld)
    {
        if (innerLayer)
        {
            yield return PlayChainInnerNestEntry(board, subject, cellIndex, cellWorld, targetWorld);
            yield break;
        }

        Target nestTarget = nestTargets.Count > 0 ? nestTargets[0] : null;
        if (nestTarget != null)
        {
            nestTarget.ShowReadyFeedback();
        }

        PlayNestEntrySound();

        // 1. Instantiate traveler sprite FIRST while the cell visual is still visible
        RectTransform traveler = CreateTravelerCell(board, subject, cellIndex, cellWorld);
        pendingCellTraveler = traveler;

        // 2. Hide original cell visual on the block now that traveler exists
        subject.SetCellVisualVisible(cellIndex, false);

        Vector2 startPos = board.GridToLocal(cellWorld);
        Vector2 targetPos = board.GridToLocal(targetWorld);

        // 3. Pre-flight anticipation lift
        if (matchingTargetAnticipateDuration > 0f)
        {
            float anticipateElapsed = 0f;
            Vector3 baseScale = traveler != null ? traveler.localScale : subject.RestScale;

            while (anticipateElapsed < matchingTargetAnticipateDuration)
            {
                anticipateElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(anticipateElapsed / matchingTargetAnticipateDuration);

                if (traveler != null)
                {
                    float lift = t * (board.CellSize * matchingTargetAnticipateLiftPercent);
                    traveler.anchoredPosition = startPos + new Vector2(0f, lift);
                    traveler.localScale = Vector3.Lerp(baseScale, baseScale * matchingTargetAnticipateScale, t);
                }
                yield return null;
            }
        }

        // 4. Arc animation into the matching target cell
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, matchingTargetArcDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Vector2 currentPos = Vector2.Lerp(startPos, targetPos, t);
            float arcLift = Mathf.Sin(t * Mathf.PI) * (board.CellSize * matchingTargetLiftPercent);
            currentPos.y += arcLift;

            if (traveler != null)
            {
                traveler.anchoredPosition = currentPos;
                traveler.localScale = Vector3.Lerp(
                    subject.RestScale * matchingTargetAnticipateScale,
                    subject.RestScale * matchingTargetHopScale,
                    t
                );
            }

            yield return null;
        }

        // Snap traveler to exact target coordinates upon landing
        if (traveler != null)
        {
            traveler.anchoredPosition = targetPos;
            traveler.localScale = subject.RestScale;
        }

        if (matchingTargetSitDuration > 0f)
        {
            yield return Pause(matchingTargetSitDuration);
        }

        if (nestTarget != null)
        {
            nestTarget.HideReadyFeedback();
        }
    }

    private IEnumerator PlayChainInnerNestEntry(
        BoardManager board,
        Block subject,
        int cellIndex,
        Vector2Int cellWorld,
        Vector2Int targetWorld)
    {
        Target nestTarget = nestTargets.Count > 0 ? nestTargets[0] : null;
        if (nestTarget != null)
        {
            nestTarget.ShowReadyFeedback();
        }

        PlayNestEntrySound();

        Image cellImage = subject.GetCellImage(cellIndex);
        if (cellImage != null)
        {
            PieceGameplayVisuals.HideInnerOverlay(cellImage.transform);
        }

        PieceGameplayVisuals.NestedInnerLook look = subject.NestedInnerLook;
        RectTransform traveler = PieceGameplayVisuals.CreateTravelingInner(
            (RectTransform)board.transform,
            subject.ContainedInnerSprite(),
            subject.VisualSizeDelta,
            (Vector2)board.GridToLocal(cellWorld) + look.offset,
            look);

        if (traveler == null)
        {
            yield return Pause(matchingTargetPause);
            if (nestTarget != null)
            {
                nestTarget.HideReadyFeedback();
            }

            yield break;
        }

        pendingCellTraveler = traveler;
        Vector3 containedScale = subject.RestScale * look.scale;
        Vector3 emergedScale = subject.RestScale;
        traveler.localScale = containedScale;

        if (look.emergeDuration > 0f)
        {
            Vector2 emergeEnd = Vector2.Lerp(
                (Vector2)board.GridToLocal(cellWorld) + look.offset,
                (Vector2)board.GridToLocal(targetWorld),
                0.12f);
            yield return AnimateTraveler(
                traveler,
                traveler.anchoredPosition,
                emergeEnd,
                look.emergeDuration,
                containedScale,
                emergedScale,
                false);
        }
        else
        {
            traveler.localScale = emergedScale;
        }

        yield return Pause(matchingTargetPause);
        if (nestTarget != null)
        {
            nestTarget.HideReadyFeedback();
        }

        float liftAmount = board.VisualCellSize.y * matchingTargetAnticipateLiftPercent;
        Vector2 lifted = traveler.anchoredPosition + new Vector2(0f, liftAmount);
        Vector3 pumped = emergedScale * matchingTargetAnticipateScale;
        yield return AnimateTraveler(
            traveler,
            traveler.anchoredPosition,
            lifted,
            matchingTargetAnticipateDuration,
            emergedScale,
            pumped,
            false);

        Vector2 start = traveler.anchoredPosition;
        Vector2 end = board.GridToLocal(targetWorld);
        Vector2 control = (((Vector2)board.GridToLocal(cellWorld) + end) * 0.5f)
            + new Vector2(0f, board.VisualCellSize.y * matchingTargetLiftPercent);
        Vector3 hopScale = emergedScale * matchingTargetHopScale;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, matchingTargetArcDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInOutCubic(t);
            traveler.anchoredPosition = QuadraticBezier(start, control, end, eased);
            traveler.localScale = Vector3.LerpUnclamped(emergedScale, hopScale, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        yield return AnimateTraveler(
            traveler,
            traveler.anchoredPosition,
            end,
            matchingTargetSitDuration,
            traveler.localScale,
            emergedScale,
            true);
        traveler.anchoredPosition = end;
        traveler.localScale = emergedScale;
    }

    private RectTransform CreateTravelerCell(BoardManager board, Block sourceBlock, int cellIndex, Vector2Int worldPos)
    {
        if (sourceBlock == null || board == null || cellIndex < 0 || cellIndex >= sourceBlock.CellCount)
        {
            return null;
        }

        Image sourceImage = sourceBlock.GetCellImage(cellIndex);
        if (sourceImage == null)
        {
            return PieceGameplayVisuals.CreateTravelingSprite(
                (RectTransform)board.transform,
                sourceBlock.GetCellOuterSprite(cellIndex),
                sourceBlock.VisualSizeDelta,
                board.GridToLocal(worldPos));
        }

        // The anchor cell is the Block root, so clone only its Image instead of
        // cloning the Block/BlockMover hierarchy. Extra cells can retain their
        // nested overlay hierarchy when cloned from the resolved cell image.
        GameObject travelerObj;
        if (sourceImage.gameObject == sourceBlock.gameObject)
        {
            RectTransform traveler = PieceGameplayVisuals.CreateTravelingSprite(
                (RectTransform)board.transform,
                sourceBlock.GetCellOuterSprite(cellIndex),
                sourceBlock.VisualSizeDelta,
                board.GridToLocal(worldPos));
            if (traveler == null)
            {
                return null;
            }

            traveler.localScale = sourceBlock.RestScale;
            return traveler;
        }

        travelerObj = Instantiate(sourceImage.gameObject, board.transform);
        travelerObj.SetActive(true);

        // Ensure all visual graphics on the traveler are active and non-raycastable
        Graphic[] graphics = travelerObj.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].gameObject.SetActive(true);
            graphics[i].raycastTarget = false;
        }

        RectTransform travelerRect = travelerObj.GetComponent<RectTransform>();
        travelerRect.anchoredPosition = board.GridToLocal(worldPos);
        travelerRect.localScale = sourceBlock.RestScale;

        return travelerRect;
    }
    
    private IEnumerator EnterNestedInnerThenOuter(
        BoardManager board,
        RectTransform rect,
        Vector2Int from,
        Vector2Int to)
    {
        dragReleased = true;
        LogDrag($"Nested inner nest {from} -> {to}");

        board.CollectNestMatches(block, to, nestCellIndices, nestTargets);
        KeepOnlyFirstMatch();
        if (nestCellIndices.Count == 0)
        {
            yield break;
        }

        Target nestTarget = nestTargets[0];
        if (nestTarget != null)
        {
            nestTarget.ShowReadyFeedback();
        }

        block.CancelDragSelectionImmediate();
        block.HideContainedInnerVisuals();
        PlayNestEntrySound();

        Vector2 restPosition = board.GridToLocal(from);
        Vector3 restScale = block.RestScale;
        rect.anchoredPosition = restPosition;
        block.transform.localScale = restScale;

        yield return PresentInnerEmergenceAndEntry(board, rect, from, to, restPosition, restScale);
        RectTransform landedTraveler = pendingCellTraveler;
        pendingCellTraveler = null;

        if (nestTarget != null)
        {
            nestTarget.HideReadyFeedback();
        }

        board.CollectNestMatches(block, to, nestCellIndices, nestTargets);
        KeepOnlyFirstMatch();

        logicalCell = from;
        block.SetGridPosition(from);
        board.TryMoveBlock(block, block.GridPosition, from);

        matchSequenceIndex++;
        int matchId = matchSequenceIndex;
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} LAND");
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CONSUME");

        bool fullyConsumed = ConsumeAndRebuild(
            board,
            block,
            from,
            out Target completedTarget,
            out ShapeType consumedShape,
            out Vector2Int _,
            out bool consumedInnerLayer);

        logicalCell = block.GridPosition;
        RememberLastMatch(from, to);
        yield return PlayMatchEffect(
            board,
            to,
            consumedShape,
            completedTarget,
            fullyConsumed ? block : null,
            matchId);

        DestroyLandedTraveler(landedTraveler);
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CLEANUP COMPLETE");

        if (fullyConsumed || block == null || block.IsSettled || !consumedInnerLayer)
        {
            if (!IsAutoMatchRunning && levelManager != null)
            {
                levelManager.NotifyBlockSettled();
            }

            yield break;
        }

        if (IsAutoMatchRunning)
        {
            yield break;
        }

        if (board.HasNestMatch(block, to)
            || TryGetAdjacentMatchingTarget(board, block.GridPosition, out Vector2Int nestCell) && nestCell == to)
        {
            yield return EnterMatchingTarget(board, rect, block.GridPosition, to);
            yield break;
        }

        if (levelManager != null)
        {
            levelManager.NotifyBlockSettled();
        }
    }

    private IEnumerator PresentInnerEmergenceAndEntry(
        BoardManager board,
        RectTransform blockRect,
        Vector2Int from,
        Vector2Int to,
        Vector2 restPosition,
        Vector3 restScale)
    {
        PieceGameplayVisuals.NestedInnerLook look = block.NestedInnerLook;
        RectTransform boardRect = blockRect.parent as RectTransform;
        Sprite innerSprite = block.ContainedInnerSprite();
        RectTransform traveler = PieceGameplayVisuals.CreateTravelingInner(
            boardRect,
            innerSprite,
            block.VisualSizeDelta,
            restPosition + look.offset,
            look);

        if (traveler == null)
        {
            yield return Pause(matchingTargetPause);
            yield break;
        }

        Vector3 containedScale = restScale * look.scale;
        Vector3 emergedScale = restScale;
        traveler.localScale = containedScale;

        if (look.emergeDuration > 0f)
        {
            Vector2 emergeEnd = Vector2.Lerp(restPosition + look.offset, board.GridToLocal(to), 0.12f);
            yield return AnimateTraveler(
                traveler,
                traveler.anchoredPosition,
                emergeEnd,
                look.emergeDuration,
                containedScale,
                emergedScale,
                false);
        }
        else
        {
            traveler.localScale = emergedScale;
        }

        yield return Pause(matchingTargetPause);

        float liftAmount = board.VisualCellSize.y * matchingTargetAnticipateLiftPercent;
        Vector2 lifted = traveler.anchoredPosition + new Vector2(0f, liftAmount);
        Vector3 pumped = emergedScale * matchingTargetAnticipateScale;
        yield return AnimateTraveler(
            traveler,
            traveler.anchoredPosition,
            lifted,
            matchingTargetAnticipateDuration,
            emergedScale,
            pumped,
            false);

        Vector2 start = traveler.anchoredPosition;
        Vector2 end = board.GridToLocal(to);
        Vector2 lift = new Vector2(0f, board.VisualCellSize.y * matchingTargetLiftPercent);
        Vector2 control = ((restPosition + end) * 0.5f) + lift;
        Vector3 hopScale = emergedScale * matchingTargetHopScale;

        float arcDuration = Mathf.Max(0.01f, matchingTargetArcDuration);
        float elapsed = 0f;
        while (elapsed < arcDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / arcDuration);
            float eased = EaseInOutCubic(t);
            traveler.anchoredPosition = QuadraticBezier(start, control, end, eased);
            float squashT = Mathf.Sin(t * Mathf.PI);
            traveler.localScale = Vector3.LerpUnclamped(emergedScale, hopScale, squashT);
            yield return null;
        }

        yield return AnimateTraveler(
            traveler,
            traveler.anchoredPosition,
            end,
            matchingTargetSitDuration,
            traveler.localScale,
            emergedScale,
            true);
        traveler.anchoredPosition = end;
        traveler.localScale = emergedScale;

        // Keep traveler through match VFX; EnterNestedInnerThenOuter destroys after PlayMatchEffect.
        pendingCellTraveler = traveler;
    }

    private static IEnumerator AnimateTraveler(
        RectTransform traveler,
        Vector2 from,
        Vector2 to,
        float duration,
        Vector3 scaleFrom,
        Vector3 scaleTo,
        bool easeOut)
    {
        if (traveler == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            traveler.anchoredPosition = to;
            traveler.localScale = scaleTo;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeOut ? 1f - ((1f - t) * (1f - t)) : Mathf.SmoothStep(0f, 1f, t);
            traveler.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            traveler.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, eased);
            yield return null;
        }

        traveler.anchoredPosition = to;
        traveler.localScale = scaleTo;
    }

    private IEnumerator ResolveCellMatches(
        BoardManager board,
        Vector2Int from,
        Vector2Int to,
        Target effectTarget)
    {
        KeepOnlyFirstMatch();
        matchSequenceIndex++;
        int matchId = matchSequenceIndex;
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CONSUME");
        bool fullyConsumed = ConsumeAndRebuild(
            board,
            block,
            to,
            out Target completedTarget,
            out ShapeType consumedShape,
            out Vector2Int effectCell,
            out bool consumedInnerLayer);
        logicalCell = block.GridPosition;
        RememberLastMatch(from, to);

        yield return PlayMatchEffect(
            board,
            effectCell,
            consumedShape,
            completedTarget,
            fullyConsumed ? block : null,
            matchId);
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CLEANUP COMPLETE");

        if (!fullyConsumed
            && consumedInnerLayer
            && block != null
            && !block.IsSettled
            && block.CellCount == 1
            && board.HasNestMatch(block, block.GridPosition)
            && !IsAutoMatchRunning)
        {
            yield return PlayNestedOuterNestEntry(board, block);
        }

        if (!IsAutoMatchRunning)
        {
            yield return ResolveAlreadyAlignedMatches(board);
        }
    }

    private IEnumerator PlayNestedOuterNestEntry(BoardManager board, Block subject)
    {
        if (subject == null || board == null)
        {
            yield break;
        }

        Vector2Int here = subject.GridPosition;
        board.CollectNestMatches(subject, here, nestCellIndices, nestTargets);
        KeepOnlyFirstMatch();
        if (nestCellIndices.Count == 0)
        {
            yield break;
        }

        Target nestTarget = nestTargets[0];
        if (nestTarget != null)
        {
            nestTarget.ShowReadyFeedback();
        }

        subject.CancelDragSelectionImmediate();
        PlayNestEntrySound();

        RectTransform rect = subject.RectTransform;
        Vector2 restPosition = board.GridToLocal(here);
        Vector3 restScale = subject.RestScale;
        rect.anchoredPosition = restPosition;
        subject.transform.localScale = restScale;

        yield return Pause(matchingTargetPause);
        if (nestTarget != null)
        {
            nestTarget.HideReadyFeedback();
        }

        yield return AnimateAnticipation(board, rect, restPosition, restScale);
        yield return AnimateNestEntry(board, rect, here, here, restScale);
        subject.SetGridPosition(here);
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchSequenceIndex + 1} LAND");

        board.CollectNestMatches(subject, here, nestCellIndices, nestTargets);
        KeepOnlyFirstMatch();
        if (nestCellIndices.Count == 0)
        {
            yield break;
        }

        matchSequenceIndex++;
        int matchId = matchSequenceIndex;
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CONSUME");
        bool fullyConsumed = ConsumeAndRebuild(
            board,
            subject,
            here,
            out Target completedTarget,
            out ShapeType consumedShape,
            out Vector2Int effectCell,
            out bool _);
        if (subject == block)
        {
            logicalCell = block.GridPosition;
        }

        yield return PlayMatchEffect(
            board,
            effectCell,
            consumedShape,
            completedTarget,
            fullyConsumed ? subject : null,
            matchId);
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CLEANUP COMPLETE");
        RememberLastMatch(here, here);
    }

    private IEnumerator ResolveAlreadyAlignedMatches(BoardManager board)
    {
        if (levelManager == null || board == null)
        {
            yield break;
        }

        yield return levelManager.WaitForAlignedMatchQueue();
    }

    public IEnumerator PlayResolvedAutoMatch(BoardManager board, Vector2Int nestTo)
    {
        // Always refresh from this GameObject — never trust a stale Block field after split/rebuild.
        block = GetComponent<Block>();

        if (block == null || board == null)
        {
            yield break;
        }

        EnsureSubjectOccupancy(board, block);

        if (block.CellCount == 1 && block.HasActiveInnerLayer())
        {
            yield return PlayAlignedNestedMatch(board, nestTo);
            yield break;
        }

        // Occupying match: traveler sits in place. Adjacent nest: focused-cell traveler only.
        // Never TryMoveBlock the whole chain.
        bool occupying = IsWorldCellOccupyingAlignedMatch(board, block, nestTo);
        yield return MatchFocusedChainCell(board, block, block.GridPosition, nestTo, occupying);
    }

    /// <summary>
    /// TEMP: after a chain match completes, dump survivor vs target for the next fresh scan.
    /// </summary>
    public static void LogAutoChainSequenceAfterMatch(BoardManager board, Block survivorBeforeNull)
    {
        Debug.Log("[AUTO CHAIN SEQUENCE]\nMATCH COMPLETE");

        if (board == null)
        {
            Debug.Log("[AUTO CHAIN SEQUENCE] board null");
            return;
        }

        var unique = new List<Block>();
        board.CollectUniqueBlocks(unique);
        if (unique.Count == 0)
        {
            Debug.Log(
                "[AUTO CHAIN SEQUENCE]\nRemaining Block: NONE\n" +
                "(board empty — next scan should end the queue)");
            return;
        }

        for (int i = 0; i < unique.Count; i++)
        {
            Block b = unique[i];
            if (b == null || b.IsSettled)
            {
                continue;
            }

            int count = Mathf.Max(1, b.CellCount);
            for (int c = 0; c < count; c++)
            {
                Vector2Int world = b.GridPosition + b.GetLocalCell(c);
                Target target = board.GetTargetAt(world);
                string reject = ExplainAlignedCellRejection(board, b, c, world, null);
                bool candidate = reject == null;
                Debug.Log(
                    "[AUTO CHAIN SEQUENCE]\n" +
                    $"Remaining Block: {b.GetInstanceID()}\n" +
                    $"Remaining cell: {c}\n" +
                    $"Remaining shape: {b.GetActiveShape(c)}\n" +
                    $"Remaining world: {world}\n" +
                    $"Target at remaining world: {(target != null ? target.RequiredShape.ToString() : "NULL")}\n" +
                    $"Occupying owner OK: {board.GetBlockAt(world) == b}\n" +
                    $"Triangle candidate = {candidate}\n" +
                    $"Reject: {(reject ?? "none")}");
            }
        }
    }

    private bool IsAutoMatchRunning =>
        resolvingAligned || (levelManager != null && levelManager.IsAlignedMatchRunning);

    public IEnumerator PlayAlignedNestedMatch(BoardManager board)
    {
        yield return PlayAlignedNestedMatch(board, block != null ? block.GridPosition : Vector2Int.zero);
    }

    public IEnumerator PlayAlignedNestedMatch(BoardManager board, Vector2Int nestTo)
    {
        if (block == null)
        {
            yield break;
        }

        bool wasResolving = resolvingAligned;
        resolvingAligned = true;
        Vector2Int here = block.GridPosition;
        yield return EnterNestedInnerThenOuter(board, block.RectTransform, here, nestTo);
        resolvingAligned = wasResolving;
    }

    public IEnumerator PlayAlignedMagnetMatch(BoardManager board, Vector2Int nestTo)
    {
        if (block == null || board == null)
        {
            yield break;
        }

        bool wasResolving = resolvingAligned;
        resolvingAligned = true;
        yield return EnterMatchingTargetBody(board, block.RectTransform, block.GridPosition, nestTo);
        resolvingAligned = wasResolving;
    }

    private IEnumerator PlaySimpleAlignedNestEntry(BoardManager board, Block subject)
    {
        yield return PlaySimpleAlignedNestEntry(board, subject, subject != null ? subject.GridPosition : Vector2Int.zero);
    }

    private IEnumerator PlaySimpleAlignedNestEntry(BoardManager board, Block subject, Vector2Int nestTo)
    {
        if (subject == null || board == null)
        {
            yield break;
        }

        EnsureSubjectOccupancy(board, subject);
        Vector2Int here = subject.GridPosition;
        matchSequenceIndex++;
        int matchId = matchSequenceIndex;
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} START");

        board.CollectNestMatches(subject, here, nestCellIndices, nestTargets);
        KeepOnlyFirstMatch();
        if (nestCellIndices.Count == 0)
        {
            Target target = board.GetTargetAt(here);
            Block occupant = board.GetBlockAt(here);
            Debug.Log(
                $"REJECT PlaySimpleAlignedNestEntry Block={subject.GetInstanceID()} here={here} nestTo={nestTo}:\n" +
                $"- CollectNestMatches empty (shape={subject.GetActiveShape(0)} " +
                $"target={(target != null ? target.RequiredShape.ToString() : "NULL")} " +
                $"occupant={(occupant != null ? occupant.GetInstanceID().ToString() : "NULL")} " +
                $"GetBlockAt(nestTo)={(board.GetBlockAt(nestTo) != null ? board.GetBlockAt(nestTo).GetInstanceID().ToString() : "NULL")})");
            yield break;
        }

        Target nestTarget = nestTargets[0];
        int lockedCellIndex = nestCellIndices[0];
        if (nestTarget != null)
        {
            nestTarget.ShowReadyFeedback();
        }

        subject.CancelDragSelectionImmediate();
        PlayNestEntrySound();

        RectTransform rect = subject.RectTransform;
        Vector2 restPosition = board.GridToLocal(here);
        Vector3 restScale = subject.RestScale;
        rect.anchoredPosition = restPosition;
        subject.transform.localScale = restScale;

        yield return Pause(matchingTargetPause);
        if (nestTarget != null)
        {
            nestTarget.HideReadyFeedback();
        }

        yield return AnimateSubjectAnticipation(board, subject, restPosition, restScale);
        yield return AnimateSubjectNestEntry(board, subject, here, here, restScale);
        subject.SetGridPosition(here);
        EnsureSubjectOccupancy(board, subject);
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} LAND");

        // Lock the pre-animation match (same pattern as MatchFocusedChainCell).
        // Do not re-CollectNestMatches after the traveler — that can soft-fail in Play Mode
        // and skip a still-valid occupying survivor.
        int cellIndex = lockedCellIndex;
        if (cellIndex < 0
            || cellIndex >= subject.CellCount
            || nestTarget == null
            || !nestTarget.isActiveAndEnabled
            || nestTarget.RequiredShape != subject.GetActiveShape(cellIndex))
        {
            cellIndex = FindCellIndexAtWorld(subject, here);
        }

        if (cellIndex < 0
            || nestTarget == null
            || !nestTarget.isActiveAndEnabled
            || nestTarget.RequiredShape != subject.GetActiveShape(cellIndex))
        {
            Debug.Log(
                $"REJECT PlaySimpleAlignedNestEntry post-animation Block={subject.GetInstanceID()} here={here}:\n" +
                $"- locked match invalid (cellIndex={cellIndex} " +
                $"target={(nestTarget != null ? nestTarget.RequiredShape.ToString() : "NULL")} " +
                $"active={(cellIndex >= 0 && cellIndex < subject.CellCount ? subject.GetActiveShape(cellIndex).ToString() : "n/a")})");
            yield break;
        }

        nestCellIndices.Clear();
        nestTargets.Clear();
        nestCellIndices.Add(cellIndex);
        nestTargets.Add(nestTarget);

        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CONSUME");
        bool fullyConsumed = ConsumeAndRebuild(
            board,
            subject,
            here,
            out Target completedTarget,
            out ShapeType consumedShape,
            out Vector2Int effectCell,
            out bool _);
        if (subject == block)
        {
            logicalCell = block.GridPosition;
        }

        yield return PlayMatchEffect(
            board,
            effectCell,
            consumedShape,
            completedTarget,
            fullyConsumed ? subject : null,
            matchId);
        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} CLEANUP COMPLETE");
        RememberLastMatch(here, here);
    }

    private IEnumerator AnimateSubjectAnticipation(
        BoardManager board,
        Block subject,
        Vector2 restPosition,
        Vector3 restScale)
    {
        float liftAmount = board.VisualCellSize.y * matchingTargetAnticipateLiftPercent;
        Vector2 lifted = restPosition + new Vector2(0f, liftAmount);
        Vector3 pumped = restScale * matchingTargetAnticipateScale;
        yield return AnimateAnchoredOn(
            subject.RectTransform,
            subject.transform,
            restPosition,
            lifted,
            matchingTargetAnticipateDuration,
            restScale,
            pumped,
            false);
    }

    private IEnumerator AnimateSubjectNestEntry(
        BoardManager board,
        Block subject,
        Vector2Int from,
        Vector2Int to,
        Vector3 restScale)
    {
        RectTransform rect = subject.RectTransform;
        Vector2 start = rect.anchoredPosition;
        Vector2 end = board.GridToLocal(to);
        Vector2 restPosition = board.GridToLocal(from);
        float liftAmount = board.VisualCellSize.y * matchingTargetLiftPercent;
        Vector2 lift = new Vector2(0f, liftAmount);
        Vector2 control = ((restPosition + end) * 0.5f) + lift;
        Vector3 hopScale = restScale * matchingTargetHopScale;

        float arcDuration = Mathf.Max(0.01f, matchingTargetArcDuration);
        float elapsed = 0f;
        while (elapsed < arcDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / arcDuration);
            float eased = EaseInOutCubic(t);
            rect.anchoredPosition = QuadraticBezier(start, control, end, eased);
            float squashT = Mathf.Sin(t * Mathf.PI);
            subject.transform.localScale = Vector3.LerpUnclamped(restScale, hopScale, squashT);
            yield return null;
        }

        yield return AnimateAnchoredOn(
            rect,
            subject.transform,
            rect.anchoredPosition,
            end,
            matchingTargetSitDuration,
            subject.transform.localScale,
            restScale,
            true);
        rect.anchoredPosition = end;
        subject.transform.localScale = restScale;
    }

    private static IEnumerator AnimateAnchoredOn(
        RectTransform rect,
        Transform scaleRoot,
        Vector2 from,
        Vector2 to,
        float duration,
        Vector3 scaleFrom,
        Vector3 scaleTo,
        bool easeOut)
    {
        if (rect == null || scaleRoot == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            rect.anchoredPosition = to;
            scaleRoot.localScale = scaleTo;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeOut ? 1f - ((1f - t) * (1f - t)) : Mathf.SmoothStep(0f, 1f, t);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            scaleRoot.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, eased);
            yield return null;
        }

        rect.anchoredPosition = to;
        scaleRoot.localScale = scaleTo;
    }

    private void KeepOnlyFirstMatch()
    {
        if (nestCellIndices.Count <= 1)
        {
            return;
        }

        int cellIndex = nestCellIndices[0];
        Target target = nestTargets.Count > 0 ? nestTargets[0] : null;
        nestCellIndices.Clear();
        nestTargets.Clear();
        nestCellIndices.Add(cellIndex);
        nestTargets.Add(target);
    }

    private void KeepOnlyNearestMatch(Block subject, Vector2Int occupancyAnchor, Vector2Int focusWorld)
    {
        if (subject == null || nestCellIndices.Count == 0)
        {
            return;
        }

        if (nestCellIndices.Count == 1)
        {
            return;
        }

        int best = 0;
        int bestDist = int.MaxValue;
        for (int i = 0; i < nestCellIndices.Count; i++)
        {
            Vector2Int world = occupancyAnchor + subject.GetLocalCell(nestCellIndices[i]);
            int dist = Mathf.Abs(world.x - focusWorld.x) + Mathf.Abs(world.y - focusWorld.y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        int cellIndex = nestCellIndices[best];
        Target target = best < nestTargets.Count ? nestTargets[best] : null;
        nestCellIndices.Clear();
        nestTargets.Clear();
        nestCellIndices.Add(cellIndex);
        nestTargets.Add(target);
    }

    private void KeepOnlyCellAtWorld(Block subject, Vector2Int occupancyAnchor, Vector2Int world)
    {
        if (subject == null)
        {
            nestCellIndices.Clear();
            nestTargets.Clear();
            return;
        }

        for (int i = 0; i < nestCellIndices.Count; i++)
        {
            if (occupancyAnchor + subject.GetLocalCell(nestCellIndices[i]) != world)
            {
                continue;
            }

            int cellIndex = nestCellIndices[i];
            Target target = i < nestTargets.Count ? nestTargets[i] : null;
            nestCellIndices.Clear();
            nestTargets.Clear();
            nestCellIndices.Add(cellIndex);
            nestTargets.Add(target);
            return;
        }

        nestCellIndices.Clear();
        nestTargets.Clear();
    }

    private static int FindCellIndexAtWorld(Block subject, Vector2Int world)
    {
        if (subject == null)
        {
            return -1;
        }

        int count = subject.CellCount;
        for (int i = 0; i < count; i++)
        {
            if (subject.GetCellWorld(i) == world)
            {
                return i;
            }
        }

        return -1;
    }

    public static bool TryFindNextAlignedMatch(
        BoardManager board,
        List<Block> scratch,
        HashSet<int> skipIds,
        bool hasLastMatch,
        Vector2Int lastMatchOrigin,
        Vector2Int lastMatchTargetCell,
        out Block subject,
        out Vector2Int nestTo)
    {
        subject = null;
        nestTo = Vector2Int.zero;
        if (board == null || scratch == null)
        {
            return false;
        }

        board.CollectUniqueBlocks(scratch);
        LogAutoMatchScan(board, scratch, skipIds);
        int bestPriority = int.MaxValue;
        int bestY = int.MaxValue;
        int bestX = int.MaxValue;
        for (int i = 0; i < scratch.Count; i++)
        {
            Block candidate = scratch[i];
            if (candidate == null || candidate.IsSettled || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            int count = Mathf.Max(1, candidate.CellCount);
            for (int cellIndex = 0; cellIndex < count; cellIndex++)
            {
                Vector2Int world = candidate.GridPosition + candidate.GetLocalCell(cellIndex);
                Vector2Int dest = world;
                string reject = ExplainAlignedCellRejection(
                    board,
                    candidate,
                    cellIndex,
                    world,
                    skipIds);
                if (reject != null)
                {
                    // Adjacent one-cell nest is only for follow-up after a finished match,
                    // not for the initial load scan.
                    if (!hasLastMatch
                        || !TryGetAdjacentAutoMatchDest(
                            board,
                            candidate,
                            cellIndex,
                            world,
                            skipIds,
                            out dest,
                            out _))
                    {
                        continue;
                    }
                }

                int priority = AlignedMatchPriority(
                    hasLastMatch,
                    world,
                    dest,
                    lastMatchOrigin,
                    lastMatchTargetCell);
                if (priority > bestPriority)
                {
                    continue;
                }

                if (priority == bestPriority
                    && (dest.y > bestY || (dest.y == bestY && dest.x >= bestX)))
                {
                    continue;
                }

                bestPriority = priority;
                bestY = dest.y;
                bestX = dest.x;
                subject = candidate;
                nestTo = dest;
            }
        }

        if (subject != null)
        {
            Debug.Log(
                $"[AUTO MATCH SCAN] SELECTED Block={subject.GetInstanceID()} nestTo={nestTo} " +
                $"shape={subject.GetActiveShape(0)} priority={bestPriority}",
                subject);
        }
        else
        {
            Debug.Log("[AUTO MATCH SCAN] SELECTED none");
            LogSelectedNoneDump(board, scratch, skipIds);
        }

        return subject != null;
    }

    private static void LogSelectedNoneDump(BoardManager board, List<Block> scratch, HashSet<int> skipIds)
    {
        if (board == null)
        {
            return;
        }

        Debug.Log(
            $"[AUTO MATCH SCAN] SELECTED-none dump: occupancyUnique={(scratch != null ? scratch.Count : -1)} " +
            $"skipCount={(skipIds != null ? skipIds.Count : 0)} board={board.Width}x{board.Height}");

        Block[] children = board.GetComponentsInChildren<Block>(true);
        Debug.Log($"[AUTO MATCH SCAN] Child Block count under board={children.Length}");
        for (int i = 0; i < children.Length; i++)
        {
            Block b = children[i];
            if (b == null)
            {
                continue;
            }

            Vector2Int world = b.GridPosition;
            Block occ = board.GetBlockAt(world);
            Target target = board.GetTargetAt(world);
            string reject = ExplainAlignedCellRejection(board, b, 0, world, skipIds);
            Debug.Log(
                $"[AUTO MATCH SCAN] ORPHAN-CHECK Block={b.GetInstanceID()} Grid={world} " +
                $"CellCount={b.CellCount} Shape={b.GetActiveShape(0)} Settled={b.IsSettled} " +
                $"Active={b.isActiveAndEnabled} GetBlockAt={(occ != null ? occ.GetInstanceID().ToString() : "NULL")} " +
                $"same={(occ == b)} Target={(target != null ? target.RequiredShape.ToString() : "NULL")} " +
                $"reject={(reject ?? "none")}");
        }
    }

    /// <summary>
    /// TEMP DIAGNOSTIC: per-cell occupying auto-match scan with exact rejection reasons.
    /// </summary>
    public static void LogAutoMatchScan(BoardManager board, List<Block> scratch, HashSet<int> skipIds)
    {
        if (board == null)
        {
            Debug.Log("[AUTO MATCH SCAN] board null");
            return;
        }

        if (scratch == null)
        {
            scratch = new List<Block>();
        }

        // Always refresh from occupancy — callers may pass an empty list.
        board.CollectUniqueBlocks(scratch);

        Debug.Log($"[AUTO MATCH SCAN] uniqueBlocks={scratch.Count}");
        for (int i = 0; i < scratch.Count; i++)
        {
            Block candidate = scratch[i];
            if (candidate == null)
            {
                Debug.Log("[AUTO MATCH SCAN] Block: null");
                continue;
            }

            int instanceId = candidate.GetInstanceID();
            int count = Mathf.Max(1, candidate.CellCount);
            Debug.Log(
                $"[AUTO MATCH SCAN] Block: {instanceId} GridPosition={candidate.GridPosition} " +
                $"CellCount={candidate.CellCount} Active={candidate.isActiveAndEnabled} " +
                $"Settled={candidate.IsSettled} InCollectUnique=TRUE");

            for (int cellIndex = 0; cellIndex < count; cellIndex++)
            {
                Vector2Int local = candidate.GetLocalCell(cellIndex);
                Vector2Int world = candidate.GridPosition + local;
                Target target = board.GetTargetAt(world);
                Block occupant = board.GetBlockAt(world);
                ShapeType offered = candidate.GetActiveShape(cellIndex);
                string reject = ExplainAlignedCellRejection(
                    board,
                    candidate,
                    cellIndex,
                    world,
                    skipIds);
                bool isCandidate = reject == null;

                Debug.Log(
                    "[AUTO MATCH SCAN]\n" +
                    $"Block: {instanceId}\n" +
                    $"Cell: {cellIndex}\n" +
                    $"Local: {local}\n" +
                    $"World: {world}\n" +
                    $"Shape: {offered}\n" +
                    $"Target: {(target != null ? target.GetInstanceID().ToString() : "NULL")}\n" +
                    $"RequiredShape: {(target != null ? target.RequiredShape.ToString() : "n/a")}\n" +
                    $"OccupyingBlock: {(occupant != null ? occupant.GetInstanceID().ToString() : "NULL")}\n" +
                    $"IsActive: {candidate.isActiveAndEnabled}\n" +
                    $"IsSettled: {candidate.IsSettled}\n" +
                    $"Candidate: {isCandidate}");

                if (!isCandidate)
                {
                    Debug.Log($"REJECT cell {cellIndex} of Block {instanceId} at {world}:\n- {reject}");
                }
            }
        }

        LogRemainingTargets(board);
    }

    public static string ExplainAlignedCellRejection(
        BoardManager board,
        Block candidate,
        int cellIndex,
        Vector2Int world,
        HashSet<int> skipIds)
    {
        if (candidate == null)
        {
            return "block null";
        }

        if (candidate.IsSettled)
        {
            return "block considered moving/settled";
        }

        if (!candidate.isActiveAndEnabled)
        {
            return "block inactive/not alive";
        }

        if (skipIds != null && skipIds.Contains(AutoMatchSkipKey(candidate.GetInstanceID(), world)))
        {
            return "candidate skipped by previous-match key";
        }

        Block occupant = board != null ? board.GetBlockAt(world) : null;
        if (occupant != candidate)
        {
            return occupant == null
                ? "occupancy missing"
                : $"occupancy mismatch (GetBlockAt={occupant.GetInstanceID()} expected={candidate.GetInstanceID()})";
        }

        Target target = board != null ? board.GetTargetAt(world) : null;
        if (target == null)
        {
            return "target not found";
        }

        if (!target.isActiveAndEnabled)
        {
            return "target inactive";
        }

        ShapeType offered = candidate.GetActiveShape(cellIndex);
        if (target.RequiredShape != offered)
        {
            return $"shape mismatch (block={offered} required={target.RequiredShape})";
        }

        Vector2Int expectedWorld = candidate.GridPosition + candidate.GetLocalCell(cellIndex);
        if (expectedWorld != world)
        {
            return $"world/local mismatch (expected {expectedWorld} got {world})";
        }

        return null;
    }

    private static readonly Vector2Int[] AutoMatchCardinals =
    {
        Vector2Int.up,
        Vector2Int.down,
        Vector2Int.left,
        Vector2Int.right
    };

    /// <summary>
    /// One-cell nest next to an occupied chain cell. Traveler-only; dest must be empty of blocks.
    /// </summary>
    public static bool TryGetAdjacentAutoMatchDest(
        BoardManager board,
        Block candidate,
        int cellIndex,
        Vector2Int sourceWorld,
        HashSet<int> skipIds,
        out Vector2Int dest,
        out string reject)
    {
        dest = sourceWorld;
        reject = "no adjacent matching target";
        if (board == null || candidate == null || candidate.IsSettled || !candidate.isActiveAndEnabled)
        {
            reject = candidate == null
                ? "block null"
                : candidate.IsSettled
                    ? "block considered moving/settled"
                    : "block inactive/not alive";
            return false;
        }

        if (board.GetBlockAt(sourceWorld) != candidate)
        {
            reject = "occupancy missing";
            return false;
        }

        ShapeType offered = candidate.GetActiveShape(cellIndex);
        for (int i = 0; i < AutoMatchCardinals.Length; i++)
        {
            Vector2Int next = sourceWorld + AutoMatchCardinals[i];
            if (skipIds != null && skipIds.Contains(AutoMatchSkipKey(candidate.GetInstanceID(), next)))
            {
                continue;
            }

            if (board.GetBlockAt(next) != null)
            {
                continue;
            }

            Target target = board.GetTargetAt(next);
            if (target == null || !target.isActiveAndEnabled)
            {
                continue;
            }

            if (target.RequiredShape != offered)
            {
                continue;
            }

            dest = next;
            reject = null;
            return true;
        }

        return false;
    }

    public static bool IsChainCellAutoMatchValid(BoardManager board, Block candidate, Vector2Int nestTo)
    {
        if (IsWorldCellOccupyingAlignedMatch(board, candidate, nestTo))
        {
            return true;
        }

        if (board == null || candidate == null || candidate.IsSettled || !candidate.isActiveAndEnabled)
        {
            return false;
        }

        Target destTarget = board.GetTargetAt(nestTo);
        if (destTarget == null || !destTarget.isActiveAndEnabled || board.GetBlockAt(nestTo) != null)
        {
            return false;
        }

        int count = Mathf.Max(1, candidate.CellCount);
        for (int i = 0; i < count; i++)
        {
            Vector2Int world = candidate.GridPosition + candidate.GetLocalCell(i);
            if (board.GetBlockAt(world) != candidate)
            {
                continue;
            }

            if (!IsFourAdjacent(world, nestTo))
            {
                continue;
            }

            if (destTarget.RequiredShape == candidate.GetActiveShape(i))
            {
                return true;
            }
        }

        return false;
    }

    public static void LogChainAutoMatchPostMatch(
        BoardManager board,
        Block survivor,
        Vector2Int consumedWorld,
        ShapeType consumedShape)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== CHAIN AUTO MATCH POST MATCH #1 ===");
        sb.AppendLine($"Consumed cell: {consumedWorld}");
        sb.AppendLine($"Consumed shape: {consumedShape}");
        sb.AppendLine();
        if (survivor == null || survivor.IsSettled)
        {
            sb.AppendLine("SURVIVING BLOCK");
            sb.AppendLine("Block ID: NONE");
            sb.AppendLine("=== END POST MATCH #1 ===");
            Debug.Log(sb.ToString());
            return;
        }

        sb.AppendLine("SURVIVING BLOCK");
        sb.AppendLine($"Block ID: {survivor.GetInstanceID()}");
        sb.AppendLine($"GridPosition: {survivor.GridPosition}");
        sb.AppendLine($"CellCount: {survivor.CellCount}");
        sb.AppendLine();

        int count = Mathf.Max(1, survivor.CellCount);
        for (int i = 0; i < count; i++)
        {
            Vector2Int world = survivor.GridPosition + survivor.GetLocalCell(i);
            Target occupying = board != null ? board.GetTargetAt(world) : null;
            Block occ = board != null ? board.GetBlockAt(world) : null;
            bool foundAdj = TryGetAdjacentAutoMatchDest(
                board,
                survivor,
                i,
                world,
                null,
                out Vector2Int adjDest,
                out string adjReject);
            sb.AppendLine($"CELL {i}");
            sb.AppendLine($"World: {world}");
            sb.AppendLine($"Local: {survivor.GetLocalCell(i)}");
            sb.AppendLine($"ActiveShape: {survivor.GetActiveShape(i)}");
            sb.AppendLine($"TargetAtWorld: {(occupying != null ? occupying.RequiredShape.ToString() : "NULL")}");
            sb.AppendLine($"TargetRequiredShape: {(occupying != null ? occupying.RequiredShape.ToString() : "n/a")}");
            sb.AppendLine($"GetBlockAtWorld == this: {occ == survivor}");
            sb.AppendLine(
                $"AdjacentDest: {(foundAdj ? adjDest.ToString() : "none")} " +
                $"reject={(adjReject ?? "none")}");
            sb.AppendLine(
                $"OccupyingCandidate: {ExplainAlignedCellRejection(board, survivor, i, world, null) == null}");
            sb.AppendLine($"AdjacentCandidate: {foundAdj}");
            sb.AppendLine();
        }

        sb.AppendLine("ALL TARGETS");
        if (board != null)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Target target = board.GetTargetAt(cell);
                    if (target == null)
                    {
                        continue;
                    }

                    sb.AppendLine($"Target world: {cell} RequiredShape: {target.RequiredShape}");
                }
            }
        }

        sb.AppendLine("=== END POST MATCH #1 ===");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Single dump after a partial consume: survivor cell vs target vs occupancy validity.
    /// </summary>
    public static void LogPostFirstMatchState(BoardManager board, Block survivor)
    {
        if (survivor == null || survivor.IsSettled)
        {
            Debug.Log("POST-FIRST-MATCH STATE\nBlock NONE");
            return;
        }

        Vector2Int world = survivor.GridPosition;
        Block occ = board != null ? board.GetBlockAt(world) : null;
        Target target = board != null ? board.GetTargetAt(world) : null;
        bool valid = IsWorldCellOccupyingAlignedMatch(board, survivor, world);
        string reject = ExplainAlignedCellRejection(board, survivor, 0, world, null);
        Debug.Log(
            "POST-FIRST-MATCH STATE\n" +
            $"Block {survivor.GetInstanceID()}\n" +
            $"Cell 0\n" +
            $"World {world}\n" +
            $"Shape {survivor.GetActiveShape(0)}\n" +
            $"TargetAtCell {(target != null ? target.GetInstanceID().ToString() : "NULL")}\n" +
            $"RequiredShape {(target != null ? target.RequiredShape.ToString() : "n/a")}\n" +
            $"OccupancyOwner {(occ != null ? occ.GetInstanceID().ToString() : "NULL")}\n" +
            $"ValidOccupyingMatch {valid}\n" +
            $"Reject {(reject ?? "none")}");
    }

    public static void LogPostConsumeAutoMatchTrace(
        BoardManager board,
        Block subject,
        Vector2Int consumedWorld,
        Vector2Int consumedTargetWorld,
        bool fullyConsumed)
    {
        Debug.Log(
            $"[AUTO MATCH POST-CONSUME] consumedWorld={consumedWorld} targetWorld={consumedTargetWorld} " +
            $"fullyConsumed={fullyConsumed} LastConsumeSucceeded={LastConsumeSucceeded}");

        if (subject == null || subject.IsSettled)
        {
            Debug.Log("[AUTO MATCH POST-CONSUME] survivor Block: NONE (settled or null)");
            LogRemainingTargets(board);
            return;
        }

        int id = subject.GetInstanceID();
        int count = Mathf.Max(1, subject.CellCount);
        Debug.Log(
            $"[AUTO MATCH POST-CONSUME] survivor exists=YES id={id} GridPosition={subject.GridPosition} " +
            $"CellCount={subject.CellCount} ShapeType={subject.ShapeType} ActiveShape0={subject.GetActiveShape(0)} " +
            $"Settled={subject.IsSettled} Active={subject.isActiveAndEnabled}");

        bool inUnique = false;
        if (board != null)
        {
            var unique = new List<Block>();
            board.CollectUniqueBlocks(unique);
            for (int i = 0; i < unique.Count; i++)
            {
                if (unique[i] == subject)
                {
                    inUnique = true;
                    break;
                }
            }
        }

        Debug.Log($"[AUTO MATCH POST-CONSUME] CollectUniqueBlocks contains survivor: {inUnique}");

        for (int i = 0; i < count; i++)
        {
            Vector2Int local = subject.GetLocalCell(i);
            Vector2Int world = subject.GridPosition + local;
            Block occupant = board != null ? board.GetBlockAt(world) : null;
            Target target = board != null ? board.GetTargetAt(world) : null;
            Debug.Log(
                $"[AUTO MATCH POST-CONSUME] cell[{i}] local={local} world={world} " +
                $"activeShape={subject.GetActiveShape(i)} " +
                $"GetBlockAt={(occupant != null ? occupant.GetInstanceID().ToString() : "NULL")} " +
                $"sameAsSurvivor={occupant == subject} " +
                $"Target={(target != null ? target.GetInstanceID().ToString() : "NULL")} " +
                $"Required={(target != null ? target.RequiredShape.ToString() : "n/a")}");

            if (target != null)
            {
                Debug.Log(
                    $"[AUTO MATCH POST-CONSUME] coord compare cell[{i}]: " +
                    $"blockWorld={world} targetWorld={target.GridPosition} " +
                    $"equal={world == target.GridPosition}");
            }
        }

        LogRemainingTargets(board);

        if (board != null)
        {
            var scratch = new List<Block>();
            LogAutoMatchScan(board, scratch, null);
            bool found = TryFindNextAlignedMatch(
                board,
                scratch,
                null,
                true,
                consumedWorld,
                consumedTargetWorld,
                out Block next,
                out Vector2Int nestTo);
            Debug.Log(
                $"[AUTO MATCH POST-CONSUME] immediate next candidate found={found} " +
                $"block={(next != null ? next.GetInstanceID().ToString() : "NULL")} nestTo={nestTo}");
        }
    }

    public static void LogRemainingTargets(BoardManager board)
    {
        if (board == null)
        {
            Debug.Log("[AUTO MATCH TARGETS] board null");
            return;
        }

        // BoardManager does not expose a target enumerator; probe every cell.
        Debug.Log($"[AUTO MATCH TARGETS] Remaining targets on {board.Width}x{board.Height}:");
        for (int y = 0; y < board.Height; y++)
        {
            for (int x = 0; x < board.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Target target = board.GetTargetAt(cell);
                if (target == null)
                {
                    continue;
                }

                Debug.Log(
                    $"[AUTO MATCH TARGETS] Target {target.GetInstanceID()} → world {cell} " +
                    $"(GridPosition={target.GridPosition}) RequiredShape={target.RequiredShape} " +
                    $"Active={target.isActiveAndEnabled}");
            }
        }
    }

    public static int AutoMatchSkipKey(int instanceId, Vector2Int cell)
    {
        return (instanceId * 397) ^ (cell.x * 17) ^ (cell.y * 31);
    }

    public bool TryRevalidateAlignedCandidate(BoardManager board, Vector2Int nestTo)
    {
        if (board == null)
        {
            return false;
        }

        if (block == null)
        {
            block = GetComponent<Block>();
        }

        if (block == null)
        {
            Debug.Log("REJECT TryRevalidateAlignedCandidate:\n- BlockMover.block is null");
            return false;
        }

        EnsureSubjectOccupancy(board, block);
        bool ok = IsWorldCellOccupyingAlignedMatch(board, block, nestTo);
        if (!ok)
        {
            int cellIndex = 0;
            for (int i = 0; i < block.CellCount; i++)
            {
                if (block.GridPosition + block.GetLocalCell(i) == nestTo)
                {
                    cellIndex = i;
                    break;
                }
            }

            string reason = ExplainAlignedCellRejection(board, block, cellIndex, nestTo, null)
                ?? "revalidate failed (no footprint cell at nestTo)";
            Debug.Log($"REJECT TryRevalidateAlignedCandidate Block={block.GetInstanceID()} nestTo={nestTo}:\n- {reason}");
        }

        return ok;
    }

    public static bool IsWorldCellOccupyingAlignedMatch(BoardManager board, Block candidate, Vector2Int world)
    {
        if (board == null || candidate == null || candidate.IsSettled || !candidate.isActiveAndEnabled)
        {
            return false;
        }

        if (board.GetBlockAt(world) != candidate)
        {
            return false;
        }

        Target target = board.GetTargetAt(world);
        if (target == null || !target.isActiveAndEnabled)
        {
            return false;
        }

        int count = Mathf.Max(1, candidate.CellCount);
        for (int i = 0; i < count; i++)
        {
            if (candidate.GridPosition + candidate.GetLocalCell(i) != world)
            {
                continue;
            }

            return target.RequiredShape == candidate.GetActiveShape(i);
        }

        return false;
    }

    public static int AlignedMatchPriority(
        bool hasLastMatch,
        Vector2Int alignedCell,
        Vector2Int nestTo,
        Vector2Int lastMatchOrigin,
        Vector2Int lastMatchTargetCell)
    {
        if (hasLastMatch && (alignedCell == lastMatchOrigin || nestTo == lastMatchTargetCell))
        {
            return 0;
        }

        bool originAdjacent = hasLastMatch && IsFourAdjacent(alignedCell, lastMatchOrigin);
        bool targetAdjacent = hasLastMatch && IsFourAdjacent(nestTo, lastMatchTargetCell);
        if (originAdjacent && targetAdjacent)
        {
            return 1;
        }

        if (originAdjacent || targetAdjacent)
        {
            return 2;
        }

        return 3;
    }

    public static bool IsOccupyingAlignedMatch(
        Vector2Int blockCell,
        Vector2Int targetCell,
        ShapeType offered,
        ShapeType required)
    {
        return blockCell == targetCell && offered == required;
    }

    private void RememberLastMatch(Vector2Int origin, Vector2Int targetCell)
    {
        hasLastMatch = true;
        lastMatchOrigin = origin;
        lastMatchTargetCell = targetCell;
        if (levelManager != null)
        {
            levelManager.RememberLastMatch(origin, targetCell);
        }
    }

    public static bool IsFourAdjacent(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1;
    }

    private bool ConsumeAndRebuild(
        BoardManager board,
        Block subject,
        Vector2Int anchor,
        out Target completedTarget,
        out ShapeType consumedShape,
        out Vector2Int effectCell,
        out bool consumedInnerLayer)
    {
        LastConsumeSucceeded = false;
        completedTarget = null;
        consumedShape = subject != null ? subject.GetActiveShape(0) : ShapeType.Square;
        effectCell = anchor;
        consumedInnerLayer = false;
        if (subject == null || nestCellIndices.Count == 0)
        {
            return false;
        }

        var consumedIndices = new HashSet<int>();
        bool consumedAny = false;
        int cellIndex = nestCellIndices[0];
        Vector2Int occupancyAnchor = subject.GridPosition;
        effectCell = occupancyAnchor + subject.GetLocalCell(cellIndex);
        Target target = nestTargets.Count > 0 ? nestTargets[0] : null;
        ShapeType offered = subject.GetActiveShape(cellIndex);
        if (target != null && target.TryConsumeLayer(offered, out bool targetComplete))
        {
            consumedShape = offered;
            consumedAny = true;
            LastConsumeSucceeded = true;
            ShapeCellData cell = subject.GetCell(cellIndex);
            bool cellGone = true;
            if (cell != null)
            {
                bool hadInner = cell.innerShapes != null && cell.innerShapes.Count > 0;
                cellGone = !hadInner;
                if (hadInner)
                {
                    ShapeLayout.TryConsumeLayer(cell, offered);
                }
            }

            if (cellGone)
            {
                consumedIndices.Add(cellIndex);
            }
            else
            {
                consumedInnerLayer = true;
            }

            if (targetComplete)
            {
                completedTarget = target;
                target.BeginMatchPresentation();
                board.UnregisterTarget(target);
            }
        }

        if (!consumedAny)
        {
            return false;
        }

        if (consumedIndices.Count == 0)
        {
            // Inner layer only: keep the cell, occupancy, and chain topology intact.
            subject.RefreshActiveLayers();
            return false;
        }

        splitWorlds.Clear();
        splitCells.Clear();
        int count = subject.CellCount;
        for (int i = 0; i < count; i++)
        {
            if (consumedIndices.Contains(i))
            {
                continue;
            }

            ShapeCellData source = subject.GetCell(i);
            splitWorlds.Add(occupancyAnchor + subject.GetLocalCell(i));
            splitCells.Add(new ShapeCellData
            {
                localPosition = Vector2Int.zero,
                shapeType = source != null ? source.shapeType : subject.GetActiveShape(i),
                innerShapes = source != null
                    ? ShapeLayout.CloneInners(source.innerShapes)
                    : new List<ShapeType>()
            });
        }

        board.UnregisterBlock(subject);
        Debug.Log(
            $"[AUTO MATCH CONSUME] Unregistered Block={subject.GetInstanceID()} " +
            $"consumedIndex={cellIndex} remainingWorlds={splitWorlds.Count} " +
            $"CellCountWas={count}");
        ShapeLayout.SplitConnected(splitWorlds, splitCells, splitAnchors, splitComponents);

        Debug.Log(
            $"[AUTO MATCH CONSUME] SplitConnected components={splitComponents.Count} " +
            $"anchors={(splitAnchors.Count > 0 ? string.Join(",", splitAnchors) : "none")}");

        if (splitComponents.Count == 0)
        {
            subject.BeginMatchPresentation();
            subject.Settle();
            Debug.Log(
                $"[AUTO MATCH CONSUME] No survivors — settled Block={subject.GetInstanceID()}");
            return true;
        }

        // 1. REBUILD THE PRIMARY SURVIVOR
        subject.RebuildFromRemaining(splitComponents[0], splitAnchors[0]);

        // --- FORCE RESET BOARD GRID REGISTRATION ---
        // Completely strip it from the board's tracking and re-add it as a fresh active block
        board.UnregisterBlock(subject);
        bool ok = board.TryRegisterBlock(subject, splitAnchors[0]);

        if (!ok)
        {
            Debug.LogWarning($"[AUTO MATCH CONSUME] Failed to force-register primary survivor Block={subject.GetInstanceID()} at {splitAnchors[0]}");
        }
        else
        {
            // If your BoardManager has a list of active falling/moving blocks, 
            // make sure it's added back there so it isn't treated as a background tile.
            // Example: board.AddActiveBlock(subject); 

            Debug.Log($"[AUTO MATCH CONSUME] Force re-registered primary survivor Block={subject.GetInstanceID()} at {subject.GridPosition}");
        }

        // 2. HANDLE ADDITIONAL SPLIT SURVIVORS
        for (int i = 1; i < splitComponents.Count; i++)
        {
            if (levelManager != null)
            {
                Block newSplitBlock = levelManager.SpawnSplitBlock(subject, splitComponents[i], splitAnchors[i]);

                if (newSplitBlock != null)
                {
                    // Ensure the brand new block is registered tightly into the grid
                    board.UnregisterBlock(newSplitBlock);
                    board.TryRegisterBlock(newSplitBlock, splitAnchors[i]);
                }
            }
        }

        return false;
    }

    private Vector2Int ClampDragDestination(
        BoardManager board,
        Vector2Int start,
        Vector2Int direction,
        Vector2Int requested)
    {
        int maxSteps = AxisSteps(requested - start, direction);
        if (maxSteps <= 0)
        {
            return start;
        }

        Vector2Int current = start;
        int steps = 0;
        while (steps < maxSteps)
        {
            Vector2Int next = current + direction;
            if (!board.CanTranslateBlock(block, next) || board.FootprintTouchesTarget(block, next))
            {
                return current;
            }

            current = next;
            steps++;
        }

        return current;
    }

    private static int AxisSteps(Vector2Int offset, Vector2Int direction)
    {
        return (offset.x * direction.x) + (offset.y * direction.y);
    }

    private BoardManager GetBoard()
    {
        if (block == null)
        {
            return null;
        }

        return block.Board != null ? block.Board : GetComponentInParent<BoardManager>();
    }

    private bool IsMatchingTargetCell(BoardManager board, Vector2Int cell)
    {
        return board.HasNestMatch(block, cell);
    }

    private IEnumerator AnimateHop(
        BoardManager board,
        RectTransform rect,
        Vector2Int from,
        Vector2Int to,
        float duration,
        bool anticipate)
    {
        Vector2 startPosition = board.GridToLocal(from);
        Vector2 endPosition = board.GridToLocal(to);
        Vector3 scaleAtStart = block.transform.localScale;

        if (anticipate && normalHopAnticipateDuration > 0f && normalHopAnticipatePercent > 0f)
        {
            Vector2 cellSize = board.VisualCellSize;
            float axisSize = dragDirection.x != 0 ? cellSize.x : cellSize.y;
            Vector2 windup = startPosition - ((Vector2)dragDirection * (axisSize * normalHopAnticipatePercent));
            yield return AnimateAnchoredPosition(rect, startPosition, windup, normalHopAnticipateDuration, easeOut: false);
            startPosition = windup;
        }

        yield return AnimateHopTravel(board, rect, startPosition, endPosition, duration, scaleAtStart);
        rect.anchoredPosition = endPosition;
        block.transform.localScale = scaleAtStart;
    }

    private IEnumerator AnimateHopTravel(
        BoardManager board,
        RectTransform rect,
        Vector2 from,
        Vector2 to,
        float duration,
        Vector3 scaleAtStart)
    {
        if (duration <= 0f)
        {
            rect.anchoredPosition = to;
            yield break;
        }

        Vector3 squash = scaleAtStart * hopTravelScale;
        bool squashHop = hopTravelScale < 0.999f;
        float liftAmount = board != null ? board.VisualCellSize.y * hopLiftPercent : 0f;
        Vector2 control = ((from + to) * 0.5f) + new Vector2(0f, liftAmount);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseInOutCubic(t);
            rect.anchoredPosition = liftAmount > 0.001f
                ? QuadraticBezier(from, control, to, eased)
                : Vector2.LerpUnclamped(from, to, eased);
            if (squashHop)
            {
                float squashT = Mathf.Sin(t * Mathf.PI);
                squashT *= squashT;
                block.transform.localScale = Vector3.LerpUnclamped(scaleAtStart, squash, squashT);
            }

            yield return null;
        }

        rect.anchoredPosition = to;
        block.transform.localScale = scaleAtStart;
    }

    private static IEnumerator AnimateAnchoredPosition(
        RectTransform rect,
        Vector2 from,
        Vector2 to,
        float duration,
        bool easeOut)
    {
        if (duration <= 0f)
        {
            rect.anchoredPosition = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeOut ? EaseOutQuad(t) : Mathf.SmoothStep(0f, 1f, t);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }

        rect.anchoredPosition = to;
    }

    private static float EaseOutQuad(float t)
    {
        return 1f - ((1f - t) * (1f - t));
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.5f)
        {
            return 4f * t * t * t;
        }

        float f = -2f * t + 2f;
        return 1f - ((f * f * f) * 0.5f);
    }

    private static float EaseInQuad(float t)
    {
        return t * t;
    }

    private IEnumerator AnimateAnticipation(
        BoardManager board,
        RectTransform rect,
        Vector2 restPosition,
        Vector3 restScale)
    {
        float liftAmount = board.VisualCellSize.y * matchingTargetAnticipateLiftPercent;
        Vector2 lifted = restPosition + new Vector2(0f, liftAmount);
        Vector3 pumped = restScale * matchingTargetAnticipateScale;
        yield return AnimateAnchored(
            rect,
            restPosition,
            lifted,
            matchingTargetAnticipateDuration,
            restScale,
            pumped,
            false);
    }

    private IEnumerator AnimateNestEntry(
        BoardManager board,
        RectTransform rect,
        Vector2Int from,
        Vector2Int to,
        Vector3 restScale)
    {
        Vector2 start = rect.anchoredPosition;
        Vector2 end = board.GridToLocal(to);
        Vector2 restPosition = board.GridToLocal(from);
        float liftAmount = board.VisualCellSize.y * matchingTargetLiftPercent;
        Vector2 lift = new Vector2(0f, liftAmount);
        Vector2 control = ((restPosition + end) * 0.5f) + lift;
        Vector3 hopScale = restScale * matchingTargetHopScale;

        float arcDuration = Mathf.Max(0.01f, matchingTargetArcDuration);
        float elapsed = 0f;
        while (elapsed < arcDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / arcDuration);
            float eased = EaseInOutCubic(t);
            rect.anchoredPosition = QuadraticBezier(start, control, end, eased);
            float squashT = Mathf.Sin(t * Mathf.PI);
            block.transform.localScale = Vector3.LerpUnclamped(restScale, hopScale, squashT);
            yield return null;
        }

        yield return AnimateAnchored(rect, rect.anchoredPosition, end, matchingTargetSitDuration, block.transform.localScale, restScale, true);
        rect.anchoredPosition = end;
        block.transform.localScale = restScale;
    }

    private IEnumerator PlayMatchEffect(
        BoardManager board,
        Vector2Int nestCell,
        ShapeType glowShape,
        Target nestTarget,
        Block dissolvingBlock,
        int matchId = -1)
    {
        if (matchId < 0)
        {
            matchSequenceIndex++;
            matchId = matchSequenceIndex;
        }

        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} VFX START");
        PlayMatchSound();

        if (matchEffectPrefab == null)
        {
            yield return PulseTarget(board, nestCell);
            if (dissolvingBlock != null)
            {
                dissolvingBlock.CompleteMatchPresentation();
            }

            if (nestTarget != null)
            {
                nestTarget.CompleteMatchPresentation();
            }

            Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} VFX COMPLETE");
            yield break;
        }

        RectTransform boardRect = (RectTransform)board.transform;
        MatchEffect effect = Instantiate(matchEffectPrefab, boardRect, false);
        activeMatchEffect = effect;
        RectTransform effectRect = effect.RectTransform;
        Vector2 cellSize = board.VisualCellSize;
        effectRect.sizeDelta = cellSize;
        effectRect.anchoredPosition = board.GridToLocal(nestCell);
        int glowSibling = boardRect.childCount;
        if (nestTarget != null)
        {
            glowSibling = Mathf.Min(glowSibling, nestTarget.RectTransform.GetSiblingIndex());
        }

        if (dissolvingBlock != null)
        {
            glowSibling = Mathf.Min(glowSibling, dissolvingBlock.RectTransform.GetSiblingIndex());
        }

        effectRect.SetSiblingIndex(glowSibling);

        try
        {
            // Host on this BlockMover so LevelManager-nested auto-match enumerators
            // cannot resume until MatchEffect.Play (impact + dissolve) fully finishes.
            yield return StartCoroutine(effect.Play(glowShape, dissolvingBlock, nestTarget));
        }
        finally
        {
            if (effect != null)
            {
                Destroy(effect.gameObject);
            }

            if (activeMatchEffect == effect)
            {
                activeMatchEffect = null;
            }
        }

        Debug.Log($"[MATCH SEQUENCE] MATCH {matchId} VFX COMPLETE");
    }

    private IEnumerator PulseTarget(BoardManager board, Vector2Int nestCell)
    {
        Target target = board.GetTargetAt(nestCell);
        if (target == null)
        {
            yield break;
        }

        target.HideReadyFeedback();

        if (matchingTargetPulseDuration <= 0f || matchingTargetPulseScale <= 1f)
        {
            yield break;
        }

        RectTransform targetRect = target.RectTransform;
        Vector3 restScale = targetRect.localScale;
        Vector3 peakScale = restScale * matchingTargetPulseScale;
        float half = matchingTargetPulseDuration * 0.45f;
        yield return AnimateTransformScale(targetRect, restScale, peakScale, half, false);
        yield return AnimateTransformScale(targetRect, peakScale, restScale, matchingTargetPulseDuration - half, true);
        targetRect.localScale = restScale;
    }

    private static IEnumerator AnimateTransformScale(
        RectTransform rect,
        Vector3 from,
        Vector3 to,
        float duration,
        bool easeOut)
    {
        if (duration <= 0f)
        {
            rect.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeOut ? 1f - (1f - t) * (1f - t) : Mathf.SmoothStep(0f, 1f, t);
            rect.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        rect.localScale = to;
    }

    private IEnumerator AnimateAnchored(
        RectTransform rect,
        Vector2 from,
        Vector2 to,
        float duration,
        Vector3 scaleFrom,
        Vector3 scaleTo,
        bool easeOut)
    {
        if (duration <= 0f)
        {
            rect.anchoredPosition = to;
            block.transform.localScale = scaleTo;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeOut ? 1f - (1f - t) * (1f - t) : Mathf.SmoothStep(0f, 1f, t);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            block.transform.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, eased);
            yield return null;
        }

        rect.anchoredPosition = to;
        block.transform.localScale = scaleTo;
    }

    private static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    private static IEnumerator Pause(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void LogDrag(string message)
    {
        if (debugDrag)
        {
            Debug.Log($"BlockMover: {message}", this);
        }
    }
}
