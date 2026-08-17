using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Subtle press scale for existing UI buttons. Unscaled time. No new visuals.
/// </summary>
public class UiPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] private float pressedScale = 0.96f;
    [SerializeField] private float duration = 0.06f;

    private RectTransform rect;
    private Vector3 restScale = Vector3.one;
    private Coroutine routine;
    private bool captured;

    private void Awake()
    {
        rect = transform as RectTransform;
        CaptureRest();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        AnimateTo(restScale * pressedScale);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AnimateTo(restScale);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(restScale);
    }

    private void CaptureRest()
    {
        if (captured || rect == null)
        {
            return;
        }

        restScale = rect.localScale;
        if (restScale.sqrMagnitude < 0.0001f)
        {
            restScale = Vector3.one;
        }

        captured = true;
    }

    private void AnimateTo(Vector3 target)
    {
        CaptureRest();
        if (rect == null)
        {
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
        }

        routine = StartCoroutine(ScaleRoutine(rect.localScale, target));
    }

    private IEnumerator ScaleRoutine(Vector3 from, Vector3 to)
    {
        if (duration <= 0f)
        {
            rect.localScale = to;
            routine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = 1f - ((1f - Mathf.Clamp01(elapsed / duration)) * (1f - Mathf.Clamp01(elapsed / duration)));
            rect.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        rect.localScale = to;
        routine = null;
    }
}
