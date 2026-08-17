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
    private readonly List<Block> alignedScanBlocks = new List<Block>();
    private bool resolvingAligned;

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

        if (levelManager != null && !levelManager.IsGameplayInputAllowed)
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
        yield return MatchFocusedChainCell(board, subject, from, focus);
        yield return ResolveAlreadyAlignedMatches(board);
        if (levelManager != null)
        {
            levelManager.NotifyBlockSettled();
        }
    }

    private IEnumerator MatchFocusedChainCell(
        BoardManager board,
        Block subject,
        Vector2Int from,
        Vector2Int focus)
    {
        if (subject == null || board == null)
        {
            yield break;
        }

        RectTransform rect = subject.RectTransform;
        Vector2Int occupancy = subject.GridPosition;
        rect.anchoredPosition = board.GridToLocal(occupancy);
        subject.transform.localScale = subject.RestScale;
        if (subject == block)
        {
            logicalCell = occupancy;
        }

        if (!CollectChainFocusedMatch(board, subject, occupancy, focus, out Vector2Int targetWorld))
        {
            yield break;
        }

        int cellIndex = nestCellIndices[0];
        Vector2Int cellWorld = occupancy + subject.GetLocalCell(cellIndex);
        bool hadInner = subject.HasInnerLayerAt(cellIndex);
        subject.CancelDragSelectionImmediate();
        PieceGameplayVisuals.ClearConnectors(rect);

        yield return PlayChainCellNestEntry(board, subject, cellIndex, hadInner, cellWorld, targetWorld);

        if (nestCellIndices.Count == 0)
        {
            yield break;
        }

        bool fullyConsumed = ConsumeAndRebuild(
            board,
            subject,
            occupancy,
            out Target completedTarget,
            out ShapeType consumedShape,
            out Vector2Int effectCell,
            out bool consumedInnerLayer);
        if (subject == block)
        {
            logicalCell = block.GridPosition;
        }

        yield return PlayMatchEffect(
            board,
            targetWorld,
            consumedShape,
            completedTarget,
            fullyConsumed ? subject : null);

        if (fullyConsumed || subject == null || subject.IsSettled || !consumedInnerLayer)
        {
            yield break;
        }

        int outerIndex = FindCellIndexAtWorld(subject, cellWorld);
        Target outerTarget = board.GetTargetAt(targetWorld);
        if (outerIndex < 0
            || outerTarget == null
            || outerTarget.RequiredShape != subject.GetActiveShape(outerIndex))
        {
            yield break;
        }

        nestCellIndices.Clear();
        nestTargets.Clear();
        nestCellIndices.Add(outerIndex);
        nestTargets.Add(outerTarget);

        yield return PlayChainCellNestEntry(board, subject, outerIndex, false, cellWorld, targetWorld);

        fullyConsumed = ConsumeAndRebuild(
            board,
            subject,
            subject.GridPosition,
            out completedTarget,
            out consumedShape,
            out effectCell,
            out _);
        if (subject == block)
        {
            logicalCell = block.GridPosition;
        }

        yield return PlayMatchEffect(
            board,
            targetWorld,
            consumedShape,
            completedTarget,
            fullyConsumed ? subject : null);
    }

    private bool CollectChainFocusedMatch(
        BoardManager board,
        Block subject,
        Vector2Int occupancy,
        Vector2Int focus,
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
            KeepOnlyNearestMatch(subject, occupancy, focus);
            int index = nestCellIndices[0];
            targetWorld = occupancy + subject.GetLocalCell(index);
            return true;
        }

        Vector2Int delta = focus - occupancy;
        if (delta != Vector2Int.up
            && delta != Vector2Int.down
            && delta != Vector2Int.left
            && delta != Vector2Int.right)
        {
            return false;
        }

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

        if (nestCellIndices.Count == 0)
        {
            return false;
        }

        KeepOnlyNearestMatch(subject, occupancy, focus);
        int cellIndex = nestCellIndices[0];
        targetWorld = occupancy + subject.GetLocalCell(cellIndex) + delta;
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
        Target nestTarget = nestTargets.Count > 0 ? nestTargets[0] : null;
        if (nestTarget != null)
        {
            nestTarget.ShowReadyFeedback();
        }

        PlayNestEntrySound();
        Vector2 startLocal = board.GridToLocal(cellWorld);
        Vector2 endLocal = board.GridToLocal(targetWorld);
        Vector3 restScale = Vector3.one;
        RectTransform boardRect = subject.RectTransform.parent as RectTransform;
        RectTransform traveler;

        if (innerLayer)
        {
            Image cellImage = subject.GetCellImage(cellIndex);
            if (cellImage != null)
            {
                PieceGameplayVisuals.HideInnerOverlay(cellImage.transform);
            }

            PieceGameplayVisuals.NestedInnerLook look = subject.NestedInnerLook;
            traveler = PieceGameplayVisuals.CreateTravelingInner(
                boardRect,
                subject.GetCellVisualSprite(cellIndex),
                subject.VisualSizeDelta,
                startLocal + look.offset,
                look);
            if (traveler != null)
            {
                Vector3 containedScale = restScale * look.scale;
                traveler.localScale = containedScale;
                if (look.emergeDuration > 0f)
                {
                    Vector2 emergeEnd = Vector2.Lerp(startLocal + look.offset, endLocal, 0.12f);
                    yield return AnimateTraveler(
                        traveler,
                        traveler.anchoredPosition,
                        emergeEnd,
                        look.emergeDuration,
                        containedScale,
                        restScale,
                        false);
                }
                else
                {
                    traveler.localScale = restScale;
                }
            }
        }
        else
        {
            subject.SetCellVisualVisible(cellIndex, false);
            traveler = PieceGameplayVisuals.CreateTravelingSprite(
                boardRect,
                subject.GetCellOuterSprite(cellIndex),
                subject.VisualSizeDelta,
                startLocal);
        }

        yield return Pause(matchingTargetPause);
        if (nestTarget != null)
        {
            nestTarget.HideReadyFeedback();
        }

        if (traveler == null)
        {
            yield break;
        }

        float liftAmount = board.VisualCellSize.y * matchingTargetAnticipateLiftPercent;
        Vector2 lifted = traveler.anchoredPosition + new Vector2(0f, liftAmount);
        Vector3 pumped = restScale * matchingTargetAnticipateScale;
        yield return AnimateTraveler(
            traveler,
            traveler.anchoredPosition,
            lifted,
            matchingTargetAnticipateDuration,
            traveler.localScale,
            pumped,
            false);

        Vector2 start = traveler.anchoredPosition;
        Vector2 end = endLocal;
        Vector2 lift = new Vector2(0f, board.VisualCellSize.y * matchingTargetLiftPercent);
        Vector2 control = ((startLocal + end) * 0.5f) + lift;
        Vector3 hopScale = restScale * matchingTargetHopScale;
        float arcDuration = Mathf.Max(0.01f, matchingTargetArcDuration);
        float elapsed = 0f;
        while (elapsed < arcDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / arcDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            traveler.anchoredPosition = QuadraticBezier(start, control, end, eased);
            traveler.localScale = Vector3.LerpUnclamped(traveler.localScale, hopScale, eased);
            yield return null;
        }

        yield return AnimateTraveler(
            traveler,
            traveler.anchoredPosition,
            end,
            matchingTargetSitDuration,
            traveler.localScale,
            restScale,
            true);

        if (traveler != null)
        {
            Destroy(traveler.gameObject);
        }
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

        if (nestTarget != null)
        {
            nestTarget.HideReadyFeedback();
        }

        board.CollectNestMatches(block, to, nestCellIndices, nestTargets);
        KeepOnlyFirstMatch();

        logicalCell = from;
        block.SetGridPosition(from);
        board.TryMoveBlock(block, block.GridPosition, from);

        bool fullyConsumed = ConsumeAndRebuild(
            board,
            block,
            from,
            out Target completedTarget,
            out ShapeType consumedShape,
            out Vector2Int _,
            out bool consumedInnerLayer);

        logicalCell = block.GridPosition;
        yield return PlayMatchEffect(
            board,
            to,
            consumedShape,
            completedTarget,
            fullyConsumed ? block : null);

        if (fullyConsumed || block == null || block.IsSettled || !consumedInnerLayer)
        {
            if (!resolvingAligned && levelManager != null)
            {
                levelManager.NotifyBlockSettled();
            }

            yield break;
        }

        if (board.HasNestMatch(block, to)
            || TryGetAdjacentMatchingTarget(board, block.GridPosition, out Vector2Int nestCell) && nestCell == to)
        {
            yield return EnterMatchingTarget(board, rect, block.GridPosition, to);
            yield break;
        }

        if (!resolvingAligned && levelManager != null)
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
            float eased = Mathf.SmoothStep(0f, 1f, t);
            traveler.anchoredPosition = QuadraticBezier(start, control, end, eased);
            traveler.localScale = Vector3.LerpUnclamped(traveler.localScale, hopScale, eased);
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

        if (traveler != null)
        {
            Destroy(traveler.gameObject);
        }
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
        bool fullyConsumed = ConsumeAndRebuild(
            board,
            block,
            to,
            out Target completedTarget,
            out ShapeType consumedShape,
            out Vector2Int effectCell,
            out bool consumedInnerLayer);
        logicalCell = block.GridPosition;

        yield return PlayMatchEffect(
            board,
            effectCell,
            consumedShape,
            completedTarget,
            fullyConsumed ? block : null);

        if (!fullyConsumed
            && consumedInnerLayer
            && block != null
            && !block.IsSettled
            && block.CellCount == 1
            && board.HasNestMatch(block, block.GridPosition))
        {
            yield return PlayNestedOuterNestEntry(board, block);
        }

        if (!resolvingAligned)
        {
            yield return ResolveAlreadyAlignedMatches(board);
            if (levelManager != null)
            {
                levelManager.NotifyBlockSettled();
            }
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

        board.CollectNestMatches(subject, here, nestCellIndices, nestTargets);
        KeepOnlyFirstMatch();
        if (nestCellIndices.Count == 0)
        {
            yield break;
        }

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
            fullyConsumed ? subject : null);
    }

    private IEnumerator ResolveAlreadyAlignedMatches(BoardManager board)
    {
        if (resolvingAligned)
        {
            yield break;
        }

        resolvingAligned = true;
        const int maxPasses = 32;
        try
        {
            for (int pass = 0; pass < maxPasses; pass++)
            {
                Block subject = FindAlreadyAlignedBlock(board);
                if (subject == null)
                {
                    yield break;
                }

                if (subject.CellCount == 1 && subject.HasActiveInnerLayer())
                {
                    BlockMover mover = subject.GetComponent<BlockMover>();
                    if (mover != null)
                    {
                        yield return mover.PlayAlignedNestedMatch(board);
                    }

                    continue;
                }

                if (subject.CellCount > 1)
                {
                    yield return MatchFocusedChainCell(
                        board,
                        subject,
                        subject.GridPosition,
                        subject.GridPosition);
                    continue;
                }

                yield return PlaySimpleAlignedNestEntry(board, subject);
            }
        }
        finally
        {
            resolvingAligned = false;
        }
    }

    public IEnumerator PlayAlignedNestedMatch(BoardManager board)
    {
        if (block == null)
        {
            yield break;
        }

        bool wasResolving = resolvingAligned;
        resolvingAligned = true;
        Vector2Int here = block.GridPosition;
        yield return EnterNestedInnerThenOuter(board, block.RectTransform, here, here);
        resolvingAligned = wasResolving;
    }

    private IEnumerator PlaySimpleAlignedNestEntry(BoardManager board, Block subject)
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

        yield return AnimateSubjectAnticipation(board, subject, restPosition, restScale);
        yield return AnimateSubjectNestEntry(board, subject, here, here, restScale);
        subject.SetGridPosition(here);

        board.CollectNestMatches(subject, here, nestCellIndices, nestTargets);
        KeepOnlyFirstMatch();
        if (nestCellIndices.Count == 0)
        {
            yield break;
        }

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
            fullyConsumed ? subject : null);
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
            float eased = Mathf.SmoothStep(0f, 1f, t);
            rect.anchoredPosition = QuadraticBezier(start, control, end, eased);
            subject.transform.localScale = Vector3.LerpUnclamped(subject.transform.localScale, hopScale, eased);
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

        KeepOnlyFirstMatch();
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

    private Block FindAlreadyAlignedBlock(BoardManager board)
    {
        board.CollectUniqueBlocks(alignedScanBlocks);
        for (int i = 0; i < alignedScanBlocks.Count; i++)
        {
            Block candidate = alignedScanBlocks[i];
            if (candidate == null || candidate.IsSettled || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            if (board.HasNestMatch(candidate, candidate.GridPosition))
            {
                return candidate;
            }
        }

        return null;
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
            subject.RefreshActiveLayers();
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
        ShapeLayout.SplitConnected(splitWorlds, splitCells, splitAnchors, splitComponents);

        if (splitComponents.Count == 0)
        {
            subject.BeginMatchPresentation();
            subject.Settle();
            return true;
        }

        subject.RebuildFromRemaining(splitComponents[0], splitAnchors[0]);
        for (int i = 1; i < splitComponents.Count; i++)
        {
            if (levelManager != null)
            {
                levelManager.SpawnSplitBlock(subject, splitComponents[i], splitAnchors[i]);
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

        yield return AnimateHopTravel(rect, startPosition, endPosition, duration, scaleAtStart);
        rect.anchoredPosition = endPosition;
        block.transform.localScale = scaleAtStart;
    }

    private IEnumerator AnimateHopTravel(
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
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, EaseOutQuad(t));
            if (squashHop)
            {
                float squashT = t < 0.72f ? t / 0.72f : (1f - t) / 0.28f;
                squashT = 1f - ((1f - Mathf.Clamp01(squashT)) * (1f - Mathf.Clamp01(squashT)));
                block.transform.localScale = Vector3.LerpUnclamped(scaleAtStart, squash, squashT);
            }

            yield return null;
        }

        rect.anchoredPosition = to;
        if (squashHop)
        {
            block.transform.localScale = scaleAtStart;
        }
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
            float eased = easeOut ? EaseOutQuad(t) : EaseInQuad(t);
            rect.anchoredPosition = Vector2.LerpUnclamped(from, to, eased);
            yield return null;
        }

        rect.anchoredPosition = to;
    }

    private static float EaseOutQuad(float t)
    {
        return 1f - ((1f - t) * (1f - t));
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
            float eased = Mathf.SmoothStep(0f, 1f, t);
            rect.anchoredPosition = QuadraticBezier(start, control, end, eased);
            block.transform.localScale = Vector3.LerpUnclamped(block.transform.localScale, hopScale, eased);
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
        Block dissolvingBlock)
    {
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
            yield return effect.Play(glowShape, dissolvingBlock, nestTarget);
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
