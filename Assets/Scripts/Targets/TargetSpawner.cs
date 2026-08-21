using UnityEngine;

/// <summary>
/// Legacy debug helper for spawning Target prefabs onto the Board.
/// Runtime levels are owned by <see cref="LevelManager"/> — this component must not
/// auto-spawn on Start (even if re-enabled on the Board).
/// </summary>
public class TargetSpawner : MonoBehaviour
{
    [SerializeField]
    private Target targetPrefab;

    [SerializeField]
    private BoardManager boardManager;

    /// <summary>
    /// TEMPORARY hardcoded target layout. Call manually from the Inspector/context menu only.
    /// </summary>
    [ContextMenu("Spawn Test Targets (Debug)")]
    public void SpawnTestTargets()
    {
        SpawnTarget(new Vector2Int(4, 2), ShapeType.Square);
        SpawnTarget(new Vector2Int(0, 2), ShapeType.Circle);
        SpawnTarget(new Vector2Int(2, 0), ShapeType.Triangle);
    }

    private Target SpawnTarget(Vector2Int gridPosition, ShapeType shapeType)
    {
        if (targetPrefab == null)
        {
            Debug.LogError("TargetSpawner: Target prefab is not assigned.", this);
            return null;
        }

        if (boardManager == null)
        {
            Debug.LogError("TargetSpawner: BoardManager is not assigned.", this);
            return null;
        }

        var boardRect = (RectTransform)boardManager.transform;
        Target target = Instantiate(targetPrefab, boardRect, false);
        target.SetShapeType(shapeType);
        target.Initialize(boardManager, gridPosition);
        target.RectTransform.SetAsFirstSibling();
        return target;
    }
}
