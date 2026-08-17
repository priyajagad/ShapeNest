using StarterKit.Utilities;
using UnityEngine;

namespace StarterKit.UI
{
    /// <summary>
    /// Insets a RectTransform to Screen.safeArea. No per-frame polling.
    /// Intended for UI Parent roots, not the gameplay board canvas.
    /// </summary>
    public class SafeAreaHandler : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private int lastWidth;
        private int lastHeight;
        private ScreenOrientation lastOrientation;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea(true);
            this.DelayedInvokeUnscaled(() => ApplySafeArea(true), 0.1f);
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplySafeArea(false);
        }

        [ContextMenu("Adjust Canvas")]
        public void AdjustCanvas()
        {
            ApplySafeArea(true);
        }

        private void ApplySafeArea(bool force)
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
                if (rectTransform == null)
                {
                    return;
                }
            }

            int width = Screen.width;
            int height = Screen.height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            Rect safeRect = Screen.safeArea;
            ScreenOrientation screenOrientation = Screen.orientation;
            if (!force
                && safeRect == lastSafeArea
                && width == lastWidth
                && height == lastHeight
                && screenOrientation == lastOrientation)
            {
                return;
            }

            lastSafeArea = safeRect;
            lastWidth = width;
            lastHeight = height;
            lastOrientation = screenOrientation;

            Vector2 minAnchor = safeRect.position;
            Vector2 maxAnchor = minAnchor + safeRect.size;

            if (screenOrientation == ScreenOrientation.LandscapeLeft
                || screenOrientation == ScreenOrientation.LandscapeRight)
            {
                minAnchor.x /= width;
                minAnchor.y = 0f;
                maxAnchor.x /= width;
                maxAnchor.y = 1f;
            }
            else
            {
                minAnchor.x = 0f;
                minAnchor.y /= height;
                maxAnchor.x = 1f;
                maxAnchor.y /= height;
            }

            rectTransform.anchorMin = minAnchor;
            rectTransform.anchorMax = maxAnchor;
        }
    }
}
