using UnityEngine;

/// <summary>
/// Scene-scoped one-shot gameplay audio. Fire-and-forget; never waits or drives gameplay.
/// </summary>
[DisallowMultipleComponent]
public class AudioFeedback : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField]
    [Tooltip("Played once when a drag successfully begins.")]
    private AudioClip dragStartClip;

    [SerializeField]
    [Tooltip("Played once per actual cell hop.")]
    private AudioClip hopClip;

    [SerializeField]
    [Tooltip("Played when matching nest-entry animation begins.")]
    private AudioClip nestEntryClip;

    [SerializeField]
    [Tooltip("Played when the match/merge effect begins.")]
    private AudioClip matchClip;

    [SerializeField]
    [Tooltip("Played once when the level is completed.")]
    private AudioClip levelCompleteClip;

    [SerializeField]
    [Tooltip("Played once when the session timer reaches 0:00.")]
    private AudioClip timeUpClip;

    [Header("Volumes")]
    [SerializeField]
    [Range(0f, 1f)]
    private float masterVolume = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float dragStartVolume = 0.35f;

    [SerializeField]
    [Range(0f, 1f)]
    private float hopVolume = 0.25f;

    [SerializeField]
    [Range(0f, 1f)]
    private float nestEntryVolume = 0.45f;

    [SerializeField]
    [Range(0f, 1f)]
    private float matchVolume = 0.6f;

    [SerializeField]
    [Range(0f, 1f)]
    private float levelCompleteVolume = 0.7f;

    [SerializeField]
    [Range(0f, 1f)]
    private float timeUpVolume = 0.7f;

    [Header("Hop Pitch")]
    [SerializeField]
    [Tooltip("Minimum pitch for hop one-shots.")]
    private float hopPitchMin = 0.96f;

    [SerializeField]
    [Tooltip("Maximum pitch for hop one-shots.")]
    private float hopPitchMax = 1.04f;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.dopplerLevel = 0f;
    }

    public void PlayDragStart()
    {
        PlayOneShot(dragStartClip, dragStartVolume, 1f);
    }

    public void PlayHop()
    {
        float pitch = Random.Range(hopPitchMin, hopPitchMax);
        PlayOneShot(hopClip, hopVolume, pitch);
    }

    public void PlayNestEntry()
    {
        PlayOneShot(nestEntryClip, nestEntryVolume, 1f);
    }

    public void PlayMatch()
    {
        PlayOneShot(matchClip, matchVolume, 1f);
    }

    public void PlayLevelComplete()
    {
        PlayOneShot(levelCompleteClip, levelCompleteVolume, 1f);
    }

    public void PlayTimeUp()
    {
        PlayOneShot(timeUpClip, timeUpVolume, 1f);
    }

    private void PlayOneShot(AudioClip clip, float volume, float pitch)
    {
        if (clip == null || audioSource == null)
        {
            return;
        }

        float previousPitch = audioSource.pitch;
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, masterVolume * volume);
        audioSource.pitch = previousPitch;
    }
}
