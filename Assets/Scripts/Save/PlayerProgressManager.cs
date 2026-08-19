using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class PlayerProgressManager : MonoBehaviour
{
    private const string SaveFileName = "shape_nest_progress.json";
    private static PlayerProgressManager instance;

    [SerializeField]
    private PlayerProgress progress;

    public static PlayerProgressManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<PlayerProgressManager>();
                if (instance == null)
                {
                    GameObject managerObject = new GameObject(nameof(PlayerProgressManager));
                    instance = managerObject.AddComponent<PlayerProgressManager>();
                }
            }
            return instance;
        }
    }

    public int HighestUnlockedLevel => progress != null ? progress.highestUnlockedLevel : 0;
    public int HighestCompletedLevel => progress != null ? progress.highestCompletedLevel : -1;
    public int CurrentLevelIndex => progress != null ? progress.currentLevelIndex : 0;
    public bool HasCompletedAllLevels => progress != null && progress.hasCompletedAllLevels;
    public string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public bool IsLevelUnlocked(int levelIndex, int levelCount)
    {
        return levelIndex >= 0
            && levelIndex < levelCount
            && levelIndex <= HighestUnlockedLevel;
    }

    public bool IsLevelCompleted(int levelIndex)
    {
        return levelIndex >= 0 && levelIndex <= HighestCompletedLevel;
    }

    public int GetContinueLevelIndex(int levelCount)
    {
        if (levelCount <= 0)
        {
            Debug.LogWarning("PlayerProgressManager: Cannot choose a continue level because the database is empty.", this);
            return -1;
        }

        int continueIndex = HighestCompletedLevel + 1;
        if (continueIndex >= levelCount)
        {
            return 0;
        }

        if (!IsLevelUnlocked(continueIndex, levelCount))
        {
            Debug.LogWarning(
                $"PlayerProgressManager: Continue level {continueIndex} is locked. Falling back to level 0.",
                this);
            return 0;
        }

        return Mathf.Max(0, continueIndex);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    public void Load()
    {
        if (!File.Exists(SavePath))
        {
            progress = CreateDefaultProgress();
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            progress = JsonUtility.FromJson<PlayerProgress>(json);
            if (progress == null)
            {
                throw new System.Exception("JSON produced no progress data.");
            }

            NormalizeProgress();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"PlayerProgressManager: Could not load progress. Resetting save. {exception.Message}",
                this);
            progress = CreateDefaultProgress();
            Save();
        }
    }

    public void Save()
    {
        if (progress == null)
        {
            progress = CreateDefaultProgress();
        }

        try
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(SavePath, JsonUtility.ToJson(progress, true));
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"PlayerProgressManager: Could not save progress. {exception.Message}",
                this);
        }
    }

    public void ResetProgress()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"PlayerProgressManager: Could not delete progress save. {exception.Message}",
                this);
        }

        progress = CreateDefaultProgress();
        Save();
    }

    public void MarkLevelCompleted(int levelIndex, int levelCount = -1)
    {
        if (levelIndex < 0)
        {
            return;
        }

        EnsureProgress();
        progress.highestCompletedLevel = Mathf.Max(progress.highestCompletedLevel, levelIndex);

        int nextUnlocked = levelIndex + 1;
        if (levelCount > 0)
        {
            nextUnlocked = Mathf.Min(nextUnlocked, levelCount - 1);
            if (levelIndex >= levelCount - 1)
            {
                progress.hasCompletedAllLevels = true;
            }
        }

        progress.highestUnlockedLevel = Mathf.Max(progress.highestUnlockedLevel, nextUnlocked);
        Save();
    }

    public void SetCurrentLevel(int levelIndex)
    {
        if (levelIndex < 0)
        {
            return;
        }

        EnsureProgress();
        progress.currentLevelIndex = levelIndex;
        Save();
    }

    private void EnsureProgress()
    {
        if (progress == null)
        {
            progress = CreateDefaultProgress();
        }

        NormalizeProgress();
    }

    private void NormalizeProgress()
    {
        progress.highestUnlockedLevel = Mathf.Max(0, progress.highestUnlockedLevel);
        progress.highestCompletedLevel = Mathf.Max(-1, progress.highestCompletedLevel);
        progress.currentLevelIndex = Mathf.Max(0, progress.currentLevelIndex);
    }

    private static PlayerProgress CreateDefaultProgress()
    {
        return new PlayerProgress
        {
            highestUnlockedLevel = 0,
            highestCompletedLevel = -1,
            currentLevelIndex = 0,
            hasCompletedAllLevels = false
        };
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            Save();
        }
    }
}
