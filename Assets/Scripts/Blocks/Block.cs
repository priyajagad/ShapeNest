using System.Collections;
using System.Collections.Generic;
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
    [Range(0.1f, 1f)]
    [Tooltip("Image alpha when the block is settled. Does not affect occupancy.")]
    private float settledAlpha = 0.55f;

    [SerializeField]
    [Range(1f, 1.08f)]
    [Tooltip("Subtle scale multiplier while pressed. Keep close to 1.")]
    private float dragSelectScale = 1.02f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Time to ease into and out of drag selection scale.")]
    private float dragSelectDuration = 0.07f;

    private BoardManager boardManager;
    public bool isSettled { get; set; }
    private Color restColor = Color.white;
    private bool hasCachedRestColor;
    private Vector3 restScale = Vector3.one;
    private bool hasRestScale;
    private Image image;
    private RectTransform cachedRect;
    private VisualState visualState = VisualState.Normal;
    private bool dragSelected;
    private Coroutine selectionRoutine;
    private int restSiblingIndex;
    private bool hasRestSiblingIndex;
    private PiecePresentation piecePresentation;
    private readonly List<Image> extraCellImages = new List<Image>();
    private Vector2Int[] cachedLocals = { Vector2Int.zero };
    private ShapeType[] cachedShapes = { ShapeType.Square };
    private ShapeType[] cachedOuters = { ShapeType.Square };
    private int cachedCellCount = 1;

    private HashSet<int> cellsHiddenForTravel = new HashSet<int>();

    public ShapeType ShapeType
    {
        get => shapeType;
        set
        {
            shapeType = value;
            SetAnchorShape(value);
            RefreshVisual();
        }
    }

    public int CellCount => cachedCellCount;

    public PieceComposition Composition => composition;
    public ShapeType OuterShape => outerShape;

    public IReadOnlyList<ShapeCellData> Cells => cells;

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
        RebuildCache();
        RefreshVisual();
    }

    private void OnEnable()
    {
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

    public ShapeType GetActiveShape(int index)
    {
        if (index < 0 || index >= cachedCellCount)
        {
            return shapeType;
        }

        return cachedShapes[index];
    }

    public ShapeCellData GetCell(int index)
    {
        if (cells == null || index < 0 || index >= cells.Count)
        {
            return null;
        }

        return cells[index];
    }

    public bool HasInnerLayerAt(int index)
    {
        return HasInnerLayer(index);
    }

    public Vector2Int GetCellWorld(int index)
    {
        return gridPosition + GetLocalCell(index);
    }

    public Sprite GetCellVisualSprite(int index)
    {
        if (index < 0 || index >= cachedCellCount)
        {
            return SpriteFor(shapeType);
        }

        return SpriteFor(HasInnerLayer(index) ? cachedShapes[index] : cachedOuters[index]);
    }

    public Sprite GetCellOuterSprite(int index)
    {
        if (index < 0 || index >= cachedCellCount)
        {
            return SpriteFor(shapeType);
        }

        return SpriteFor(cachedOuters[index]);
    }

    public Image GetCellImage(int index)
    {
        CacheImage();
        if (index < 0 || index >= cachedCellCount)
        {
            return image;
        }

        if (cachedLocals[index] == Vector2Int.zero)
        {
            return image;
        }

        int extraIndex = 0;
        for (int i = 0; i < cachedCellCount; i++)
        {
            if (cachedLocals[i] == Vector2Int.zero)
            {
                continue;
            }

            if (i == index)
            {
                return extraIndex < extraCellImages.Count ? extraCellImages[extraIndex] : null;
            }

            extraIndex++;
        }

        return null;
    }

    public void SetCellVisualVisible(int index, bool visible)
    {
        // 1. Track or enforce the suppression state
        if (!visible)
        {
            cellsHiddenForTravel.Add(index);
        }
        else
        {
            // If the traveler is still owning this cell, reject attempts to turn it back on
            if (cellsHiddenForTravel.Contains(index))
            {
                return;
            }
        }

        Image cellImage = GetCellImage(index);
        if (cellImage == null)
        {
            return;
        }

        cellImage.enabled = visible;
        if (!visible)
        {
            PieceGameplayVisuals.HideInnerOverlay(cellImage.transform);
        }
        else if (HasInnerLayer(index))
        {
            SyncContainedInner(cellImage.transform, index);
        }
    }

    public void RefreshLayoutVisuals()
    {
        SyncVisualSizeToBoard();
        SetGridPosition(gridPosition);
        RefreshVisual();

        // 2. Rebuild the visuals normally
        RebuildCellVisuals();

        ApplyTravelHiddenVisuals();
    }

    // 4. Add this cleanup method so BlockMover can release the lock when VFX completes
    public void ClearTravelState(int index)
    {
        cellsHiddenForTravel.Remove(index);
    }

    public void RefreshActiveLayers()
    {
        RebuildCache();
        SyncVisualSizeToBoard();
        RefreshVisual();
        RebuildCellVisuals();
    }

    public void RebuildFromRemaining(IReadOnlyList<ShapeCellData> remaining, Vector2Int worldAnchor)
    {
        // The consumed cell no longer exists after this rebuild. Its old index
        // must not suppress a new survivor that later occupies the same index.
        cellsHiddenForTravel.Clear();
        ShapeType nextShape = shapeType;
        ShapeType nextOuter = outerShape;
        if (remaining != null && remaining.Count > 0 && remaining[0] != null)
        {
            nextShape = ShapeLayout.ActiveShape(remaining[0], remaining[0].shapeType);
            nextOuter = remaining[0].shapeType;
        }

        ApplyLayout(nextShape, remaining, PieceComposition.Simple, nextOuter);
        ResetMatchPresentation();
        isSettled = false;
        SetGridPosition(worldAnchor);
        if (boardManager != null)
        {
            boardManager.TryRegisterBlock(this, worldAnchor);
        }
    }

    public void Initialize(BoardManager board, Vector2Int startPosition)
    {
        ResetMatchPresentation();
        boardManager = board;
        RebuildCache();
        SyncVisualSizeToBoard();
        SetGridPosition(startPosition);
        RefreshVisual();
        RebuildCellVisuals();

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
        RaiseInDrawOrder();
        AnimateSelectionScale(restScale * dragSelectScale, 1f);
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
        RestoreDrawOrder();
        if (!isActiveAndEnabled)
        {
            StopSelectionRoutine();
            SetHeldPresentation(false);
            RectTransform.localScale = restScale;
            return;
        }

        AnimateSelectionScale(restScale, 0f);
    }

    public void CancelDragSelectionImmediate()
    {
        dragSelected = false;
        StopSelectionRoutine();
        RestoreDrawOrder();
        SetHeldPresentation(false);
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

        SetExtraCellsRaycast(false);
    }

    public void SetMatchPresentation(float scale, float alpha)
    {
        visualState = VisualState.Matching;
        CaptureRestScale();
        CaptureRestColor();
        ApplyVisualToAll(restScale * scale, restColor.a * Mathf.Clamp01(alpha), alpha > 0.001f);
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

        ApplyAlphaToExtraCells(0f, false);
        SetExtraCellsRaycast(false);
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
            image.enabled = !cellsHiddenForTravel.Contains(FindAnchorIndex());
            image.raycastTarget = true;
        }

        SetExtraCellsRaycast(true);
        ApplyAlphaToExtraCells(1f, true);
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
        ApplyColorToExtraCells(color);
        if (image != null)
        {
            PieceGameplayVisuals.ApplyOverlayColor(image.transform, color);
        }
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
        if (image.sprite != sprite)
        {
            image.sprite = sprite;
        }

        image.preserveAspect = true;
        if (!IsMatchVisual)
        {
            image.raycastTarget = true;
        }

        ApplyExtraCellSprites();

        PiecePresentation presentation = CachePresentation();
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

    public bool HasActiveInnerLayer()
    {
        return HasInnerLayer(0) || HasInnerLayer(FindAnchorIndex());
    }

    public PieceGameplayVisuals.NestedInnerLook NestedInnerLook =>
        PieceGameplayVisuals.NestedInnerLook.FromTheme(theme);

    public Sprite ContainedInnerSprite()
    {
        int anchor = FindAnchorIndex();
        return SpriteFor(HasInnerLayer(anchor) ? cachedShapes[anchor] : GetActiveShape(anchor));
    }

    public Vector2 VisualSizeDelta
    {
        get
        {
            CacheImage();
            return image != null ? image.rectTransform.sizeDelta : new Vector2(64f, 64f);
        }
    }

    public void HideContainedInnerVisuals()
    {
        CacheImage();
        if (image != null)
        {
            PieceGameplayVisuals.HideInnerOverlay(image.transform);
        }

        for (int i = 0; i < extraCellImages.Count; i++)
        {
            Image extraImage = extraCellImages[i];
            if (extraImage != null)
            {
                PieceGameplayVisuals.HideInnerOverlay(extraImage.transform);
            }
        }
    }

    public void PresentInnerEntryVisual()
    {
        HideContainedInnerVisuals();
    }

    private int FindAnchorIndex()
    {
        for (int i = 0; i < cachedCellCount; i++)
        {
            if (cachedLocals[i] == Vector2Int.zero)
            {
                return i;
            }
        }

        return 0;
    }

    private bool HasInnerLayer(int index)
    {
        ShapeCellData cell = GetCell(index);
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
        PiecePresentation presentation = CachePresentation();
        if (presentation != null)
        {
            presentation.SetVisualSize(size);
        }
    }

    private Color ConnectorColor()
    {
        Color color = theme != null ? theme.accent : new Color(0.55f, 0.48f, 0.78f, 1f);
        color.a = 0.9f;
        return color;
    }

    private void RebuildCellVisuals()
    {
        CacheImage();
        SyncVisualSizeToBoard();
        EnsureExtraCellCount();
        ApplyExtraCellSprites();
        LayoutExtraCells();
        ApplyTravelHiddenVisuals();
        if (!PieceGameplayVisuals.CanMutateHierarchy(transform))
        {
            return;
        }

        ApplyNestedOverlays();
        if (boardManager != null)
        {
            PieceGameplayVisuals.RebuildConnectors(
                RectTransform,
                cachedLocals,
                cachedCellCount,
                boardManager.VisualCellSize,
                ConnectorColor());
        }
        else
        {
            PieceGameplayVisuals.ClearConnectors(RectTransform);
        }
    }

    private void ApplyTravelHiddenVisuals()
    {
        foreach (int index in cellsHiddenForTravel)
        {
            Image cellImage = GetCellImage(index);
            if (cellImage == null)
            {
                continue;
            }

            cellImage.enabled = false;
            PieceGameplayVisuals.HideInnerOverlay(cellImage.transform);
        }
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
                DestroyImmediateIfNeeded(extra.gameObject);
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
        extraImage.raycastTarget = !IsMatchVisual;
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
        }

        if (image != null && !IsMatchVisual)
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
            extraImage.raycastTarget = !IsMatchVisual;
        }
    }

    private void ApplyNestedOverlays()
    {
        CacheImage();
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

            SyncContainedInner(image.transform, anchor);
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

            SyncContainedInner(extraImage.transform, i);
        }
    }

    private void SyncContainedInner(Transform parent, int cellIndex)
    {
        bool showInner = HasInnerLayer(cellIndex);
        PieceGameplayVisuals.SyncInnerOverlay(
            parent,
            showInner ? SpriteFor(cachedShapes[cellIndex]) : null,
            showInner,
            Color.white,
            NestedInnerLook,
            SpriteFor(cachedOuters[cellIndex]));
    }

    private Sprite SpriteFor(ShapeType type)
    {
        return ShapeVisuals.SpriteFor(
            type,
            ShapeVisuals.First(theme != null ? theme.blockSquare : null, squareSprite),
            ShapeVisuals.First(theme != null ? theme.blockCircle : null, circleSprite),
            ShapeVisuals.First(theme != null ? theme.blockTriangle : null, triangleSprite));
    }

    private void ApplyVisualToAll(Vector3 scale, float alpha, bool enabled)
    {
        RectTransform.localScale = scale;
        CacheImage();
        CaptureRestColor();
        Color color = restColor;
        color.a = alpha;
        if (image != null)
        {
            image.color = color;
            image.enabled = enabled && !cellsHiddenForTravel.Contains(FindAnchorIndex());
        }

        ApplyColorToExtraCells(color);
        ApplyAlphaToExtraCells(alpha, enabled);
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

    private void ApplyAlphaToExtraCells(float alpha, bool enabled)
    {
        CaptureRestColor();
        Color color = restColor;
        color.a = restColor.a * Mathf.Clamp01(alpha);
        int extraIndex = 0;
        for (int cellIndex = 0; cellIndex < cachedCellCount; cellIndex++)
        {
            if (cachedLocals[cellIndex] == Vector2Int.zero)
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

            extraImage.color = color;
            bool cellEnabled = enabled && !cellsHiddenForTravel.Contains(cellIndex);
            if (cellEnabled)
            {
                PieceGameplayVisuals.ApplyOverlayColor(extraImage.transform, color);
            }
            else
            {
                PieceGameplayVisuals.HideInnerOverlay(extraImage.transform);
            }

            extraImage.enabled = cellEnabled;
        }
    }

    private void SetExtraCellsRaycast(bool enabled)
    {
        for (int i = 0; i < extraCellImages.Count; i++)
        {
            if (extraCellImages[i] != null)
            {
                extraCellImages[i].raycastTarget = enabled;
            }
        }
    }

    private void DestroyImmediateIfNeeded(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private PiecePresentation CachePresentation()
    {
        if (piecePresentation == null)
        {
            piecePresentation = GetComponent<PiecePresentation>();
        }

        return piecePresentation;
    }

    private void SetHeldPresentation(bool held)
    {
        SetHeldBlend(held ? 1f : 0f);
    }

    private void SetHeldBlend(float blend)
    {
        PiecePresentation presentation = CachePresentation();
        if (presentation != null)
        {
            presentation.SetHeldBlend(blend);
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

    private void AnimateSelectionScale(Vector3 targetScale, float heldBlend)
    {
        StopSelectionRoutine();
        if (dragSelectDuration <= 0f)
        {
            RectTransform.localScale = targetScale;
            SetHeldBlend(heldBlend);
            return;
        }

        selectionRoutine = StartCoroutine(SelectionScaleRoutine(RectTransform.localScale, targetScale, heldBlend));
    }

    private IEnumerator SelectionScaleRoutine(Vector3 from, Vector3 to, float heldBlendTo)
    {
        PiecePresentation presentation = CachePresentation();
        float heldBlendFrom = presentation != null ? presentation.HeldBlend : heldBlendTo;
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
            float eased = Mathf.SmoothStep(0f, 1f, t);
            RectTransform.localScale = Vector3.LerpUnclamped(from, to, eased);
            SetHeldBlend(Mathf.LerpUnclamped(heldBlendFrom, heldBlendTo, eased));
            yield return null;
        }

        if (!IsMatchVisual)
        {
            RectTransform.localScale = to;
            SetHeldBlend(heldBlendTo);
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

    private void RaiseInDrawOrder()
    {
        if (!hasRestSiblingIndex)
        {
            restSiblingIndex = RectTransform.GetSiblingIndex();
            hasRestSiblingIndex = true;
        }

        RectTransform.SetAsLastSibling();
    }

    private void RestoreDrawOrder()
    {
        if (!hasRestSiblingIndex)
        {
            return;
        }

        Transform parent = RectTransform.parent;
        int maxIndex = parent != null ? parent.childCount - 1 : restSiblingIndex;
        RectTransform.SetSiblingIndex(Mathf.Clamp(restSiblingIndex, 0, maxIndex));
        hasRestSiblingIndex = false;
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
        RebuildCache();
        RefreshSerializedVisualState();
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
