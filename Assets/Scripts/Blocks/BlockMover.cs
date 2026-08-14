using System.Collections;
using UnityEngine;

/// <summary>
/// Moves a block cell-by-cell along one locked cardinal direction.
/// Occupancy updates per committed cell; matching-target nest entry is visual.
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

    private bool dragActive;
    private bool dragReleased;
    private Vector2Int dragOrigin;
    private Vector2Int dragDirection;
    private Vector2Int desiredCell;
    private bool dragWantsForward;
    private Coroutine dragRoutine;
    private MatchEffect activeMatchEffect;

    private static readonly Vector2Int[] OrthogonalNeighbors =
    {
        Vector2Int.right,
        Vector2Int.left,
        Vector2Int.up,
        Vector2Int.down
    };

    public bool IsMoving => isMoving;
    public bool IsDragging => dragActive;

    public void SetLevelManager(LevelManager manager)
    {
        levelManager = manager;
    }

    public void SetAudioFeedback(AudioFeedback feedback)
    {
        audioFeedback = feedback;
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
        dragDirection = direction;
        desiredCell = dragOrigin;
        dragWantsForward = false;
        isMoving = true;
        dragRoutine = StartCoroutine(DragRoutine(board));
        LogDrag($"BeginDrag {block.name} origin={dragOrigin} dir={direction}");
        PlayDragStartSound();
        return true;
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

        int requestedSteps = AxisSteps(clamped - dragOrigin, dragDirection);
        int desiredSteps = AxisSteps(desiredCell - dragOrigin, dragDirection);
        if (requestedSteps > desiredSteps)
        {
            desiredCell = clamped;
            if (debugDrag)
            {
                LogDrag($"Request {requestedCell} -> clamped {clamped}");
            }
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
                Vector2Int committed = block.GridPosition;

                if (committed == desiredCell)
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

                PlayHopSound();
                yield return AnimateHop(board, rect, committed, next, duration, committed == dragOrigin);
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
    }

    private void PlayHopSound()
    {
        if (audioFeedback != null)
        {
            audioFeedback.PlayHop();
        }
    }

    private void PlayNestEntrySound()
    {
        if (audioFeedback != null)
        {
            audioFeedback.PlayNestEntry();
        }
    }

    private void PlayMatchSound()
    {
        if (audioFeedback != null)
        {
            audioFeedback.PlayMatch();
        }
    }

    private bool CanHopInto(BoardManager board, Vector2Int next)
    {
        if (!board.IsInsideBoard(next))
        {
            return false;
        }

        Block occupant = board.GetBlockAt(next);
        if (occupant != null && occupant != block)
        {
            return false;
        }

        Target target = board.GetTargetAt(next);
        if (target != null && target.ShapeType != block.ShapeType)
        {
            return false;
        }

        return !IsMatchingTargetCell(board, next);
    }

    private bool TryGetAdjacentMatchingTarget(
        BoardManager board,
        Vector2Int blockPosition,
        out Vector2Int nestCell)
    {
        if (IsMatchingTargetCell(board, blockPosition + dragDirection))
        {
            nestCell = blockPosition + dragDirection;
            return true;
        }

        for (int i = 0; i < OrthogonalNeighbors.Length; i++)
        {
            Vector2Int offset = OrthogonalNeighbors[i];
            if (offset == dragDirection)
            {
                continue;
            }

            Vector2Int candidate = blockPosition + offset;
            if (IsMatchingTargetCell(board, candidate))
            {
                nestCell = candidate;
                return true;
            }
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
        dragReleased = true;
        LogDrag($"Matching magnet {from} -> {to}");

        Target nestTarget = board.GetTargetAt(to);
        if (nestTarget != null)
        {
            nestTarget.ShowReadyFeedback();
        }

        if (!board.TryMoveBlock(block, from, to))
        {
            if (nestTarget != null)
            {
                nestTarget.HideReadyFeedback();
            }

            yield break;
        }

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
        yield return AnimateNestEntry(board, rect, from, to, restScale);

        block.SetGridPosition(to);

        if (board.IsMatchingTarget(block))
        {
            block.BeginMatchPresentation();
            if (nestTarget != null)
            {
                nestTarget.BeginMatchPresentation();
            }

            block.Settle();
            if (levelManager != null)
            {
                levelManager.NotifyBlockSettled();
            }

            yield return PlayMatchEffect(board, to, nestTarget);
            board.ReleaseMatchedCell(block, nestTarget);
        }
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
            if (!board.IsInsideBoard(next))
            {
                return current;
            }

            Block occupant = board.GetBlockAt(next);
            if (occupant != null && occupant != block)
            {
                return current;
            }

            Target target = board.GetTargetAt(next);
            if (target != null)
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
        Target target = board.GetTargetAt(cell);
        return target != null && target.ShapeType == block.ShapeType;
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

        if (anticipate && normalHopAnticipateDuration > 0f && normalHopAnticipatePercent > 0f)
        {
            Vector2 cellSize = board.VisualCellSize;
            float axisSize = dragDirection.x != 0 ? cellSize.x : cellSize.y;
            Vector2 windup = startPosition - ((Vector2)dragDirection * (axisSize * normalHopAnticipatePercent));
            yield return AnimateAnchoredPosition(rect, startPosition, windup, normalHopAnticipateDuration, easeOut: false);
            startPosition = windup;
        }

        yield return AnimateAnchoredPosition(rect, startPosition, endPosition, duration, easeOut: true);
        rect.anchoredPosition = endPosition;
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

    private IEnumerator PlayMatchEffect(BoardManager board, Vector2Int nestCell, Target nestTarget)
    {
        PlayMatchSound();

        if (matchEffectPrefab == null)
        {
            yield return PulseTarget(board, nestCell);
            if (block != null)
            {
                block.CompleteMatchPresentation();
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
        effectRect.SetAsLastSibling();

        try
        {
            yield return effect.Play(block.ShapeType, block, nestTarget);
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
