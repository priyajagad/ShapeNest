using DG.Tweening;
using UnityEngine;

public static class TimeUtils
{
    public static Tween LerpTimeScale(float targetTimeScale, float duration)
    {
        return DOVirtual.Float(Time.timeScale, targetTimeScale, duration, value => Time.timeScale = value);
    }
}
