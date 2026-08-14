using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only HUD. Level number, countdown, pause, and time-up.
/// Does not own level lifecycle, movement, occupancy, or input.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button pauseButton;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private GameObject timeUpPanel;
    [SerializeField] private Button timeUpRestartButton;

    private int lastDisplayedIndex = int.MinValue;
    private int lastDisplayedSeconds = int.MinValue;
    private LevelManager.SessionState lastSession = (LevelManager.SessionState)(-1);
    private Color timerNormalColor = new Color(0.6313726f, 0.6156863f, 0.9411765f, 1f);
    private Color timerWarningColor = new Color(0.95f, 0.55f, 0.55f, 1f);
    private bool builtOverlays;

    private void Awake()
    {
        BindButton(restartButton, OnRestartClicked);
        EnsureSessionUi();
        BindButton(pauseButton, OnPauseClicked);
        BindButton(resumeButton, OnResumeClicked);
        BindButton(pauseRestartButton, OnRestartClicked);
        BindButton(timeUpRestartButton, OnRestartClicked);
        if (timerText != null)
        {
            timerNormalColor = timerText.color;
        }
        else if (levelText != null)
        {
            timerNormalColor = levelText.color;
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        UnbindButton(restartButton, OnRestartClicked);
        UnbindButton(pauseButton, OnPauseClicked);
        UnbindButton(resumeButton, OnResumeClicked);
        UnbindButton(pauseRestartButton, OnRestartClicked);
        UnbindButton(timeUpRestartButton, OnRestartClicked);
    }

    private void Update()
    {
        if (levelManager == null)
        {
            return;
        }

        if (levelManager.CurrentLevelIndex != lastDisplayedIndex)
        {
            RefreshLevelText();
        }

        RefreshTimerText();
        RefreshSessionUi();
    }

    public void Refresh()
    {
        RefreshLevelText();
        lastDisplayedSeconds = int.MinValue;
        lastSession = (LevelManager.SessionState)(-1);
        RefreshTimerText();
        RefreshSessionUi();
    }

    private void RefreshLevelText()
    {
        if (levelManager == null || levelText == null)
        {
            return;
        }

        lastDisplayedIndex = levelManager.CurrentLevelIndex;
        levelText.text = $"LEVEL {lastDisplayedIndex + 1}";
    }

    private void RefreshTimerText()
    {
        if (levelManager == null || timerText == null)
        {
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(levelManager.RemainingSeconds));
        if (seconds == lastDisplayedSeconds)
        {
            return;
        }

        lastDisplayedSeconds = seconds;
        int minutes = seconds / 60;
        int remainder = seconds % 60;
        timerText.text = $"{minutes}:{remainder:00}";
        timerText.color = seconds <= 10 ? timerWarningColor : timerNormalColor;
    }

    private void RefreshSessionUi()
    {
        if (levelManager == null)
        {
            return;
        }

        LevelManager.SessionState session = levelManager.Session;
        if (session == lastSession)
        {
            return;
        }

        lastSession = session;
        bool playing = session == LevelManager.SessionState.Playing;
        bool paused = session == LevelManager.SessionState.Paused;
        bool expired = session == LevelManager.SessionState.TimeExpired;

        if (pausePanel != null)
        {
            pausePanel.SetActive(paused);
        }

        if (timeUpPanel != null)
        {
            timeUpPanel.SetActive(expired);
        }

        if (pauseButton != null)
        {
            pauseButton.interactable = playing;
        }
    }

    private void OnPauseClicked()
    {
        if (levelManager != null)
        {
            levelManager.PauseSession();
        }
    }

    private void OnResumeClicked()
    {
        if (levelManager != null)
        {
            levelManager.ResumeSession();
        }
    }

    private void OnRestartClicked()
    {
        if (levelManager == null)
        {
            return;
        }

        levelManager.RestartLevel();
        Refresh();
    }

    private void EnsureSessionUi()
    {
        if (builtOverlays)
        {
            return;
        }

        builtOverlays = true;
        Canvas canvas = GetComponentInParent<Canvas>();
        TMP_FontAsset font = levelText != null ? levelText.font : null;

        if (timerText == null)
        {
            timerText = CreateHudText("TimerText", transform, new Vector2(0.3f, 0f), new Vector2(0.7f, 0.42f), 48, font);
        }

        if (pauseButton == null)
        {
            pauseButton = CreateHudButton("PauseButton", transform, new Vector2(1f, 1f), new Vector2(-80f, -90f), "II", font);
        }

        if (canvas == null)
        {
            return;
        }

        Transform overlayRoot = canvas.transform;
        if (pausePanel == null)
        {
            pausePanel = CreateOverlayPanel(overlayRoot, "PausePanel", "PAUSED", font, out resumeButton, "RESUME", out pauseRestartButton, "RESTART");
            pausePanel.SetActive(false);
        }

        if (timeUpPanel == null)
        {
            timeUpPanel = CreateOverlayPanel(overlayRoot, "TimeUpPanel", "TIME UP", font, out _, null, out timeUpRestartButton, "RESTART");
            timeUpPanel.SetActive(false);
        }
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void UnbindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    private TMP_Text CreateHudText(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TMP_FontAsset font)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = timerNormalColor;
        text.raycastTarget = false;
        text.text = "1:30";
        return text;
    }

    private Button CreateHudButton(string objectName, Transform parent, Vector2 anchor, Vector2 anchoredPos, string label, TMP_FontAsset font)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(96f, 96f);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.35f, 0.3f, 0.55f, 0.9f);
        Button button = go.GetComponent<Button>();
        CreateLabel(go.transform, label, font, 36f);
        return button;
    }

    private GameObject CreateOverlayPanel(
        Transform parent,
        string objectName,
        string title,
        TMP_FontAsset font,
        out Button primaryButton,
        string primaryLabel,
        out Button secondaryButton,
        string secondaryLabel)
    {
        GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.14f, 0.72f);
        panel.GetComponent<Image>().raycastTarget = true;

        CreateCenteredText(panel.transform, title, font, 72f, new Vector2(0f, 80f));

        primaryButton = null;
        if (!string.IsNullOrEmpty(primaryLabel))
        {
            primaryButton = CreateOverlayButton(panel.transform, primaryLabel, font, new Vector2(0f, -20f));
        }

        secondaryButton = CreateOverlayButton(panel.transform, secondaryLabel, font, new Vector2(0f, -120f));
        return panel;
    }

    private void CreateCenteredText(Transform parent, string value, TMP_FontAsset font, float size, Vector2 offset)
    {
        GameObject go = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.1f, 0.5f);
        rect.anchorMax = new Vector2(0.9f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 120f);
        rect.anchoredPosition = offset;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = value;
    }

    private Button CreateOverlayButton(Transform parent, string label, TMP_FontAsset font, Vector2 offset)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(360f, 80f);
        rect.anchoredPosition = offset;
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.45f, 0.4f, 0.75f, 1f);
        Button button = go.GetComponent<Button>();
        CreateLabel(go.transform, label, font, 36f);
        return button;
    }

    private static void CreateLabel(Transform parent, string label, TMP_FontAsset font, float size)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TMP_Text text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = label;
    }
}
