using System.Collections;
using System.Collections.Generic;
using StarterKit.UIKit;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Loads a LevelData asset onto the Board. Owns level lifecycle, session timer, and completion.
/// Does not own movement, occupancy, or matching.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public enum SessionState
    {
        Playing,
        Paused,
        Completed,
        TimeExpired
    }

    [SerializeField]
    private LevelData currentLevel;

    [SerializeField]
    private LevelDatabase levelDatabase;

    [SerializeField]
    private BoardManager boardManager;

    [SerializeField]
    private Block blockPrefab;

    [SerializeField]
    private Target targetPrefab;

    [SerializeField]
    private AudioFeedback audioFeedback;

    [SerializeField]
    private HapticFeedback hapticFeedback;

    [SerializeField]
    [Min(1f)]
    [Tooltip("Countdown duration in seconds for each level session.")]
    private float timeLimitSeconds = 90f;

    private readonly List<Block> spawnedBlocks = new List<Block>();
    private readonly List<Target> spawnedTargets = new List<Target>();
    private bool isLoading;
    private bool isLevelActive;
    private bool levelComplete;
    private int currentLevelIndex;
    private SessionState session = SessionState.Playing;
    private float remainingSeconds;
    private bool timerRunning;
    private bool timeUpSoundPlayed;
    private Coroutine pauseTimeFreezeRoutine;

    public int CurrentLevelIndex => currentLevelIndex;
    public SessionState Session => session;
    public float RemainingSeconds => remainingSeconds;
    public bool IsGameplayInputAllowed => session == SessionState.Playing;

    private void Awake()
    {
        if (audioFeedback == null)
        {
            audioFeedback = GetComponent<AudioFeedback>();
        }

        if (hapticFeedback == null)
        {
            hapticFeedback = GetComponent<HapticFeedback>();
        }

        SyncCurrentLevelIndex(currentLevel);
        remainingSeconds = timeLimitSeconds;
    }

    private void OnDisable()
    {
        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (levelDatabase != null && levelDatabase.Count > 0)
        {
            LoadLevel(0);
        }
        else
        {
            LoadLevel(currentLevel);
        }

        StartCoroutine(SyncSessionScreenWhenUiReady());
    }

    private void Update()
    {
        HandleBackButton();

        if (!timerRunning || session != SessionState.Playing)
        {
            return;
        }

        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds > 0f)
        {
            return;
        }

        remainingSeconds = 0f;
        ExpireTime();
    }

    [ContextMenu("Load Level 0")]
    private void LoadLevelZeroDebug()
    {
        LoadLevel(0);
    }

    public bool LoadLevel(int levelIndex)
    {
        if (isLoading)
        {
            return false;
        }

        if (levelDatabase == null)
        {
            Debug.LogError("LevelManager: LevelDatabase is not assigned.", this);
            return false;
        }

        LevelData level = levelDatabase.GetLevel(levelIndex);
        if (level == null)
        {
            Debug.LogError($"LevelManager: Could not load level at index {levelIndex}.", this);
            return false;
        }

        currentLevelIndex = levelIndex;
        LoadLevel(level);
        return true;
    }

    public bool HasNextLevel =>
        levelDatabase != null && currentLevelIndex + 1 < levelDatabase.Count;

    public bool LoadNextLevel()
    {
        if (!HasNextLevel)
        {
            Debug.Log("LevelManager: No further levels in the database.", this);
            return false;
        }

        return LoadLevel(currentLevelIndex + 1);
    }

    [ContextMenu("Restart Level")]
    public void RestartLevel()
    {
        if (currentLevel == null)
        {
            Debug.LogError("LevelManager: Cannot restart because Current Level is not assigned.", this);
            return;
        }

        LoadLevel(currentLevel);
    }

    public void LoadLevel(LevelData level)
    {
        if (isLoading)
        {
            return;
        }

        isLoading = true;

        try
        {
            Time.timeScale = 1f;
            if (pauseTimeFreezeRoutine != null)
            {
                StopCoroutine(pauseTimeFreezeRoutine);
                pauseTimeFreezeRoutine = null;
            }
            StopTimer();
            session = SessionState.Playing;
            isLevelActive = false;
            levelComplete = false;
            timeUpSoundPlayed = false;
            currentLevel = level;
            SyncCurrentLevelIndex(level);
            ClearRuntimeLevel();

            if (currentLevel == null)
            {
                Debug.LogError("LevelManager: LevelData is not assigned.", this);
                return;
            }

            if (boardManager == null)
            {
                Debug.LogError("LevelManager: BoardManager is not assigned.", this);
                return;
            }

            boardManager.ApplyGridSize(currentLevel.ResolvedGridWidth, currentLevel.ResolvedGridHeight);
            SpawnTargets();
            SpawnBlocks();
            RefreshBoardPresentation();
            isLevelActive = true;
            remainingSeconds = timeLimitSeconds;
            timerRunning = true;
        }
        finally
        {
            isLoading = false;
        }

        if (isLevelActive)
        {
            NotifyBlockSettled();
        }

        SyncSessionScreen();
    }

    public void PauseSession()
    {
        if (session != SessionState.Playing)
        {
            return;
        }

        session = SessionState.Paused;
        timerRunning = false;
        SyncSessionScreen();
        if (pauseTimeFreezeRoutine != null)
        {
            StopCoroutine(pauseTimeFreezeRoutine);
        }

        pauseTimeFreezeRoutine = StartCoroutine(FreezeTimeAfterPauseUi());
    }

    public void ResumeSession()
    {
        if (session != SessionState.Paused)
        {
            return;
        }

        if (pauseTimeFreezeRoutine != null)
        {
            StopCoroutine(pauseTimeFreezeRoutine);
            pauseTimeFreezeRoutine = null;
        }

        Time.timeScale = 1f;
        session = SessionState.Playing;
        timerRunning = true;
        SyncSessionScreen();
    }

    public void NotifyBlockSettled()
    {
        if (!isLevelActive || isLoading || levelComplete || boardManager == null)
        {
            return;
        }

        if (session == SessionState.TimeExpired || session == SessionState.Paused)
        {
            return;
        }

        if (!boardManager.AreAllMatchesComplete())
        {
            return;
        }

        levelComplete = true;
        session = SessionState.Completed;
        StopTimer();
        Time.timeScale = 1f;
        Debug.Log("LEVEL COMPLETE!");
        if (audioFeedback != null)
        {
            audioFeedback.PlayLevelComplete();
        }

        if (hapticFeedback != null)
        {
            hapticFeedback.PlayLevelComplete();
        }

        SyncSessionScreen();
    }

    private void ExpireTime()
    {
        if (session != SessionState.Playing || levelComplete)
        {
            return;
        }

        session = SessionState.TimeExpired;
        StopTimer();
        remainingSeconds = 0f;
        Time.timeScale = 1f;
        if (!timeUpSoundPlayed)
        {
            timeUpSoundPlayed = true;
            if (audioFeedback != null)
            {
                audioFeedback.PlayTimeUp();
            }

            if (hapticFeedback != null)
            {
                hapticFeedback.PlayTimeUp();
            }
        }

        SyncSessionScreen();
    }

    private IEnumerator SyncSessionScreenWhenUiReady()
    {
        yield return null;
        SyncSessionScreen();
    }

    private void SyncSessionScreen()
    {
        UIController ui = UIController.instance;
        if (ui == null)
        {
            return;
        }

        ScreenType wanted = ScreenType.Gameplay;
        if (session == SessionState.Completed)
        {
            wanted = ScreenType.LevelComplete;
        }
        else if (session == SessionState.TimeExpired)
        {
            wanted = ScreenType.GameOver;
        }
        else if (session == SessionState.Paused)
        {
            wanted = ScreenType.Settings;
        }

        ScreenType active = ui.GetActiveScreen();
        if (active == wanted || active == ScreenType.None)
        {
            return;
        }

        ui.ShowNextScreen(wanted);
    }

    private void HandleBackButton()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (session == SessionState.Playing)
        {
            PauseSession();
            return;
        }

        if (session == SessionState.Paused)
        {
            ResumeSession();
        }
    }

    private IEnumerator FreezeTimeAfterPauseUi()
    {
        yield return null;
        if (session == SessionState.Paused)
        {
            Time.timeScale = 0f;
        }

        pauseTimeFreezeRoutine = null;
    }

    private void StopTimer()
    {
        timerRunning = false;
    }

    private void SyncCurrentLevelIndex(LevelData level)
    {
        if (levelDatabase == null || level == null)
        {
            return;
        }

        IReadOnlyList<LevelData> levels = levelDatabase.Levels;
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] == level)
            {
                currentLevelIndex = i;
                return;
            }
        }
    }

    private void SpawnTargets()
    {
        if (currentLevel.targets == null)
        {
            return;
        }

        if (targetPrefab == null)
        {
            Debug.LogError("LevelManager: Target prefab is not assigned.", this);
            return;
        }

        var boardRect = (RectTransform)boardManager.transform;

        for (int i = 0; i < currentLevel.targets.Count; i++)
        {
            LevelTargetData data = currentLevel.targets[i];
            if (data == null)
            {
                continue;
            }

            Target target = Instantiate(targetPrefab, boardRect, false);
            target.ApplyLayout(data.shapeType, data.cells, data.composition, data.outerShape);
            target.Initialize(boardManager, data.gridPosition);
            spawnedTargets.Add(target);
        }
    }

    private void SpawnBlocks()
    {
        if (currentLevel.blocks == null)
        {
            return;
        }

        if (blockPrefab == null)
        {
            Debug.LogError("LevelManager: Block prefab is not assigned.", this);
            return;
        }

        var boardRect = (RectTransform)boardManager.transform;

        for (int i = 0; i < currentLevel.blocks.Count; i++)
        {
            LevelBlockData data = currentLevel.blocks[i];
            if (data == null)
            {
                continue;
            }

            Block block = Instantiate(blockPrefab, boardRect, false);
            block.ApplyLayout(data.shapeType, data.cells, data.composition, data.outerShape);
            block.MoveDirection = data.moveDirection;
            block.Initialize(boardManager, data.gridPosition);

            BlockMover mover = block.GetComponent<BlockMover>();
            if (mover != null)
            {
                mover.SetLevelManager(this);
                mover.SetAudioFeedback(audioFeedback);
                mover.SetHapticFeedback(hapticFeedback);
            }

            spawnedBlocks.Add(block);
        }
    }

    public Block SpawnSplitBlock(Block template, IReadOnlyList<ShapeCellData> remainingCells, Vector2Int worldAnchor)
    {
        if (blockPrefab == null || boardManager == null || template == null)
        {
            return null;
        }

        var boardRect = (RectTransform)boardManager.transform;
        Block block = Instantiate(blockPrefab, boardRect, false);
        block.MoveDirection = template.MoveDirection;
        block.ApplyLayout(template.ShapeType, remainingCells, PieceComposition.Simple, template.OuterShape);
        block.Initialize(boardManager, worldAnchor);

        BlockMover mover = block.GetComponent<BlockMover>();
        if (mover != null)
        {
            mover.SetLevelManager(this);
            mover.SetAudioFeedback(audioFeedback);
            mover.SetHapticFeedback(hapticFeedback);
        }

        spawnedBlocks.Add(block);
        return block;
    }

    private void ClearRuntimeLevel()
    {
        if (boardManager != null)
        {
            MatchEffect[] effects = boardManager.GetComponentsInChildren<MatchEffect>(true);
            for (int i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                {
                    Destroy(effects[i].gameObject);
                }
            }
        }

        for (int i = spawnedBlocks.Count - 1; i >= 0; i--)
        {
            Block block = spawnedBlocks[i];
            if (block == null)
            {
                continue;
            }

            if (boardManager != null)
            {
                boardManager.UnregisterBlock(block);
            }

            Destroy(block.gameObject);
        }

        spawnedBlocks.Clear();

        for (int i = spawnedTargets.Count - 1; i >= 0; i--)
        {
            Target target = spawnedTargets[i];
            if (target == null)
            {
                continue;
            }

            if (boardManager != null)
            {
                boardManager.UnregisterTarget(target);
            }

            Destroy(target.gameObject);
        }

        spawnedTargets.Clear();

        if (boardManager != null)
        {
            boardManager.ClearRuntimeRegistrations();
        }
    }

    private void RefreshBoardPresentation()
    {
        if (boardManager == null)
        {
            return;
        }

        BoardVisual visual = boardManager.GetComponent<BoardVisual>();
        if (visual != null)
        {
            visual.RefreshPresentation();
        }

        for (int i = 0; i < spawnedBlocks.Count; i++)
        {
            if (spawnedBlocks[i] != null)
            {
                spawnedBlocks[i].RefreshLayoutVisuals();
            }
        }

        for (int i = 0; i < spawnedTargets.Count; i++)
        {
            if (spawnedTargets[i] != null)
            {
                spawnedTargets[i].RefreshLayoutVisuals();
            }
        }
    }
}
