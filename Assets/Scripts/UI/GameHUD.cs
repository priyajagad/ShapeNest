using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presentation-only HUD. Level number, countdown, and pause.
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

    [Header("Timer colors")]
    [SerializeField] private Color timerWarningColor = new Color(0.86f, 0.58f, 0.72f, 1f);
    [SerializeField] private Color timerUrgentColor = new Color(0.9f, 0.48f, 0.58f, 1f);

    private int lastDisplayedIndex = int.MinValue;
    private int lastDisplayedSeconds = int.MinValue;
    private LevelManager.SessionState lastSession = (LevelManager.SessionState)(-1);
    private Color timerNormalColor = Color.white;
    private bool builtOverlays;

    private CanvasGroup levelGroup;
    private Vector3 levelRestScale = Vector3.one;
    private Coroutine levelIntroRoutine;

    private Vector3 timerRestScale = Vector3.one;
    private Coroutine timerPulseRoutine;

    private OverlayView pauseOverlay;

    private void Awake()
    {
        BindButton(restartButton, OnRestartClicked);
        EnsureSessionUi();
        BindButton(pauseButton, OnPauseClicked);
        BindButton(resumeButton, OnResumeClicked);
        BindButton(pauseRestartButton, OnRestartClicked);
        CacheHudPresentation();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Start()
    {
        if (timerText != null)
        {
            timerNormalColor = timerText.color;
        }
    }

    private void OnDestroy()
    {
        UnbindButton(restartButton, OnRestartClicked);
        UnbindButton(pauseButton, OnPauseClicked);
        UnbindButton(resumeButton, OnResumeClicked);
        UnbindButton(pauseRestartButton, OnRestartClicked);
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
        lastDisplayedIndex = int.MinValue;
        lastDisplayedSeconds = int.MinValue;
        lastSession = (LevelManager.SessionState)(-1);
        RefreshLevelText();
        RefreshTimerText();
        RefreshSessionUi();
    }

    private void RefreshLevelText()
    {
        if (levelManager == null || levelText == null)
        {
            return;
        }

        int index = levelManager.CurrentLevelIndex;
        bool changed = index != lastDisplayedIndex;
        lastDisplayedIndex = index;
        levelText.text = $"LEVEL {index + 1}";
        if (changed)
        {
            PlayLevelIntro();
        }
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

        if (seconds <= 3)
        {
            timerText.color = timerUrgentColor;
            PulseTimer(1.07f, 0.14f);
        }
        else if (seconds <= 10)
        {
            timerText.color = timerWarningColor;
            PulseTimer(1.045f, 0.12f);
        }
        else
        {
            timerText.color = timerNormalColor;
            if (timerText.rectTransform.localScale != timerRestScale)
            {
                timerText.rectTransform.localScale = timerRestScale;
            }
        }
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

        if (pauseButton != null)
        {
            pauseButton.interactable = playing;
        }

        if (paused)
        {
            return;
        }

        HideOverlayImmediate(pauseOverlay);
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
        if (levelText != null)
        {
            levelText.raycastTarget = false;
        }

        if (pausePanel != null)
        {
            pauseOverlay = CaptureOverlay(pausePanel);
        }

        HideOverlayImmediate(pauseOverlay);
    }

    private void CacheHudPresentation()
    {
        if (levelText != null)
        {
            levelRestScale = levelText.rectTransform.localScale;
            if (levelRestScale.sqrMagnitude < 0.0001f)
            {
                levelRestScale = Vector3.one;
            }

            levelGroup = levelText.GetComponent<CanvasGroup>();
            if (levelGroup == null)
            {
                levelGroup = levelText.gameObject.AddComponent<CanvasGroup>();
            }

            levelGroup.blocksRaycasts = false;
        }

        if (timerText != null)
        {
            timerRestScale = timerText.rectTransform.localScale;
            if (timerRestScale.sqrMagnitude < 0.0001f)
            {
                timerRestScale = Vector3.one;
            }

            timerNormalColor = timerText.color;
        }
    }

    private void PlayLevelIntro()
    {
        if (levelText == null)
        {
            return;
        }

        if (levelIntroRoutine != null)
        {
            StopCoroutine(levelIntroRoutine);
        }

        levelIntroRoutine = StartCoroutine(LevelIntroRoutine());
    }

    private IEnumerator LevelIntroRoutine()
    {
        RectTransform rect = levelText.rectTransform;
        float duration = 0.18f;
        float elapsed = 0f;
        if (levelGroup != null)
        {
            levelGroup.alpha = 0f;
        }

        rect.localScale = levelRestScale * 0.96f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (levelGroup != null)
            {
                levelGroup.alpha = t;
            }

            rect.localScale = Vector3.LerpUnclamped(levelRestScale * 0.96f, levelRestScale, t);
            yield return null;
        }

        if (levelGroup != null)
        {
            levelGroup.alpha = 1f;
        }

        rect.localScale = levelRestScale;
        levelIntroRoutine = null;
    }

    private void PulseTimer(float peakScale, float duration)
    {
        if (timerText == null)
        {
            return;
        }

        if (timerPulseRoutine != null)
        {
            StopCoroutine(timerPulseRoutine);
        }

        timerPulseRoutine = StartCoroutine(TimerPulseRoutine(peakScale, duration));
    }

    private IEnumerator TimerPulseRoutine(float peakScale, float duration)
    {
        RectTransform rect = timerText.rectTransform;
        float elapsed = 0f;
        Vector3 peak = timerRestScale * peakScale;
        float rise = duration * 0.4f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t;
            Vector3 from;
            Vector3 to;
            if (elapsed <= rise)
            {
                t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / rise));
                from = timerRestScale;
                to = peak;
            }
            else
            {
                t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - rise) / (duration - rise)));
                from = peak;
                to = timerRestScale;
            }

            rect.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        rect.localScale = timerRestScale;
        timerPulseRoutine = null;
    }

    private void ShowOverlay(OverlayView overlay, float duration, bool impactTitle)
    {
        if (overlay == null || overlay.root == null)
        {
            return;
        }

        if (overlay.intro != null)
        {
            StopCoroutine(overlay.intro);
        }

        overlay.root.SetActive(true);
        overlay.intro = StartCoroutine(OverlayIntroRoutine(overlay, duration, impactTitle));
    }

    private void HideOverlayImmediate(OverlayView overlay)
    {
        if (overlay == null || overlay.root == null)
        {
            return;
        }

        if (overlay.intro != null)
        {
            StopCoroutine(overlay.intro);
            overlay.intro = null;
        }

        if (overlay.group != null)
        {
            overlay.group.alpha = 0f;
        }

        overlay.root.SetActive(false);
    }

    private IEnumerator OverlayIntroRoutine(OverlayView overlay, float duration, bool impactTitle)
    {
        if (overlay.group != null)
        {
            overlay.group.alpha = 0f;
        }

        if (overlay.content != null)
        {
            overlay.content.localScale = Vector3.one * 0.94f;
        }

        if (overlay.title != null)
        {
            overlay.title.rectTransform.localScale = Vector3.one;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (overlay.group != null)
            {
                overlay.group.alpha = t;
            }

            if (overlay.content != null)
            {
                overlay.content.localScale = Vector3.LerpUnclamped(Vector3.one * 0.94f, Vector3.one, t);
            }

            if (impactTitle && overlay.title != null)
            {
                float bounce = t < 0.55f
                    ? Mathf.LerpUnclamped(0.92f, 1.06f, Mathf.SmoothStep(0f, 1f, t / 0.55f))
                    : Mathf.LerpUnclamped(1.06f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));
                overlay.title.rectTransform.localScale = Vector3.one * bounce;
            }

            yield return null;
        }

        if (overlay.group != null)
        {
            overlay.group.alpha = 1f;
        }

        if (overlay.content != null)
        {
            overlay.content.localScale = Vector3.one;
        }

        if (overlay.title != null)
        {
            overlay.title.rectTransform.localScale = Vector3.one;
        }

        overlay.intro = null;
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

    private static OverlayView CaptureOverlay(GameObject panel)
    {
        OverlayView view = new OverlayView { root = panel };
        if (panel == null)
        {
            return view;
        }

        view.group = panel.GetComponent<CanvasGroup>();
        if (view.group == null)
        {
            view.group = panel.AddComponent<CanvasGroup>();
        }
        Transform content = panel.transform.Find("Content");
        if (content != null)
        {
            view.content = content as RectTransform;
            Transform title = content.Find("Title");
            if (title != null)
            {
                view.title = title.GetComponent<TMP_Text>();
            }
        }

        return view;
    }

    private class OverlayView
    {
        public GameObject root;
        public CanvasGroup group;
        public RectTransform content;
        public TMP_Text title;
        public Coroutine intro;
    }
}
