using System;
using UnityEngine;

namespace StarterKit.Utilities
{
    public static class DelayedAction
    {
        public static void DelayedInvoke(this MonoBehaviour monoBehaviour, Action action, float delay)
        {
            monoBehaviour.StartCoroutine(DelayedInvocation(action, delay));
        }
        public static void DelayedInvokeUnscaled(this MonoBehaviour monoBehaviour, Action action, float delay)
        {
            monoBehaviour.StartCoroutine(DelayedInvocationRealtime(action, delay));
        }

        private static System.Collections.IEnumerator DelayedInvocation(Action action, float delay)
        {
            yield return new WaitForSeconds(delay);
            action.Invoke();
        }

        private static System.Collections.IEnumerator DelayedInvocationRealtime(Action action, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            action.Invoke();
        }
    }
}
