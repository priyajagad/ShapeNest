using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelDatabase", menuName = "Shape Nest/Level Database")]
public class LevelDatabase : ScriptableObject
{
    [SerializeField]
    private List<LevelData> levels = new List<LevelData>();

    public IReadOnlyList<LevelData> Levels => levels;

    public int Count => levels != null ? levels.Count : 0;

    public LevelData GetLevel(int index)
    {
        if (levels == null || levels.Count == 0)
        {
            Debug.LogWarning("LevelDatabase: The database is empty.", this);
            return null;
        }

        if (index < 0 || index >= levels.Count)
        {
            Debug.LogWarning(
                $"LevelDatabase: Invalid level index {index}. Valid range is 0 to {levels.Count - 1}.",
                this);
            return null;
        }

        return levels[index];
    }
}
