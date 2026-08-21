using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime state and presentation for a level shutter. A shutter covers board cells
/// independently of the blocks/targets underneath it. Every successful match reduces
/// durability by one; at zero the shutter opens and its covered cells become usable.
/// </summary>
public class ShutterState : MonoBehaviour
{
    private static Sprite sharedWhiteSprite;

    private BoardManager boardManager;
    private RectTransform visualRoot;
    private TMP_Text durabilityText;
    private readonly List<Vector2Int> cells = new List<Vector2Int>();
    private int durability;
    private Coroutine openRoutine;

    public int Durability => durability;
    public bool IsClosed => durability > 0;
    public IReadOnlyList<Vector2Int> Cells => cells;

    public void Configure(BoardManager board, LevelShutterData data)
    {
        boardManager = board;
        cells.Clear();
        if (data != null && data.cells != null)
        {
            cells.AddRange(data.cells);
        }

        durability = Mathf.Max(1, data != null ? data.durability : 1);
        EnsureVisual();
        RefreshVisual();
        if (boardManager != null)
        {
            boardManager.RegisterShutter(this);
        }
    }

    public bool CoversCell(Vector2Int cell)
    {
        return IsClosed && cells.Contains(cell);
    }

    public void ConsumeSuccessfulMatch()
    {
        if (!IsClosed)
        {
            return;
        }

        durability = Mathf.Max(0, durability - 1);
        RefreshVisual();

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
        }

        openRoutine = StartCoroutine(FeedbackRoutine());
    }

    private void Awake()
    {
        EnsureVisual();
    }

    private void OnDestroy()
    {
        if (boardManager != null)
        {
            boardManager.UnregisterShutter(this);
        }

    }

    private void EnsureVisual()
    {
        if (visualRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("ShutterOverlay", typeof(RectTransform));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        visualRoot = root.GetComponent<RectTransform>();
        visualRoot.anchorMin = new Vector2(0.5f, 0.5f);
        visualRoot.anchorMax = new Vector2(0.5f, 0.5f);
        visualRoot.pivot = new Vector2(0.5f, 0.5f);
        visualRoot.anchoredPosition = Vector2.zero;
        visualRoot.sizeDelta = Vector2.zero;
        visualRoot.SetAsLastSibling();

        GameObject textObject = new GameObject("ShutterDurability", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(visualRoot, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(180f, 80f);
        durabilityText = textObject.GetComponent<TextMeshProUGUI>();
        durabilityText.fontSize = 34f;
        durabilityText.fontStyle = FontStyles.Bold;
        durabilityText.alignment = TextAlignmentOptions.Center;
        durabilityText.color = Color.white;
        durabilityText.raycastTarget = false;
    }

    public void RefreshLayoutVisuals()
    {
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        EnsureVisual();
        ClearCellVisuals();

        if (!IsClosed || boardManager == null || cells.Count == 0)
        {
            visualRoot.gameObject.SetActive(false);
            return;
        }

        visualRoot.gameObject.SetActive(true);
        visualRoot.SetAsLastSibling();
        CreateCellVisuals();
        durabilityText.transform.SetAsLastSibling();
        PositionLabel();
        durabilityText.text = durability.ToString();
    }

    private void CreateCellVisuals()
    {
        Sprite sprite = GetWhiteSprite();
        Vector2 cellSize = boardManager.VisualCellSize;

        for (int i = 0; i < cells.Count; i++)
        {
            GameObject cellObject = new GameObject($"ShutterCell_{cells[i].x}_{cells[i].y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cellObject.layer = gameObject.layer;
            cellObject.transform.SetParent(visualRoot, false);
            RectTransform rect = cellObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = boardManager.GridToLocal(cells[i]);
            rect.sizeDelta = cellSize + new Vector2(1f, 1f);

            Image image = cellObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(0.22f, 0.12f, 0.34f, 0.94f);
            image.raycastTarget = false;

            CreateSlat(cellObject.transform, new Vector2(0f, cellSize.y * 0.24f), new Vector2(cellSize.x, 2f));
            CreateSlat(cellObject.transform, new Vector2(0f, -cellSize.y * 0.24f), new Vector2(cellSize.x, 2f));
        }
    }

    private void CreateSlat(Transform parent, Vector2 position, Vector2 size)
    {
        GameObject slat = new GameObject("ShutterSlat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        slat.layer = gameObject.layer;
        slat.transform.SetParent(parent, false);
        RectTransform rect = slat.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = slat.GetComponent<Image>();
        image.sprite = GetWhiteSprite();
        image.color = new Color(0.42f, 0.28f, 0.56f, 0.9f);
        image.raycastTarget = false;
    }

    private void PositionLabel()
    {
        if (cells.Count == 0 || boardManager == null)
        {
            return;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        for (int i = 0; i < cells.Count; i++)
        {
            minX = Mathf.Min(minX, cells[i].x);
            minY = Mathf.Min(minY, cells[i].y);
            maxX = Mathf.Max(maxX, cells[i].x);
            maxY = Mathf.Max(maxY, cells[i].y);
        }

        Vector2 center = (boardManager.GridToLocal(new Vector2Int(minX, minY)) + boardManager.GridToLocal(new Vector2Int(maxX, maxY))) * 0.5f;
        Vector2 size = new Vector2(
            (maxX - minX + 1) * boardManager.VisualCellSize.x,
            (maxY - minY + 1) * boardManager.VisualCellSize.y);

        durabilityText.rectTransform.anchoredPosition = center;
        durabilityText.rectTransform.sizeDelta = new Vector2(Mathf.Max(120f, size.x * 0.75f), Mathf.Max(60f, size.y * 0.55f));
    }

    private void ClearCellVisuals()
    {
        if (visualRoot == null)
        {
            return;
        }

        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = visualRoot.GetChild(i);
            if (child == durabilityText?.transform)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private IEnumerator FeedbackRoutine()
    {
        if (visualRoot == null)
        {
            yield break;
        }

        Vector3 start = Vector3.one;
        Vector3 peak = IsClosed ? Vector3.one * 1.04f : Vector3.one * 1.12f;
        float duration = IsClosed ? 0.1f : 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            visualRoot.localScale = Vector3.LerpUnclamped(start, peak, Mathf.Sin(t * Mathf.PI));
            yield return null;
        }

        visualRoot.localScale = start;
        if (!IsClosed)
        {
            if (boardManager != null)
            {
                boardManager.UnregisterShutter(this);
            }

            visualRoot.gameObject.SetActive(false);
        }

        openRoutine = null;
    }

    private static Sprite GetWhiteSprite()
    {
        if (sharedWhiteSprite != null)
        {
            return sharedWhiteSprite;
        }

        Texture2D texture = Texture2D.whiteTexture;
        sharedWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
        sharedWhiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedWhiteSprite;
    }
}
