using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StarterKit.Utilities
{

    public enum Quality
    {
        Low,
        Medium,
        High
    }

    public class DynamicGraphicsConfigurator : MonoBehaviour
    {
        Camera mainCamera;

        public Quality EditorQuality = Quality.Medium;



        private void Awake()
        {
            mainCamera = GetComponent<Camera>();
        }

        void Start()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                Debug.LogError("No main camera found");
                return;
            }

#if UNITY_EDITOR
            SetQuality(EditorQuality);
#else
#if UNITY_ANDROID
            ConfigureGraphics();
#else
            SetQuality(Quality.High);
#endif
#endif
        }

        void ConfigureGraphics()
        {
            // Get device performance metrics
            int memorySize = SystemInfo.systemMemorySize;
            int processorCount = SystemInfo.processorCount;
            int maxTextureSize = SystemInfo.maxTextureSize;
            GraphicsTier tier = Graphics.activeTier;

            // Adjust camera settings based on device performance
            if (memorySize > 4000 && processorCount >= 8 && maxTextureSize >= 4096 && tier == GraphicsTier.Tier3)
            {
                SetHighQuality();
            }
            else if (memorySize > 2000 && processorCount >= 4 && maxTextureSize >= 2048)
            {
                SetMediumQuality();
            }
            else
            {
                SetLowQuality();
            }
        }

        void SetHighQuality()
        {
            Debug.Log("Setting high quality graphics");
            mainCamera.allowHDR = false;
            mainCamera.allowMSAA = true;

            UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
        }

        void SetMediumQuality()
        {
            Debug.Log("Setting medium quality graphics");
            mainCamera.allowHDR = false;
            mainCamera.allowMSAA = true;

            UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cameraData.antialiasingQuality = AntialiasingQuality.Low;
        }

        void SetLowQuality()
        {
            Debug.Log("Setting low quality graphics");
            mainCamera.allowHDR = false;
            mainCamera.allowMSAA = false;

            UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.None;
        }

        public void SetQuality(Quality quality)
        {
            switch (quality)
            {
                case Quality.Low:
                    SetLowQuality();
                    break;
                case Quality.Medium:
                    SetMediumQuality();
                    break;
                case Quality.High:
                    SetHighQuality();
                    break;
            }
        }

    }

}