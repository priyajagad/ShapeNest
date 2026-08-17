using StarterKit.UIKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shape Nest pause/settings screen. Session state stays on LevelManager.
/// Sound and haptics are session-only toggles on the existing feedback components.
/// </summary>
public class ShapeNestSettingsScreen : UIScreenBase
{
    [SerializeField] private LevelManager levelManager;
    [SerializeField] private AudioFeedback audioFeedback;
    [SerializeField] private HapticFeedback hapticFeedback;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button hapticsButton;
    [SerializeField] private TMP_Text soundLabel;
    [SerializeField] private TMP_Text hapticsLabel;
    [SerializeField] private ResultScreenIntro intro;

    public override void OnAwake()
    {
        base.OnAwake();
        Bind(resumeButton, OnResumeClicked);
        Bind(restartButton, OnRestartClicked);
        Bind(soundButton, OnSoundClicked);
        Bind(hapticsButton, OnHapticsClicked);
    }

    public override void OnScreenShowAnimationStarted()
    {
        base.OnScreenShowAnimationStarted();
        RefreshToggleLabels();
        if (intro != null)
        {
            intro.Play();
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
        if (levelManager != null)
        {
            levelManager.RestartLevel();
        }
    }

    private void OnSoundClicked()
    {
        if (audioFeedback != null)
        {
            audioFeedback.SoundEnabled = !audioFeedback.SoundEnabled;
        }

        RefreshToggleLabels();
    }

    private void OnHapticsClicked()
    {
        if (hapticFeedback != null)
        {
            hapticFeedback.Enabled = !hapticFeedback.Enabled;
        }

        RefreshToggleLabels();
    }

    private void RefreshToggleLabels()
    {
        if (soundLabel != null)
        {
            bool on = audioFeedback == null || audioFeedback.SoundEnabled;
            soundLabel.text = on ? "SOUND ON" : "SOUND OFF";
        }

        if (hapticsLabel != null)
        {
            bool on = hapticFeedback == null || hapticFeedback.Enabled;
            hapticsLabel.text = on ? "HAPTICS ON" : "HAPTICS OFF";
        }
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
