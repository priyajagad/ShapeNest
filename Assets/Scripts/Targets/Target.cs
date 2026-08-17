using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI nest slot on the Board. Uses BoardManager grid coordinates. Does not occupy block cells.
/// ShapeType is gameplay identity; sprites are presentation-only and can be replaced.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class Target : MonoBehaviour
{
    private enum VisualState
    {
        Normal,
        Entering,
        Matched
    }

    [SerializeField]
    private ShapeType shapeType = ShapeType.Square;

    [SerializeField]
    private Vector2Int gridPosition = Vector2Int.zero;

    [SerializeField]
    [Tooltip("Local cells relative to Grid Position. Empty means a single cell at (0,0).")]
    private List<ShapeCellData> cells = new List<ShapeCellData>();

    [SerializeField]
    private PieceComposition composition = PieceComposition.Simple;

    [SerializeField]
    private ShapeType outerShape = ShapeType.Square;

    [SerializeField]
    private Sprite squareSprite;

    [SerializeField]
    private Sprite circleSprite;

    [SerializeField]
    private Sprite triangleSprite;

    [SerializeField]
    [Tooltip("Optional. Theme shape sprites override prefab sprites when assigned.")]
    private ShapeNestTheme theme;

    [SerializeField]
    [Range(1f, 1.12f)]
    [Tooltip("Peak scale while a matching block is adjacent.")]
    private float readyScale = 1.04f;

    [SerializeField]
    [Range(1f, 1.12f)]
    [Tooltip("Trough scale of the gentle ready pulse.")]
    private float pulseScale = 1.02f;

    [SerializeField]
    [Range(1f, 1.2f)]
    [Tooltip("RGB multiplier while ready. 1 means no brightness change.")]
    private float readyBrightness = 1.08f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Time to ease from rest into the ready pose.")]
    private float readyRiseDuration = 0.12f;

    [SerializeField]
    [Min(0.05f)]
    [Tooltip("One full ready pulse cycle (peak to trough to peak).")]
    private float readyPulseDuration = 0.4f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("Time to ease back to rest when ready feedback ends.")]
    private float readyRestoreDuration = 0.08f;

    private BoardManager boardManager;
    private bool isRegistered;
    private Image image;
    private RectTransform cachedRect;

    private Vector3 restScale = Vector3.one;
    private Color restColor = Color.white;
    private Color readyColor = Color.white;
    private bool hasRestPose;
    private bool isReadyFeedbackActive;
    private VisualState visualState = VisualState.Normal;
    private Coroutine readyRoutine;
    private readonly List<Image> extraCellImages = new List<Image>();
    private Vector2Int[] cachedLocals = { Vector2Int.zero };
    private ShapeType[] cachedShapes = { ShapeType.Square };
    private ShapeType[] cachedOuters = { ShapeType.Square };
    private int cachedCellCount = 1;

    public ShapeType RequiredShape
    {
        get
        {
            ShapeCellData cell = cells != null && cells.Count > 0 ? cells[0] : null;
            return ShapeLayout.ActiveShape(cell, shapeType);
        }
    }

    public bool HasRemainingLayers
    {
        get
        {
            ShapeCellData cell = cells != null && cells.Count > 0 ? cells[0] : null;
            if (cell == null)
            {
                return visualState != VisualState.Matched;
            }

            return true;
        }
    }

    public bool TryConsumeLayer(ShapeType offered, out bool fullyComplete)
    {
        fullyComplete = false;
        ShapeCellData cell = cells != null && cells.Count > 0 ? cells[0] : null;
        if (cell == null)
        {
            if (offered != shapeType)
            {
                return false;
            }

            fullyComplete = true;
            return true;
        }

        if (ShapeLayout.ActiveShape(cell, shapeType) != offered)
        {
            return false;
        }

        bool hadInner = cell.innerShapes != null && cell.innerShapes.Count > 0;
        ShapeLayout.TryConsumeLayer(cell, offered);
        if (hadInner)
        {
            shapeType = ShapeLayout.ActiveShape(cell, cell.shapeType);
            RebuildCache();
            RefreshVisual();
            fullyComplete = false;
            return true;
        }

        fullyComplete = true;
        return true;
    }

    public ShapeType ShapeType => RequiredShape;
    public Vector2Int GridPosition => gridPosition;
    public int CellCount => cachedCellCount;
    public PieceComposition Composition => composition;
    public ShapeType OuterShape => outerShape;
    public IReadOnlyList<ShapeCellData> Cells => cells;

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

    private void Awake()
    {
        cachedRect = (RectTransform)transform;
        CacheImage();
        CaptureRestPose();
        RebuildCache();
        RefreshVisual();
    }

    private void OnEnable()
    {
        RefreshVisual();
    }

    private void OnDisable()
    {
        StopReadyRoutine();
        isReadyFeedbackActive = false;
        visualState = VisualState.Normal;
        ApplyRestVisuals();
        if (image != null)
        {
            image.enabled = true;
        }
    }

    public void SetShapeType(ShapeType type)
    {
        shapeType = type;
        SetAnchorShape(type);
        RefreshVisual();
    }

    public void ApplyLayout(ShapeType fallback, IReadOnlyList<ShapeCellData> source)
    {
        ApplyLayout(fallback, source, PieceComposition.Simple, fallback);
    }

    public void ApplyLayout(
        ShapeType fallback,
        IReadOnlyList<ShapeCellData> source,
        PieceComposition pieceComposition,
        ShapeType pieceOuterShape)
    {
        if (cells == null)
        {
            cells = new List<ShapeCellData>();
        }

        ShapeLayout.CopyInto(source, fallback, cells);
        ShapeLayout.ApplyLegacyShapeInShape(cells, pieceComposition, pieceOuterShape);
        shapeType = ShapeLayout.ActiveShape(
            cells.Count > 0 ? cells[0] : null,
            ShapeLayout.AnchorShape(cells, fallback));
        composition = pieceComposition;
        outerShape = pieceOuterShape;
        RebuildCache();
        SyncVisualSizeToBoard();
        RefreshVisual();
        RebuildCellVisuals();
    }

    public Vector2Int GetLocalCell(int index)
    {
        if (index < 0 || index >= cachedCellCount)
        {
            return Vector2Int.zero;
        }

        return cachedLocals[index];
    }

    public ShapeType GetShapeAtIndex(int index)
    {
        if (index < 0 || index >= cachedCellCount)
        {
            return shapeType;
        }

        return cachedShapes[index];
    }

    public void Initialize(BoardManager board, Vector2Int startPosition)
    {
        StopReadyRoutine();
        ResetMatchPresentation();
        isReadyFeedbackActive = false;

        if (isRegistered && boardManager != null)
        {
            boardManager.UnregisterTarget(this);
            isRegistered = false;
        }

        boardManager = board;
        gridPosition = startPosition;
        RebuildCache();
        SyncVisualSizeToBoard();
        RefreshVisual();
        CaptureRestPose();
        RebuildCellVisuals();

        if (boardManager == null)
        {
            return;
        }

        RectTransform.anchoredPosition = boardManager.GridToLocal(gridPosition);
        isRegistered = boardManager.TryRegisterTarget(this);
    }

    public void RefreshLayoutVisuals()
    {
        SyncVisualSizeToBoard();
        if (boardManager != null)
        {
            RectTransform.anchoredPosition = boardManager.GridToLocal(gridPosition);
        }

        RefreshVisual();
        RebuildCellVisuals();
    }

    public void ShowReadyFeedback()
    {
        if (!isActiveAndEnabled || visualState == VisualState.Matched)
        {
            return;
        }

        CacheImage();
        CaptureRestPose();

        if (isReadyFeedbackActive)
        {
            return;
        }

        StopReadyRoutine();
        visualState = VisualState.Entering;
        isReadyFeedbackActive = true;
        readyRoutine = StartCoroutine(ReadyPulseRoutine());
    }

    public void HideReadyFeedback()
    {
        if (visualState == VisualState.Matched)
        {
            StopReadyRoutine();
            isReadyFeedbackActive = false;
            return;
        }
        CacheImage();
        CaptureRestPose();

        if (readyRoutine != null)
        {
            StopCoroutine(readyRoutine);
            readyRoutine = null;
        }

        if (!isReadyFeedbackActive)
        {
            ApplyRestVisuals();
            return;
        }

        isReadyFeedbackActive = false;

        if (!isActiveAndEnabled || readyRestoreDuration <= 0f)
        {
            ApplyRestVisuals();
            return;
        }

        readyRoutine = StartCoroutine(RestoreRestRoutine());
    }

    public void BeginMatchPresentation()
    {
        StopReadyRoutine();
        isReadyFeedbackActive = false;
        visualState = VisualState.Entering;
        CacheImage();
        ApplyRestVisuals();
    }

    public void SetMatchPresentation(float scale, float alpha)
    {
        visualState = VisualState.Entering;
        if (cachedRect == null)
        {
            cachedRect = (RectTransform)transform;
        }

        cachedRect.localScale = restScale * scale;
        CacheImage();
        ApplyTargetVisualAlpha(Mathf.Clamp01(alpha), alpha > 0.001f);
    }

    public void CompleteMatchPresentation()
    {
        visualState = VisualState.Matched;
        StopReadyRoutine();
        isReadyFeedbackActive = false;
        if (cachedRect == null)
        {
            cachedRect = (RectTransform)transform;
        }

        cachedRect.localScale = Vector3.zero;
        CacheImage();
        if (image != null)
        {
            Color color = restColor;
            color.a = 0f;
            image.color = color;
            image.enabled = false;
        }

        ApplyTargetVisualAlpha(0f, false);
    }

    public void ResetMatchPresentation()
    {
        visualState = VisualState.Normal;
        StopReadyRoutine();
        isReadyFeedbackActive = false;
        if (restScale.sqrMagnitude < 0.0001f)
        {
            restScale = Vector3.one;
            hasRestPose = false;
        }

        CacheImage();
        if (image != null)
        {
            image.enabled = true;
            if (restColor.a < 0.01f)
            {
                restColor = Color.white;
                hasRestPose = false;
            }
        }

        ApplyRestVisuals();
        CaptureRestPose();
        RebuildCellVisuals();
        RefreshVisual();
    }

    public void RefreshVisual()
    {
        RefreshSerializedVisualState();
        RefreshRuntimeVisuals();
    }

    private void RefreshSerializedVisualState()
    {
        CacheImage();
        if (image == null)
        {
            return;
        }

        Sprite sprite = SpriteFor(AnchorOuterShape());
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        if (!isReadyFeedbackActive && visualState == VisualState.Normal)
        {
            image.color = hasRestPose ? restColor : Color.white;
        }

        ApplyExtraCellSprites();

        PiecePresentation presentation = GetComponent<PiecePresentation>();
        if (presentation != null)
        {
            presentation.Apply();
        }
    }

    private void RefreshRuntimeVisuals()
    {
        if (!PieceGameplayVisuals.CanMutateHierarchy(transform))
        {
            return;
        }

        ApplyNestedOverlays();
    }

    private IEnumerator ReadyPulseRoutine()
    {
        Vector3 peak = restScale * readyScale;
        Vector3 trough = restScale * pulseScale;
        float halfPulse = Mathf.Max(0.01f, readyPulseDuration * 0.5f);

        yield return AnimateReadyVisual(
            cachedRect.localScale,
            peak,
            image != null ? image.color : restColor,
            readyColor,
            readyRiseDuration,
            true);

        while (isReadyFeedbackActive)
        {
            yield return AnimateReadyVisual(peak, trough, readyColor, readyColor, halfPulse, false);
            if (!isReadyFeedbackActive)
            {
                yield break;
            }

            yield return AnimateReadyVisual(trough, peak, readyColor, readyColor, halfPulse, true);
        }
    }

    private IEnumerator RestoreRestRoutine()
    {
        Vector3 fromScale = cachedRect.localScale;
        Color fromColor = image != null ? image.color : restColor;
        yield return AnimateReadyVisual(fromScale, restScale, fromColor, restColor, readyRestoreDuration, true);
        ApplyRestVisuals();
        readyRoutine = null;
    }

    private IEnumerator AnimateReadyVisual(
        Vector3 fromScale,
        Vector3 toScale,
        Color fromColor,
        Color toColor,
        float duration,
        bool easeOut)
    {
        if (duration <= 0f)
        {
            ApplyVisual(toScale, toColor);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = easeOut ? 1f - ((1f - t) * (1f - t)) : t * t;
            ApplyVisual(
                Vector3.LerpUnclamped(fromScale, toScale, eased),
                Color.LerpUnclamped(fromColor, toColor, eased));
            yield return null;
        }

        ApplyVisual(toScale, toColor);
    }

    private void ApplyVisual(Vector3 scale, Color color)
    {
        cachedRect.localScale = scale;
        if (image != null)
        {
            image.color = color;
        }

        ApplyColorToExtraCells(color);
    }

    private void ApplyRestVisuals()
    {
        if (cachedRect == null)
        {
            cachedRect = (RectTransform)transform;
        }

        cachedRect.localScale = restScale;
        if (image != null)
        {
            image.color = restColor;
        }

        ApplyColorToExtraCells(restColor);
    }

    private void CaptureRestPose()
    {
        if (hasRestPose)
        {
            return;
        }

        if (cachedRect == null)
        {
            cachedRect = (RectTransform)transform;
        }

        restScale = cachedRect.localScale;
        if (restScale.sqrMagnitude < 0.0001f)
        {
            restScale = Vector3.one;
        }

        restColor = image != null ? image.color : Color.white;
        readyColor = new Color(
            restColor.r * readyBrightness,
            restColor.g * readyBrightness,
            restColor.b * readyBrightness,
            restColor.a);
        hasRestPose = true;
    }

    private void SetAnchorShape(ShapeType type)
    {
        if (cells == null)
        {
            cells = new List<ShapeCellData>();
        }

        if (cells.Count == 0)
        {
            cells.Add(new ShapeCellData
            {
                localPosition = Vector2Int.zero,
                shapeType = type
            });
            RebuildCache();
            return;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null && cells[i].localPosition == Vector2Int.zero)
            {
                cells[i].shapeType = type;
                RebuildCache();
                return;
            }
        }

        cells[0].shapeType = type;
        RebuildCache();
    }

    private void RebuildCache()
    {
        if (cells == null)
        {
            cells = new List<ShapeCellData>();
        }

        int count = ShapeLayout.EffectiveCount(cells);
        if (cachedLocals == null || cachedLocals.Length < count)
        {
            cachedLocals = new Vector2Int[count];
            cachedShapes = new ShapeType[count];
            cachedOuters = new ShapeType[count];
        }

        cachedCellCount = count;
        for (int i = 0; i < count; i++)
        {
            cachedLocals[i] = ShapeLayout.EffectiveLocal(cells, i);
            ShapeCellData cell = cells != null && i < cells.Count ? cells[i] : null;
            cachedShapes[i] = ShapeLayout.ActiveShape(cell, shapeType);
            cachedOuters[i] = ShapeLayout.VisualOuter(cell, shapeType);
        }
    }

    private ShapeType AnchorOuterShape()
    {
        for (int i = 0; i < cachedCellCount; i++)
        {
            if (cachedLocals[i] == Vector2Int.zero)
            {
                return cachedOuters[i];
            }
        }

        return cachedCellCount > 0 ? cachedOuters[0] : shapeType;
    }

    private bool HasInnerLayer(int index)
    {
        ShapeCellData cell = cells != null && index >= 0 && index < cells.Count ? cells[index] : null;
        return cell != null && cell.innerShapes != null && cell.innerShapes.Count > 0;
    }

    private void SyncVisualSizeToBoard()
    {
        if (boardManager == null)
        {
            return;
        }

        Vector2 size = PieceGameplayVisuals.PieceSizeForCell(boardManager.VisualCellSize);
        RectTransform.sizeDelta = size;
        PiecePresentation presentation = GetComponent<PiecePresentation>();
        if (presentation != null)
        {
            presentation.SetVisualSize(size);
        }
    }

    private void RebuildCellVisuals()
    {
        CacheImage();
        SyncVisualSizeToBoard();
        EnsureExtraCellCount();
        ApplyExtraCellSprites();
        LayoutExtraCells();
        if (!PieceGameplayVisuals.CanMutateHierarchy(transform))
        {
            return;
        }

        ApplyNestedOverlays();
    }

    private void EnsureExtraCellCount()
    {
        int extraCount = 0;
        for (int i = 0; i < cachedCellCount; i++)
        {
            if (cachedLocals[i] != Vector2Int.zero)
            {
                extraCount++;
            }
        }

        while (extraCellImages.Count < extraCount)
        {
            extraCellImages.Add(CreateExtraCellImage());
        }

        for (int i = extraCellImages.Count - 1; i >= extraCount; i--)
        {
            Image extra = extraCellImages[i];
            extraCellImages.RemoveAt(i);
            if (extra != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(extra.gameObject);
                }
                else
                {
                    DestroyImmediate(extra.gameObject);
                }
            }
        }
    }

    private Image CreateExtraCellImage()
    {
        var cellObject = new GameObject("ShapeCell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = cellObject.GetComponent<RectTransform>();
        rect.SetParent(RectTransform, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = RectTransform.sizeDelta;
        cellObject.layer = gameObject.layer;

        var extraImage = cellObject.GetComponent<Image>();
        extraImage.preserveAspect = true;
        extraImage.raycastTarget = false;
        extraImage.color = image != null ? image.color : Color.white;
        return extraImage;
    }

    private void LayoutExtraCells()
    {
        if (boardManager == null)
        {
            return;
        }

        Vector2 cellSize = boardManager.VisualCellSize;
        Vector2 visualSize = RectTransform.sizeDelta;
        int extraIndex = 0;
        bool showAnchor = false;
        for (int i = 0; i < cachedCellCount; i++)
        {
            Vector2Int local = cachedLocals[i];
            if (local == Vector2Int.zero)
            {
                showAnchor = true;
                continue;
            }

            if (extraIndex >= extraCellImages.Count)
            {
                break;
            }

            Image extraImage = extraCellImages[extraIndex];
            extraIndex++;
            if (extraImage == null)
            {
                continue;
            }

            RectTransform extraRect = extraImage.rectTransform;
            extraRect.sizeDelta = visualSize;
            extraRect.anchoredPosition = new Vector2(local.x * cellSize.x, local.y * cellSize.y);
            extraImage.raycastTarget = false;
        }

        if (image != null && visualState != VisualState.Matched)
        {
            image.enabled = showAnchor || cachedCellCount <= 1;
        }
    }

    private void ApplyExtraCellSprites()
    {
        int extraIndex = 0;
        for (int i = 0; i < cachedCellCount; i++)
        {
            if (cachedLocals[i] == Vector2Int.zero)
            {
                continue;
            }

            if (extraIndex >= extraCellImages.Count)
            {
                break;
            }

            Image extraImage = extraCellImages[extraIndex];
            extraIndex++;
            if (extraImage == null)
            {
                continue;
            }

            extraImage.sprite = SpriteFor(cachedOuters[i]);
            extraImage.preserveAspect = true;
            extraImage.raycastTarget = false;
        }
    }

    private void ApplyNestedOverlays()
    {
        CacheImage();
        Color innerColor = theme != null ? new Color(1f, 1f, 1f, 0.95f) : Color.white;
        if (image != null)
        {
            int anchor = 0;
            for (int i = 0; i < cachedCellCount; i++)
            {
                if (cachedLocals[i] == Vector2Int.zero)
                {
                    anchor = i;
                    break;
                }
            }

            SyncContainedInner(image.transform, anchor, innerColor);
        }

        int extraIndex = 0;
        for (int i = 0; i < cachedCellCount; i++)
        {
            if (cachedLocals[i] == Vector2Int.zero)
            {
                continue;
            }

            if (extraIndex >= extraCellImages.Count)
            {
                break;
            }

            Image extraImage = extraCellImages[extraIndex];
            extraIndex++;
            if (extraImage == null)
            {
                continue;
            }

            SyncContainedInner(extraImage.transform, i, innerColor);
        }
    }

    private void SyncContainedInner(Transform parent, int cellIndex, Color innerColor)
    {
        bool showInner = HasInnerLayer(cellIndex);
        PieceGameplayVisuals.SyncInnerOverlay(
            parent,
            showInner ? SpriteFor(cachedShapes[cellIndex]) : null,
            showInner,
            innerColor,
            PieceGameplayVisuals.NestedInnerLook.FromTheme(theme),
            SpriteFor(cachedOuters[cellIndex]));
    }

    private Sprite SpriteFor(ShapeType type)
    {
        return ShapeVisuals.SpriteFor(
            type,
            ShapeVisuals.First(theme != null ? theme.targetSquare : null, squareSprite),
            ShapeVisuals.First(theme != null ? theme.targetCircle : null, circleSprite),
            ShapeVisuals.First(theme != null ? theme.targetTriangle : null, triangleSprite));
    }

    private void ApplyTargetVisualAlpha(float alpha, bool enabled)
    {
        Color color = restColor;
        color.a = restColor.a * Mathf.Clamp01(alpha);
        if (image != null)
        {
            image.color = color;
            image.enabled = enabled;
        }

        ApplyColorToExtraCells(color);
        for (int i = 0; i < extraCellImages.Count; i++)
        {
            if (extraCellImages[i] != null)
            {
                extraCellImages[i].enabled = enabled;
            }
        }
    }

    private void ApplyColorToExtraCells(Color color)
    {
        for (int i = 0; i < extraCellImages.Count; i++)
        {
            if (extraCellImages[i] != null)
            {
                extraCellImages[i].color = color;
            }
        }
    }

    private void StopReadyRoutine()
    {
        if (readyRoutine != null)
        {
            StopCoroutine(readyRoutine);
            readyRoutine = null;
        }
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
        RebuildCache();
        RefreshSerializedVisualState();
    }
#endif

    private void OnDestroy()
    {
        if (isRegistered && boardManager != null)
        {
            boardManager.UnregisterTarget(this);
            isRegistered = false;
        }
    }
}
