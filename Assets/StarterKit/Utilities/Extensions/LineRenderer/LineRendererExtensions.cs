using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;


namespace StarterKit.PhysicsUtilities
{

    public static class LineRendererExtensions
    {

        public static IEnumerator FadeInLine(this LineRenderer lineRenderer, float duration, Action callback = null, float delay = 0f)
        {
            float currentTime = 0;

            Material lineMaterial = lineRenderer.material;
            Color initialColor = lineMaterial.color;

            while (currentTime < delay) { 
            
                currentTime += Time.unscaledDeltaTime;
                yield return null;
            }

            currentTime = 0;


            while (currentTime < duration)
            {
                float alpha = Mathf.Lerp(0.0f, 1.0f, currentTime / duration);
                lineMaterial.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                currentTime += Time.unscaledDeltaTime;
                yield return null;
            }

            lineMaterial.color = new Color(initialColor.r, initialColor.g, initialColor.b, 1);
            callback?.Invoke();
        }

        public static IEnumerator FadeOutLine(this LineRenderer lineRenderer, float duration, Action callback = null, float delay = 0f)
        {

            float currentTime = 0;

            while(currentTime < delay)
            {
                currentTime += Time.unscaledDeltaTime;
                yield return null;
            }

            currentTime = 0;

            Material lineMaterial = lineRenderer.material;
            Color initialColor = lineMaterial.color;

            while (currentTime < duration)
            {
                float alpha = Mathf.Lerp(1.0f, 0.0f, currentTime / duration);
                lineMaterial.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);
                currentTime += Time.unscaledDeltaTime;
                yield return null;
            }

            lineMaterial.color = new Color(initialColor.r, initialColor.g, initialColor.b, 0);
            callback?.Invoke();
        }
    }
}