using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Data model for a Canvas-based puzzle block.
/// Owns shape, allowed move direction, and grid cell. Placement uses BoardManager UI coordinates.
/// Visual states are presentation-only.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class Block : MonoBehaviour
{
    private enum VisualState
    {
        Normal,
        Moving,
        Settled,
        Matching,
        Matched
    }

    [SerializeField]
    private ShapeType shapeType = ShapeType.Square;

    [SerializeField]
    private MoveDirection moveDirection = MoveDirection.Any;

    [SerializeField]
    private Vector2Int gridPosition = Vector2Int.zero;

    [SerializeField]
    private Sprite squareSprite;

    [SerializeField]
    private Sprite circleSprite;

    [SerializeField]
    private Sprite triangleSprite;

    [SerializeField]
    [Range(0.1f, 1f)]
    [Tooltip("Image alpha when the block is settled. Does not affect occupancy.")]
    private float settledAlpha = 0.55f;

    [SerializeField]
    [Range(1f, 1.15f)]
    [Tooltip("Scale multiplier while the block is pressed/dragged.")]
    private float dragSelectScale = 1.06f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Time to ease into and out of drag selection scale.")]
    private float dragSelectDuration = 0.1f;

    private BoardManager boardManager;
    private bool isSettled;
    private Color restColor = Color.white;
    private bool hasCachedRestColor;
    private Vector3 restScale = Vector3.one;
    private bool hasRestScale;
    private Image image;
    private RectTransform cachedRect;
    private VisualState visualState = VisualState.Normal;
    private bool dragSelected;
    private Coroutine selectionRoutine;

    public ShapeType ShapeType
    {
        get => shapeType;
        set
        {
            if (shapeType == value)
            {
                return;
            }

            shapeType = value;
            RefreshVisual();
        }
    }

    public MoveDirection MoveDirection
    {
        get => moveDirection;
        set => moveDirection = value;
    }

    public Vector2Int GridPosition
    {
        get => gridPosition;
        set => SetGridPosition(value);
    }

    public bool IsSettled => isSettled;

    public Vector3 RestScale
    {
        get
        {
            CaptureRestScale();
            return restScale;
        }
    }

    public RectTransform RectTransform
    {
        get
        {
            if (cachedRect == null)
            {
                cachedRect = (RectTransform)transform;
            }

            return cachedRect;
        }
    }

    public BoardManager Board => boardManager;

    private bool IsMatchVisual =>
        visualState == VisualState.Matching || visualState == VisualState.Matched;

    private void Awake()
    {
        cachedRect = (RectTransform)transform;
        CacheImage();
        CaptureRestScale();
        CaptureRestColor();
        RefreshVisual();
    }

    private void OnEnable()
    {
        RefreshVisual();
    }

    public void Initialize(BoardManager board, Vector2Int startPosition)
    {
        ResetMatchPresentation();
        boardManager = board;
        SetGridPosition(startPosition);

        if (boardManager != null)
        {
            boardManager.TryRegisterBlock(this, gridPosition);
        }
    }

    public void SetGridPosition(Vector2Int position)
    {
        gridPosition = position;

        if (boardManager == null)
        {
            return;
        }

        Vector3 localPosition = boardManager.GridToLocal(gridPosition);
        RectTransform.anchoredPosition = localPosition;
    }

    public void Settle()
    {
        if (isSettled)
        {
            return;
        }

        isSettled = true;
        if (!IsMatchVisual)
        {
            visualState = VisualState.Settled;
        }

        UpdateSettledVisual();
    }

    public void ResetSettledState()
    {
        isSettled = false;
        if (!IsMatchVisual)
        {
            visualState = VisualState.Normal;
        }

        UpdateSettledVisual();
    }

    public void ShowDragSelection()
    {
        if (isSettled || IsMatchVisual || !isActiveAndEnabled)
        {
            return;
        }

        CaptureRestScale();
        visualState = VisualState.Moving;
        dragSelected = true;
        AnimateSelectionScale(restScale * dragSelectScale);
    }

    public void HideDragSelection()
    {
        if (IsMatchVisual)
        {
            return;
        }

        if (!dragSelected && selectionRoutine == null)
        {
            return;
        }

        dragSelected = false;
        visualState = isSettled ? VisualState.Settled : VisualState.Normal;
        CaptureRestScale();
        if (!isActiveAndEnabled)
        {
            StopSelectionRoutine();
            RectTransform.localScale = restScale;
            return;
        }

        AnimateSelectionScale(restScale);
    }

    public void CancelDragSelectionImmediate()
    {
        dragSelected = false;
        StopSelectionRoutine();
        if (!IsMatchVisual)
        {
            CaptureRestScale();
            RectTransform.localScale = restScale;
            visualState = isSettled ? VisualState.Settled : VisualState.Normal;
        }
    }

    public void BeginMatchPresentation()
    {
        CancelDragSelectionImmediate();
        visualState = VisualState.Matching;
        CacheImage();
        if (image != null)
        {
            image.raycastTarget = false;
        }
    }

    public void SetMatchPresentation(float scale, float alpha)
    {
        visualState = VisualState.Matching;
        CaptureRestScale();
        RectTransform.localScale = restScale * scale;
        CacheImage();
        if (image == null)
        {
            return;
        }

        CaptureRestColor();
        Color color = restColor;
        color.a = restColor.a * Mathf.Clamp01(alpha);
        image.color = color;
        image.enabled = alpha > 0.001f;
    }

    public void CompleteMatchPresentation()
    {
        visualState = VisualState.Matched;
        RectTransform.localScale = Vector3.zero;
        CacheImage();
        if (image != null)
        {
            CaptureRestColor();
            Color color = restColor;
            color.a = 0f;
            image.color = color;
            image.enabled = false;
            image.raycastTarget = false;
        }
    }

    public void ResetMatchPresentation()
    {
        visualState = isSettled ? VisualState.Settled : VisualState.Normal;
        CancelDragSelectionImmediate();
        CaptureRestScale();
        RectTransform.localScale = restScale;
        CacheImage();
        if (image != null)
        {
            image.enabled = true;
            image.raycastTarget = true;
        }

        UpdateSettledVisual();
    }

    public void UpdateSettledVisual()
    {
        if (IsMatchVisual)
        {
            return;
        }

        CacheImage();
        if (image == null)
        {
            return;
        }

        CaptureRestColor();
        Color color = restColor;
        color.a = isSettled ? restColor.a * settledAlpha : restColor.a;
        image.color = color;
    }

    public void RefreshVisual()
    {
        CacheImage();
        if (image == null)
        {
            return;
        }

        Sprite sprite = ShapeVisuals.SpriteFor(shapeType, squareSprite, circleSprite, triangleSprite);
        if (image.sprite != sprite)
        {
            image.sprite = sprite;
        }

        image.preserveAspect = true;
        if (!IsMatchVisual)
        {
            image.raycastTarget = true;
        }
    }

    private void CaptureRestScale()
    {
        if (hasRestScale)
        {
            return;
        }

        restScale = RectTransform.localScale;
        if (restScale.sqrMagnitude < 0.0001f)
        {
            restScale = Vector3.one;
        }

        hasRestScale = true;
    }

    private void CaptureRestColor()
    {
        if (hasCachedRestColor)
        {
            return;
        }

        CacheImage();
        restColor = image != null ? image.color : Color.white;
        hasCachedRestColor = true;
    }

    private void AnimateSelectionScale(Vector3 targetScale)
    {
        StopSelectionRoutine();
        if (dragSelectDuration <= 0f)
        {
            RectTransform.localScale = targetScale;
            return;
        }

        selectionRoutine = StartCoroutine(SelectionScaleRoutine(RectTransform.localScale, targetScale));
    }

    private IEnumerator SelectionScaleRoutine(Vector3 from, Vector3 to)
    {
        float elapsed = 0f;
        while (elapsed < dragSelectDuration)
        {
            if (IsMatchVisual)
            {
                selectionRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dragSelectDuration);
            float eased = 1f - ((1f - t) * (1f - t));
            RectTransform.localScale = Vector3.LerpUnclamped(from, to, eased);
            yield return null;
        }

        if (!IsMatchVisual)
        {
            RectTransform.localScale = to;
        }

        selectionRoutine = null;
    }

    private void StopSelectionRoutine()
    {
        if (selectionRoutine != null)
        {
            StopCoroutine(selectionRoutine);
            selectionRoutine = null;
        }
    }

    private void OnDisable()
    {
        CancelDragSelectionImmediate();
    }

    private void CacheImage()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshVisual();
    }
#endif

    private void OnDestroy()
    {
        if (boardManager != null)
        {
            boardManager.UnregisterBlock(this);
        }
    }
}
