using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only board panel. Does not affect grid math, occupancy, or input.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class BoardVisual : MonoBehaviour
{
    private const string BackgroundName = "BoardBackground";
    private const string RuntimeGridName = "RuntimeGrid";

    [SerializeField]
    private Sprite panelSprite;

    [SerializeField]
    private Color panelColor = new Color(1f, 1f, 1f, 1f);

    [SerializeField]
    private Vector2 panelPadding = new Vector2(12f, 12f);

    [SerializeField]
    private bool softenRuntimeGrid = true;

    [SerializeField]
    private Color runtimeGridColor = new Color(1f, 1f, 1f, 0.08f);

    private RectTransform backgroundRect;
    private Image backgroundImage;

    private void OnEnable()
    {
        EnsureBackground();
        ApplyPresentation();
    }

    private void LateUpdate()
    {
        EnsureBackground();
        ApplyPresentation();
    }

    private void EnsureBackground()
    {
        if (backgroundRect != null && backgroundImage != null)
        {
            return;
        }

        Transform existing = transform.Find(BackgroundName);
        GameObject backgroundObject;
        if (existing != null)
        {
            backgroundObject = existing.gameObject;
        }
        else
        {
            backgroundObject = new GameObject(BackgroundName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backgroundObject.layer = gameObject.layer;
            backgroundObject.transform.SetParent(transform, false);
        }

        backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundImage = backgroundObject.GetComponent<Image>();
    }

    private void ApplyPresentation()
    {
        if (backgroundRect == null || backgroundImage == null)
        {
            return;
        }

        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = new Vector2(-panelPadding.x, -panelPadding.y);
        backgroundRect.offsetMax = new Vector2(panelPadding.x, panelPadding.y);
        backgroundRect.localScale = Vector3.one;
        backgroundRect.localRotation = Quaternion.identity;

        backgroundImage.sprite = panelSprite;
        backgroundImage.color = panelColor;
        backgroundImage.type = panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        backgroundImage.raycastTarget = false;
        backgroundImage.maskable = true;

        backgroundRect.SetSiblingIndex(0);

        Transform grid = transform.Find(RuntimeGridName);
        if (grid != null)
        {
            grid.SetSiblingIndex(1);
            if (softenRuntimeGrid)
            {
                Image[] lines = grid.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i].color = runtimeGridColor;
                    lines[i].raycastTarget = false;
                }
            }
        }
    }
}
