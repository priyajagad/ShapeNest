using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "Shape Nest/Level Data")]
public class LevelData : ScriptableObject
{
    public List<LevelBlockData> blocks = new List<LevelBlockData>();
    public List<LevelTargetData> targets = new List<LevelTargetData>();
}
