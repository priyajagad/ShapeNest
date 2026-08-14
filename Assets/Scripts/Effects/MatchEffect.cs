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
    private Image glowImage;

    [SerializeField]
    private Image outlineImage;

    [SerializeField]
    private Color glowColor = new Color(1f, 0.95f, 0.7f, 1f);

    [SerializeField]
    [Min(0f)]
    private float sitDuration = 0.03f;

    [SerializeField]
    [Min(0.01f)]
    private float glowAppearDuration = 0.05f;

    [SerializeField]
    [Min(0.01f)]
    private float glowExpandDuration = 0.1f;

    [SerializeField]
    [Min(0.01f)]
    private float pulseDuration = 0.1f;

    [SerializeField]
    [Min(0.01f)]
    private float shrinkDuration = 0.1f;

    [SerializeField]
    [Min(0.01f)]
    private float fadeDuration = 0.1f;

    [SerializeField]
    private float glowStartScale = 0.85f;

    [SerializeField]
    private float glowPeakScale = 1.12f;

    [SerializeField]
    private float piecePulseScale = 1.06f;

    [SerializeField]
    private float pieceShrinkScale = 0.85f;

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
        Sprite sprite = ShapeVisuals.SpriteFor(shapeType, squareGlow, circleGlow, triangleGlow);
        ApplySprite(glowImage, sprite);
        ApplySprite(outlineImage, sprite);
        SetGlow(glowStartScale, 0f);

        if (block != null)
        {
            block.BeginMatchPresentation();
        }

        if (target != null)
        {
            target.BeginMatchPresentation();
        }

        if (sitDuration > 0f)
        {
            yield return Wait(sitDuration);
        }

        yield return AnimateGlow(glowStartScale, glowStartScale, 0f, 1f, glowAppearDuration, true);
        yield return AnimateGlow(glowStartScale, glowPeakScale, 1f, 1f, glowExpandDuration, true);

        float halfPulse = pulseDuration * 0.5f;
        yield return AnimatePiecesAndGlow(
            block,
            target,
            1f,
            piecePulseScale,
            1f,
            1f,
            glowPeakScale,
            glowPeakScale + 0.03f,
            1f,
            1f,
            halfPulse,
            false);
        yield return AnimatePiecesAndGlow(
            block,
            target,
            piecePulseScale,
            pieceShrinkScale,
            1f,
            1f,
            glowPeakScale + 0.03f,
            1f,
            1f,
            1f,
            halfPulse,
            true);

        float dissolve = Mathf.Max(shrinkDuration, fadeDuration);
        yield return AnimatePiecesAndGlow(
            block,
            target,
            pieceShrinkScale,
            0f,
            1f,
            0f,
            1f,
            0f,
            1f,
            0f,
            dissolve,
            true);

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

    private IEnumerator AnimateGlow(float fromScale, float toScale, float fromAlpha, float toAlpha, float duration, bool easeOut)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Ease(Mathf.Clamp01(elapsed / duration), easeOut);
            SetGlow(Mathf.LerpUnclamped(fromScale, toScale, t), Mathf.LerpUnclamped(fromAlpha, toAlpha, t));
            yield return null;
        }

        SetGlow(toScale, toAlpha);
    }

    private IEnumerator AnimatePiecesAndGlow(
        Block block,
        Target target,
        float fromPieceScale,
        float toPieceScale,
        float fromPieceAlpha,
        float toPieceAlpha,
        float fromGlowScale,
        float toGlowScale,
        float fromGlowAlpha,
        float toGlowAlpha,
        float duration,
        bool easeOut)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Ease(Mathf.Clamp01(elapsed / duration), easeOut);
            float pieceScale = Mathf.LerpUnclamped(fromPieceScale, toPieceScale, t);
            float pieceAlpha = Mathf.LerpUnclamped(fromPieceAlpha, toPieceAlpha, t);
            if (block != null)
            {
                block.SetMatchPresentation(pieceScale, pieceAlpha);
            }

            if (target != null)
            {
                target.SetMatchPresentation(pieceScale, pieceAlpha);
            }

            SetGlow(
                Mathf.LerpUnclamped(fromGlowScale, toGlowScale, t),
                Mathf.LerpUnclamped(fromGlowAlpha, toGlowAlpha, t));
            yield return null;
        }

        if (block != null)
        {
            block.SetMatchPresentation(toPieceScale, toPieceAlpha);
        }

        if (target != null)
        {
            target.SetMatchPresentation(toPieceScale, toPieceAlpha);
        }

        SetGlow(toGlowScale, toGlowAlpha);
    }

    private void SetGlow(float scale, float alpha)
    {
        SetImage(glowImage, scale, alpha * 0.65f, 1.18f);
        SetImage(outlineImage, scale, alpha, 1f);
    }

    private void SetImage(Image image, float scale, float alpha, float extraScale)
    {
        if (image == null)
        {
            return;
        }

        image.rectTransform.localScale = Vector3.one * (scale * extraScale);
        Color color = glowColor;
        color.a = glowColor.a * Mathf.Clamp01(alpha);
        image.color = color;
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
    }

    private static float Ease(float t, bool easeOut)
    {
        return easeOut ? 1f - ((1f - t) * (1f - t)) : t * t;
    }

    private static IEnumerator Wait(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
