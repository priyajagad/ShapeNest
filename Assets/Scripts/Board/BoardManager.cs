using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Grid layout for ShapeNest on a UI Canvas.
/// Lives on the Board RectTransform. Cell (0, 0) is the bottom-left cell.
/// Visual cell size comes from the Board rect; serialized cellSize is kept for compatibility.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class BoardManager : MonoBehaviour
{
    private const string RuntimeGridName = "RuntimeGrid";
    private const float LineThickness = 2f;

    [SerializeField]
    [Min(1)]
    private int width = 5;

    [SerializeField]
    [Min(1)]
    private int height = 5;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Legacy field. Visual cell size is driven by the Board RectTransform.")]
    private float cellSize = 1f;

    [SerializeField]
    private bool showGrid = true;

    [SerializeField]
    private bool debugOccupancy;

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private RectTransform boardRectTransform;
    private RectTransform runtimeGridRoot;
    private Sprite lineSprite;
    private int builtWidth;
    private int builtHeight;
    private readonly Dictionary<Vector2Int, Block> occupancy = new Dictionary<Vector2Int, Block>();
    private readonly Dictionary<Vector2Int, Target> targets = new Dictionary<Vector2Int, Target>();

    private RectTransform BoardRect
    {
        get
        {
            if (boardRectTransform == null)
            {
                boardRectTransform = (RectTransform)transform;
            }

            return boardRectTransform;
        }
    }

    /// <summary>
    /// Cell size in Board local UI units, derived from the RectTransform.
    /// </summary>
    public Vector2 VisualCellSize
    {
        get
        {
            Rect rect = BoardRect.rect;
            return new Vector2(rect.width / width, rect.height / height);
        }
    }

    /// <summary>
    /// Local / anchored position of the cell center, relative to the Board RectTransform.
    /// Uses Rect.xMin / yMin so (0, 0) stays the bottom-left cell regardless of pivot.
    /// </summary>
    public Vector3 GridToLocal(Vector2Int gridCoordinate)
    {
        Rect rect = BoardRect.rect;
        Vector2 cell = VisualCellSize;
        float x = rect.xMin + (gridCoordinate.x + 0.5f) * cell.x;
        float y = rect.yMin + (gridCoordinate.y + 0.5f) * cell.y;
        return new Vector3(x, y, 0f);
    }

    /// <summary>
    /// UI local position of the cell center (same as GridToLocal).
    /// Kept so existing call sites keep a familiar name.
    /// </summary>
    public Vector3 GridToWorld(Vector2Int gridCoordinate)
    {
        return GridToLocal(gridCoordinate);
    }

    /// <summary>
    /// Converts a Board-local UI position to the nearest cell.
    /// Input is RectTransform local space, not world or screen space.
    /// </summary>
    public Vector2Int LocalToGrid(Vector3 localPosition)
    {
        Rect rect = BoardRect.rect;
        Vector2 cell = VisualCellSize;

        if (cell.x <= 0f || cell.y <= 0f)
        {
            return Vector2Int.zero;
        }

        // Invert GridToLocal: subtract bottom-left, divide by cell size, then nearest center.
        float x = (localPosition.x - rect.xMin) / cell.x - 0.5f;
        float y = (localPosition.y - rect.yMin) / cell.y - 0.5f;
        return new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
    }

    /// <summary>
    /// Same as LocalToGrid. The argument is Board-local UI coordinates, not world space.
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 localPosition)
    {
        return LocalToGrid(localPosition);
    }

    public bool IsInsideBoard(Vector2Int gridCoordinate)
    {
        return gridCoordinate.x >= 0
            && gridCoordinate.x < width
            && gridCoordinate.y >= 0
            && gridCoordinate.y < height;
    }

    public bool IsCellOccupied(Vector2Int gridPosition)
    {
        return occupancy.TryGetValue(gridPosition, out Block occupant) && occupant != null;
    }

    public Block GetBlockAt(Vector2Int gridPosition)
    {
        occupancy.TryGetValue(gridPosition, out Block occupant);
        return occupant;
    }

    public bool TryRegisterBlock(Block block, Vector2Int gridPosition)
    {
        if (block == null || !IsInsideBoard(gridPosition))
        {
            return false;
        }

        Block occupant = GetBlockAt(gridPosition);
        if (occupant != null && occupant != block)
        {
            LogOccupancy($"Register rejected: {gridPosition} already has {occupant.name}");
            return false;
        }

        UnregisterBlock(block);
        occupancy[gridPosition] = block;
        LogOccupancy($"Registered {block.name} at {gridPosition}");
        return true;
    }

    public void UnregisterBlock(Block block)
    {
        if (block == null)
        {
            return;
        }

        List<Vector2Int> keysToRemove = null;
        foreach (KeyValuePair<Vector2Int, Block> entry in occupancy)
        {
            if (entry.Value != block)
            {
                continue;
            }

            keysToRemove ??= new List<Vector2Int>();
            keysToRemove.Add(entry.Key);
        }

        if (keysToRemove == null)
        {
            return;
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            occupancy.Remove(keysToRemove[i]);
            LogOccupancy($"Unregistered {block.name} from {keysToRemove[i]}");
        }
    }

    public bool TryMoveBlock(Block block, Vector2Int from, Vector2Int to)
    {
        if (block == null || !IsInsideBoard(to))
        {
            return false;
        }

        Block occupant = GetBlockAt(to);
        if (occupant != null && occupant != block)
        {
            LogOccupancy($"Move rejected: {block.name} {from} -> {to} occupied by {occupant.name}");
            return false;
        }

        if (from == to)
        {
            occupancy[to] = block;
            return true;
        }

        Block atFrom = GetBlockAt(from);
        if (atFrom != null && atFrom != block)
        {
            LogOccupancy($"Move rejected: {from} belongs to {atFrom.name}, not {block.name}");
            return false;
        }

        if (atFrom == block)
        {
            occupancy.Remove(from);
        }
        else
        {
            UnregisterBlock(block);
        }

        occupancy[to] = block;
        LogOccupancy($"Moved {block.name} {from} -> {to}");
        return true;
    }

    public bool TryRegisterTarget(Target target)
    {
        if (target == null || !IsInsideBoard(target.GridPosition))
        {
            return false;
        }

        Target existing = GetTargetAt(target.GridPosition);
        if (existing != null && existing != target)
        {
            return false;
        }

        UnregisterTarget(target);
        targets[target.GridPosition] = target;
        return true;
    }

    /// <summary>
    /// Clears block occupancy and target registration after a finished match.
    /// The cell is then empty for movement. Does not destroy objects.
    /// </summary>
    public void ReleaseMatchedCell(Block block, Target target)
    {
        UnregisterBlock(block);
        UnregisterTarget(target);
    }

    public void UnregisterTarget(Target target)
    {
        if (target == null)
        {
            return;
        }

        List<Vector2Int> keysToRemove = null;
        foreach (KeyValuePair<Vector2Int, Target> entry in targets)
        {
            if (entry.Value != target)
            {
                continue;
            }

            keysToRemove ??= new List<Vector2Int>();
            keysToRemove.Add(entry.Key);
        }

        if (keysToRemove == null)
        {
            return;
        }

        for (int i = 0; i < keysToRemove.Count; i++)
        {
            targets.Remove(keysToRemove[i]);
        }
    }

    public Target GetTargetAt(Vector2Int position)
    {
        targets.TryGetValue(position, out Target target);
        return target;
    }

    public bool IsTargetCell(Vector2Int position)
    {
        return GetTargetAt(position) != null;
    }

    public bool IsMatchingTarget(Block block)
    {
        if (block == null)
        {
            return false;
        }

        Target target = GetTargetAt(block.GridPosition);
        return target != null && target.ShapeType == block.ShapeType;
    }

    public bool AreAllBlocksSettled()
    {
        if (occupancy.Count == 0)
        {
            return false;
        }

        foreach (Block registeredBlock in occupancy.Values)
        {
            if (registeredBlock == null || !registeredBlock.IsSettled)
            {
                return false;
            }
        }

        return true;
    }

    private void LogOccupancy(string message)
    {
        if (debugOccupancy)
        {
            Debug.Log($"[Board Occupancy] {message}", this);
        }
    }

    private void OnEnable()
    {
        RefreshRuntimeGrid();
    }

    private void OnDisable()
    {
        SetRuntimeGridVisible(false);
    }

    private void OnDestroy()
    {
        DestroyLineSprite();
    }

    private void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        cellSize = Mathf.Max(0.01f, cellSize);
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StretchRuntimeGrid();
    }

    private void Update()
    {
        RefreshRuntimeGrid();
    }

    private void OnDrawGizmos()
    {
        if (!showGrid)
        {
            return;
        }

        Gizmos.color = new Color(1f, 1f, 1f, 0.7f);

        for (int x = 0; x <= width; x++)
        {
            Gizmos.DrawLine(GetCornerWorld(x, 0), GetCornerWorld(x, height));
        }

        for (int y = 0; y <= height; y++)
        {
            Gizmos.DrawLine(GetCornerWorld(0, y), GetCornerWorld(width, y));
        }
    }

    private Vector3 GetCornerWorld(int cornerX, int cornerY)
    {
        return BoardRect.TransformPoint(GetCornerLocal(cornerX, cornerY));
    }

    private Vector2 GetCornerLocal(int cornerX, int cornerY)
    {
        Rect rect = BoardRect.rect;
        Vector2 cell = VisualCellSize;
        return new Vector2(rect.xMin + cornerX * cell.x, rect.yMin + cornerY * cell.y);
    }

    private void RefreshRuntimeGrid()
    {
        if (!showGrid)
        {
            SetRuntimeGridVisible(false);
            return;
        }

        if (runtimeGridRoot == null || builtWidth != width || builtHeight != height)
        {
            BuildRuntimeGrid();
        }

        StretchRuntimeGrid();
        SetRuntimeGridVisible(true);
    }

    private void BuildRuntimeGrid()
    {
        EnsureRuntimeGridRoot();
        ClearRuntimeGridChildren();
        EnsureLineSprite();

        builtWidth = width;
        builtHeight = height;

        for (int x = 0; x <= width; x++)
        {
            float t = width == 0 ? 0f : (float)x / width;
            CreateGridLine($"Vertical_{x}", new Vector2(t, 0f), new Vector2(t, 1f), new Vector2(LineThickness, 0f));
        }

        for (int y = 0; y <= height; y++)
        {
            float t = height == 0 ? 0f : (float)y / height;
            CreateGridLine($"Horizontal_{y}", new Vector2(0f, t), new Vector2(1f, t), new Vector2(0f, LineThickness));
        }
    }

    private void EnsureRuntimeGridRoot()
    {
        if (runtimeGridRoot != null)
        {
            return;
        }

        Transform existing = transform.Find(RuntimeGridName);
        if (existing != null)
        {
            runtimeGridRoot = existing as RectTransform;
            if (runtimeGridRoot == null)
            {
                DestroyImmediate(existing.gameObject);
            }
            else
            {
                return;
            }
        }

        var rootObject = new GameObject(RuntimeGridName, typeof(RectTransform));
        runtimeGridRoot = rootObject.GetComponent<RectTransform>();
        runtimeGridRoot.SetParent(BoardRect, false);
        runtimeGridRoot.gameObject.layer = gameObject.layer;
        runtimeGridRoot.hideFlags = HideFlags.DontSave;
    }

    private void StretchRuntimeGrid()
    {
        if (runtimeGridRoot == null)
        {
            return;
        }

        runtimeGridRoot.anchorMin = Vector2.zero;
        runtimeGridRoot.anchorMax = Vector2.one;
        runtimeGridRoot.offsetMin = Vector2.zero;
        runtimeGridRoot.offsetMax = Vector2.zero;
        runtimeGridRoot.pivot = BoardRect.pivot;
        runtimeGridRoot.localScale = Vector3.one;
        runtimeGridRoot.localRotation = Quaternion.identity;
    }

    private void CreateGridLine(string lineName, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        var lineObject = new GameObject(lineName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var lineRect = lineObject.GetComponent<RectTransform>();
        lineRect.SetParent(runtimeGridRoot, false);
        lineObject.layer = gameObject.layer;
        lineObject.hideFlags = HideFlags.DontSave;

        lineRect.anchorMin = anchorMin;
        lineRect.anchorMax = anchorMax;
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.sizeDelta = sizeDelta;

        var image = lineObject.GetComponent<Image>();
        image.sprite = lineSprite;
        image.color = new Color(1f, 1f, 1f, 0.7f);
        image.raycastTarget = false;
    }

    private void ClearRuntimeGridChildren()
    {
        if (runtimeGridRoot == null)
        {
            return;
        }

        for (int i = runtimeGridRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = runtimeGridRoot.GetChild(i);
            DestroyImmediate(child.gameObject);
        }
    }

    private void SetRuntimeGridVisible(bool visible)
    {
        if (runtimeGridRoot != null)
        {
            runtimeGridRoot.gameObject.SetActive(visible);
        }
    }

    private void EnsureLineSprite()
    {
        if (lineSprite != null)
        {
            return;
        }

        Texture2D texture = Texture2D.whiteTexture;
        lineSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
        lineSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void DestroyLineSprite()
    {
        if (lineSprite == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(lineSprite);
        }
        else
        {
            DestroyImmediate(lineSprite);
        }

        lineSprite = null;
    }
}
