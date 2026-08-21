using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only match/merge effect. Does not touch occupancy, matching, or completion.
/// </summary>
public class MatchEffect : MonoBehaviour
{
    [SerializeField]
    private Sprite squareGlow;

    [SerializeField]
    private Sprite circleGlow;

    [SerializeField]
    private Sprite triangleGlow;

    [SerializeField]
    private Sprite diamondGlow;

    [SerializeField]
    private Sprite hexagonGlow;

    [SerializeField]
    private Sprite starGlow;

    [SerializeField]
    [Tooltip("Optional. Theme glow sprites override prefab sprites when assigned.")]
    private ShapeNestTheme theme;

    [SerializeField]
    private Image glowImage;

    [SerializeField]
    private Image outlineImage;

    [SerializeField]
    private Color glowColor = new Color(1f, 0.95f, 0.7f, 1f);

    [SerializeField]
    [Range(1.05f, 1.1f)]
    [Tooltip("Peak piece scale at match contact, relative to captured rest scale.")]
    private float impactScale = 1.08f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Duration of the contact click pulse.")]
    private float impactDuration = 0.12f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Full glow lifetime: appear, expand slightly, fade out.")]
    private float glowDuration = 0.22f;

    [SerializeField]
    [Min(0.5f)]
    [Tooltip("Glow footprint relative to the cell. 1 matches the block/target silhouette.")]
    private float glowScale = 1f;

    [SerializeField]
    [Range(0.2f, 1f)]
    [Tooltip("Peak glow opacity.")]
    private float glowPeakAlpha = 0.85f;

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("Block and target shrink/fade after the contact pulse.")]
    private float dissolveDuration = 0.1f;

    private RectTransform cachedRect;
    private bool visualsReady;

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
        EnsureVisuals();
    }

    public IEnumerator Play(ShapeType shapeType, Block block, Target target)
    {
        EnsureVisuals();
        Sprite sprite = ShapeVisuals.SpriteFor(
            shapeType,
            ShapeVisuals.First(theme != null ? theme.matchSquareGlow : null, squareGlow),
            ShapeVisuals.First(theme != null ? theme.matchCircleGlow : null, circleGlow),
            ShapeVisuals.First(theme != null ? theme.matchTriangleGlow : null, triangleGlow),
            ShapeVisuals.First(theme != null ? theme.matchDiamondGlow : null, diamondGlow),
            ShapeVisuals.First(theme != null ? theme.matchHexagonGlow : null, hexagonGlow),
            ShapeVisuals.First(theme != null ? theme.matchStarGlow : null, starGlow));
        ApplySprite(glowImage, sprite);
        ApplySprite(outlineImage, sprite);
        SetGlow(glowScale * 0.94f, 0f);

        if (block != null)
        {
            block.BeginMatchPresentation();
            block.SetMatchPresentation(1f, 1f);
        }

        if (target != null)
        {
            target.BeginMatchPresentation();
            target.SetMatchPresentation(1f, 1f);
        }

        float contactDuration = Mathf.Max(impactDuration, glowDuration);
        float elapsed = 0f;
        while (elapsed < contactDuration)
        {
            elapsed += Time.deltaTime;
            ApplyImpact(block, target, elapsed);
            EvaluateGlow(elapsed / glowDuration, out float glowSize, out float glowAlpha);
            SetGlow(glowSize, glowAlpha);
            yield return null;
        }

        ApplyImpact(block, target, impactDuration);
        SetGlow(glowScale, 0f);

        elapsed = 0f;
        while (elapsed < dissolveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dissolveDuration));
            float pieceScale = Mathf.LerpUnclamped(1f, 0f, t);
            float pieceAlpha = Mathf.LerpUnclamped(1f, 0f, t);
            if (block != null)
            {
                block.SetMatchPresentation(pieceScale, pieceAlpha);
            }

            if (target != null)
            {
                target.SetMatchPresentation(pieceScale, pieceAlpha);
            }

            yield return null;
        }

        if (block != null)
        {
            block.CompleteMatchPresentation();
        }

        if (target != null)
        {
            target.CompleteMatchPresentation();
        }

        SetGlow(0f, 0f);
    }

    private void ApplyImpact(Block block, Target target, float elapsed)
    {
        float pieceScale = ImpactMultiplier(elapsed, impactDuration, impactScale);
        if (block != null)
        {
            block.SetMatchPresentation(pieceScale, 1f);
        }

        if (target != null)
        {
            target.SetMatchPresentation(pieceScale, 1f);
        }
    }

    private void EvaluateGlow(float t, out float scale, out float alpha)
    {
        t = Mathf.Clamp01(t);
        if (t < 0.28f)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / 0.28f);
            scale = Mathf.LerpUnclamped(0.94f, 1f, u) * glowScale;
            alpha = Mathf.LerpUnclamped(0f, glowPeakAlpha, u);
            return;
        }

        if (t < 0.48f)
        {
            float u = Mathf.SmoothStep(0f, 1f, (t - 0.28f) / 0.2f);
            scale = Mathf.LerpUnclamped(1f, 1.04f, u) * glowScale;
            alpha = glowPeakAlpha;
            return;
        }

        float fade = Mathf.SmoothStep(0f, 1f, (t - 0.48f) / 0.52f);
        scale = Mathf.LerpUnclamped(1.04f, 1f, fade) * glowScale;
        alpha = Mathf.LerpUnclamped(glowPeakAlpha, 0f, fade);
    }

    private static float ImpactMultiplier(float elapsed, float duration, float peak)
    {
        if (duration <= 0f)
        {
            return 1f;
        }

        float t = Mathf.Clamp01(elapsed / duration);
        const float rise = 0.45f;
        if (t < rise)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / rise);
            return Mathf.LerpUnclamped(1f, peak, u);
        }

        float v = Mathf.SmoothStep(0f, 1f, (t - rise) / (1f - rise));
        return Mathf.LerpUnclamped(peak, 1f, v);
    }

    private void SetGlow(float scale, float alpha)
    {
        SetImage(glowImage, scale, alpha * 0.7f);
        SetImage(outlineImage, scale, alpha);
    }

    private void SetImage(Image image, float scale, float alpha)
    {
        if (image == null)
        {
            return;
        }

        image.rectTransform.localScale = Vector3.one * scale;
        Color color = glowColor;
        color.a = glowColor.a * Mathf.Clamp01(alpha);
        image.color = color;
        image.enabled = alpha > 0.001f;
        image.raycastTarget = false;
    }

    private void EnsureVisuals()
    {
        if (visualsReady)
        {
            return;
        }

        if (cachedRect == null)
        {
            cachedRect = (RectTransform)transform;
        }

        if (glowImage == null)
        {
            glowImage = CreateChildImage("Glow");
        }

        if (outlineImage == null)
        {
            outlineImage = CreateChildImage("ShapeOutline");
        }

        glowImage.raycastTarget = false;
        outlineImage.raycastTarget = false;
        visualsReady = true;
    }

    private Image CreateChildImage(string childName)
    {
        GameObject child = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = child.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void ApplySprite(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.preserveAspect = true;
        image.enabled = sprite != null;
        image.raycastTarget = false;
    }
}
