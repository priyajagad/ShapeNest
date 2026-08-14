using UnityEngine;

/// <summary>
/// TEMPORARY development/testing script. Delete after block placement is verified.
/// Spawns one Block prefab onto the parent Board at grid cell (2, 2).
/// </summary>
public class BlockTestSpawner : MonoBehaviour
{
    [SerializeField]
    private Block blockPrefab;

    private BoardManager boardManager;
    private Block spawnedBlock;
    private bool hasSpawned;

    private void Awake()
    {
        boardManager = GetComponentInParent<BoardManager>();
    }

    private void Start()
    {
        // Guard: Start can theoretically run more than once on the same instance.
        if (hasSpawned || spawnedBlock != null)
        {
            return;
        }

        if (boardManager == null)
        {
            boardManager = GetComponentInParent<BoardManager>();
        }

        if (boardManager == null)
        {
            Debug.LogError(
                "BlockTestSpawner: BoardManager was not found on a parent GameObject. Block will not spawn.",
                this);
            return;
        }

        if (blockPrefab == null)
        {
            Debug.LogError("BlockTestSpawner: blockPrefab is not assigned. Block will not spawn.", this);
            return;
        }

        hasSpawned = true;

        var boardRect = (RectTransform)boardManager.transform;

        // Parent under the Board RectTransform and keep the prefab's local scale.
        spawnedBlock = Instantiate(blockPrefab, boardRect, false);
        spawnedBlock.Initialize(boardManager, new Vector2Int(2, 2));
    }
}
