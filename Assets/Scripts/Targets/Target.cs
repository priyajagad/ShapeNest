using System.Collections;
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
    private Sprite squareSprite;

    [SerializeField]
    private Sprite circleSprite;

    [SerializeField]
    private Sprite triangleSprite;

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

    public ShapeType ShapeType => shapeType;
    public Vector2Int GridPosition => gridPosition;

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
        RefreshVisual();
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
        RefreshVisual();
        CaptureRestPose();

        if (boardManager == null)
        {
            return;
        }

        RectTransform.anchoredPosition = boardManager.GridToLocal(gridPosition);
        isRegistered = boardManager.TryRegisterTarget(this);
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
        if (image == null)
        {
            return;
        }

        Color color = restColor;
        color.a = restColor.a * Mathf.Clamp01(alpha);
        image.color = color;
        image.enabled = alpha > 0.001f;
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
    }

    public void ResetMatchPresentation()
    {
        visualState = VisualState.Normal;
        StopReadyRoutine();
        isReadyFeedbackActive = false;
        CacheImage();
        if (image != null)
        {
            image.enabled = true;
        }

        ApplyRestVisuals();
    }

    public void RefreshVisual()
    {
        CacheImage();
        if (image == null)
        {
            return;
        }

        Sprite sprite = ShapeVisuals.SpriteFor(shapeType, squareSprite, circleSprite, triangleSprite);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        if (!isReadyFeedbackActive && visualState == VisualState.Normal)
        {
            image.color = hasRestPose ? restColor : Color.white;
        }
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
        RefreshVisual();
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
