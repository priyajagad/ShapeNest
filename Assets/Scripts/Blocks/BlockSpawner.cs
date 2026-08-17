using UnityEngine;

/// <summary>
/// Instantiates Block prefabs onto the Board. A future LevelManager will call this from level data.
/// Includes a temporary SpawnTestLevel() used only for early playable checks.
/// </summary>
public class BlockSpawner : MonoBehaviour
{
    [SerializeField]
    private Block blockPrefab;

    [SerializeField]
    private BoardManager boardManager;

    private void Start()
    {
        SpawnTestLevel();
    }

    /// <summary>
    /// TEMPORARY hardcoded test layout. Delete or replace when real level data exists.
    /// Positions are Board grid cells; Block.Initialize uses BoardManager.GridToLocal.
    /// </summary>
    public void SpawnTestLevel()
    {
        SpawnBlock(new Vector2Int(1, 2), ShapeType.Square, MoveDirection.Any);
        SpawnBlock(new Vector2Int(3, 2), ShapeType.Circle, MoveDirection.Left);
        SpawnBlock(new Vector2Int(2, 4), ShapeType.Triangle, MoveDirection.Down);
    }

    public Block SpawnBlock(Vector2Int gridPosition, ShapeType shapeType, MoveDirection moveDirection)
    {
        if (blockPrefab == null)
        {
            Debug.LogError("BlockSpawner: Block prefab is not assigned.", this);
            return null;
        }

        if (boardManager == null)
        {
            Debug.LogError("BlockSpawner: BoardManager is not assigned.", this);
            return null;
        }

        var boardRect = (RectTransform)boardManager.transform;
        Block block = Instantiate(blockPrefab, boardRect, false);
        block.Initialize(boardManager, gridPosition);
        block.ShapeType = shapeType;
        block.MoveDirection = moveDirection;

        return block;
    }
}
