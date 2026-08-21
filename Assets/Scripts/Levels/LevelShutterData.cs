using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LevelShutterData
{
    [Min(1)]
    [Tooltip("Successful match events required to open this shutter.")]
    public int durability = 1;

    [Tooltip("Board cells covered by this shutter. Cells may overlap blocks/targets because the shutter is an overlay obstacle.")]
    public List<Vector2Int> cells = new List<Vector2Int>();

    public LevelShutterData Clone()
    {
        return new LevelShutterData
        {
            durability = Mathf.Max(1, durability),
            cells = cells != null ? new List<Vector2Int>(cells) : new List<Vector2Int>()
        };
    }
}
