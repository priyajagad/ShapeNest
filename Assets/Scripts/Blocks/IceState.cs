using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Block))]
public class IceState : MonoBehaviour
{
    private const string OverlayName = "IceOverlay";

    private Block block;
    private RectTransform overlayRoot;
    private Image overlayImage;
    private TMP_Text durabilityText;
    private int durability;
    private bool configured;
    private Coroutine feedbackRoutine;

    public int Durability => durability;
    public bool IsFrozen => configured && durability > 0;

    public void Configure(Block source, bool enabled, int startingDurability)
    {
        block = source != null ? source : GetComponent<Block>();
        configured = enabled;
        durability = enabled ? Mathf.Max(1, startingDurability) : 0;
        EnsureVisual();
        RefreshVisual();
    }

    public void ConsumeSuccessfulMatch()
    {
        if (!IsFrozen)
        {
            return;
        }

        durability = Mathf.Max(0, durability - 1);
        RefreshVisual();
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(FeedbackRoutine(durability > 0));
    }

    private void Awake()
    {
        block = GetComponent<Block>();
    }

    private void EnsureVisual()
    {
        if (overlayRoot != null)
        {
            return;
        }

        GameObject root = new GameObject(OverlayName, typeof(RectTransform));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        overlayRoot = root.GetComponent<RectTransform>();
        overlayRoot.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRoot.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRoot.pivot = new Vector2(0.5f, 0.5f);
        overlayRoot.SetAsLastSibling();

        GameObject imageObject = new GameObject("IceTint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = gameObject.layer;
        imageObject.transform.SetParent(overlayRoot, false);
        RectTransform imageRect = imageObject.GetComponent<RectTransform>();
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = Vector2.zero;
        imageRect.offsetMax = Vector2.zero;
        overlayImage = imageObject.GetComponent<Image>();
        overlayImage.color = new Color(0.35f, 0.75f, 1f, 0.36f);
        overlayImage.raycastTarget = false;

        GameObject textObject = new GameObject("IceDurability", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = gameObject.layer;
        textObject.transform.SetParent(overlayRoot, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(1f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(1f, 1f);
        textRect.anchoredPosition = new Vector2(-6f, -6f);
        textRect.sizeDelta = new Vector2(110f, 50f);
        durabilityText = textObject.GetComponent<TextMeshProUGUI>();
        durabilityText.fontSize = 28f;
        durabilityText.alignment = TextAlignmentOptions.TopRight;
        durabilityText.color = Color.white;
        durabilityText.raycastTarget = false;
    }

    private void RefreshVisual()
    {
        EnsureVisual();
        if (!configured || durability <= 0 || block == null)
        {
            overlayRoot.gameObject.SetActive(false);
            return;
        }

        overlayRoot.gameObject.SetActive(true);
        durabilityText.text = $"ICE {durability}";
        RefreshBounds();
    }

    private void RefreshBounds()
    {
        if (block.Board == null || block.CellCount <= 0)
        {
            overlayRoot.anchoredPosition = Vector2.zero;
            overlayRoot.sizeDelta = block.VisualSizeDelta;
            return;
        }

        Vector2 cellSize = block.Board.VisualCellSize;
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        for (int i = 0; i < block.CellCount; i++)
        {
            Vector2Int local = block.GetLocalCell(i);
            minX = Mathf.Min(minX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxX = Mathf.Max(maxX, local.x);
            maxY = Mathf.Max(maxY, local.y);
        }

        Vector2 padding = cellSize * 0.12f;
        overlayRoot.anchoredPosition = new Vector2(
            (minX + maxX) * cellSize.x * 0.5f,
            (minY + maxY) * cellSize.y * 0.5f);
        overlayRoot.sizeDelta = new Vector2(
            (maxX - minX + 1) * cellSize.x + padding.x * 2f,
            (maxY - minY + 1) * cellSize.y + padding.y * 2f);
    }

    private IEnumerator FeedbackRoutine(bool stillFrozen)
    {
        if (overlayRoot == null)
        {
            yield break;
        }

        Vector3 restScale = Vector3.one;
        Vector3 peakScale = stillFrozen ? Vector3.one * 1.06f : Vector3.one * 1.14f;
        float duration = stillFrozen ? 0.12f : 0.22f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(t * Mathf.PI);
            overlayRoot.localScale = Vector3.LerpUnclamped(restScale, peakScale, pulse);
            yield return null;
        }

        overlayRoot.localScale = restScale;
        if (!stillFrozen)
        {
            overlayRoot.gameObject.SetActive(false);
        }

        feedbackRoutine = null;
    }
}
