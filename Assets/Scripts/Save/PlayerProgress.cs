using System;

[Serializable]
public class PlayerProgress
{
    public int highestUnlockedLevel = 0;
    public int highestCompletedLevel = -1;
    public int currentLevelIndex = 0;
    public bool hasCompletedAllLevels = false;
}
