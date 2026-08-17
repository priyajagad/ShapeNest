using System.Linq;
using UnityEngine;

namespace StarterKit.Utilities
{
    public class FPSManager : MonoBehaviour
    {
        void Start()
        {
            // Get the highest supported refresh rate
            //int maxSupportedFPS = GetMaxSupportedFPS();
            int maxSupportedFPS = (int)Screen.currentResolution.refreshRateRatio.value;

            // Dont Let The Screen Sleep
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Set the target frame rate
            Application.targetFrameRate = maxSupportedFPS;

            Debug.Log("Target frame rate set to: " + maxSupportedFPS + " FPS");
        }

        int GetMaxSupportedFPS()
        {
            // Retrieve all the supported refresh rates for the current display
            int[] supportedRefreshRates = Screen.resolutions.Select(r => (int)r.refreshRateRatio.value).Distinct().ToArray();

            // Find the maximum refresh rate
            int maxRefreshRate = supportedRefreshRates.Max();

            return maxRefreshRate;
        }
    }
}