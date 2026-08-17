using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace StarterKit.UIKit
{
    [Serializable]
    public class UIScreen
    {
        public ScreenType screenType;
        public UIScreenBase screenView;
    }

    public enum ScreenType
    {
        None = 0,
        LoadingScreen = 1,
        TopNavigation = 2,
        MainMenu = 3,
        Settings = 4,
        Shop = 5,
        Profile = 6,
        Gameplay = 7,
        //MatchMaking = 8,
        //Result = 9,
        //IAPPurchase = 10,
        //Blank = 11,
        // DressingRoomUI = 12,
        // CategoryDisplayUI = 13,
        // ShopItemDetailUI = 14,
        DailyChallenge = 15,
        DailyReward = 16,
        //Reward = 17,
        AlertPopup = 19,
        NoInternetPopup = 20,
        ConfirmationPopup = 21,
        RewardPopup = 22,
        // CustomizationUI = 23,
        //Inventory = 24,
        // ProfileUI = 25,
        //TrophyRoad = 26,
        //Stats = 27,
        //ArenaSelection = 28,
        //LoadingTransition = 29,
        LevelComplete = 30,
        GameOver = 31,
        // 32 reserved (removed BoosterConfirmationPopup)
        BoosterPurchasePopup = 33,
        BoosterUnlockPopup = 34,
        SpeedUpPurchasePopup = 35,
        FeatureUnlockPopup = 36,
    }

    public class UIController : IndestructibleSingleton<UIController>
    {
        public Camera UICamera;

        public ScreenType StartScreen;
        public List<UIScreen> Screens;

        [Header("3D Object Management")]
        public GameObject managed3DObject; // Reference to the 3D object to show in UI screens

        [SerializeField]
        private List<ScreenType> currentScreens;

        private Dictionary<ScreenType, List<ScreenType>> activePopups = new();
        private ScreenType previousScreen = ScreenType.None;
        public ScreenType PreviousScreen => previousScreen;
        public bool isPointerOverUIElement = false;

        public override void OnAwake()
        {
            base.OnAwake();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            Camera[] cameras = FindObjectsOfType<Camera>(true);

            foreach (var camera in cameras)
            {
                if (camera != UICamera)
                {
                    var cameraData = camera.GetUniversalAdditionalCameraData();

                    if (cameraData.renderType == CameraRenderType.Base)
                    {
                        if (!cameraData.cameraStack.Contains(UICamera))
                        {
                            cameraData.cameraStack.Add(UICamera);
                        }
                    }
                }
            }
        }

        private IEnumerator Start()
        {
            currentScreens = new List<ScreenType>();
            ShowScreen(StartScreen);
            yield return null;
        }

        public void ShowNextScreen(ScreenType screenType)
        {
            if (!currentScreens.Contains(screenType))
            {
                if (currentScreens.Count > 0)
                {
                    previousScreen = currentScreens.Last();
                    HideScreen(currentScreens.Last(), () =>
                    {
                        ShowScreen(screenType);
                    });
                }
                else
                {
                    ShowScreen(screenType);
                }
            }
        }

        public void ShowScreen(ScreenType screenType)
        {
            currentScreens.Add(screenType);
            getScreen(screenType).Show();
        }

        public void HideScreen(ScreenType screenType, Action Callback)
        {
            getScreen(screenType).Hide(() =>
            {
                Callback?.Invoke();
                currentScreens.Remove(screenType);
                ClosePopupsLinkedWithScreen(screenType);
            });
        }

        public UIScreenBase getScreen(ScreenType screenType)
        {
            return Screens.Find(screen => screen.screenType == screenType).screenView;
        }

        public bool IsScreenActive(ScreenType screenType)
        {
            return currentScreens != null && currentScreens.Contains(screenType);
        }

        public bool IsPopupActive(ScreenType popupScreenType)
        {
            foreach (List<ScreenType> popups in activePopups.Values)
            {
                if (popups.Contains(popupScreenType))
                    return true;
            }

            return false;
        }

        public ScreenType GetActiveScreen()
        {
            if (currentScreens == null || currentScreens.Count == 0)
                return ScreenType.None;

            return currentScreens[currentScreens.Count - 1];
        }

        public void CloseAllPopups()
        {
            if (activePopups.Count == 0)
                return;

            var popupTypes = new List<ScreenType>();
            foreach (List<ScreenType> popups in activePopups.Values)
            {
                foreach (ScreenType popup in popups)
                {
                    if (!popupTypes.Contains(popup))
                        popupTypes.Add(popup);
                }
            }

            foreach (ScreenType popup in popupTypes)
                ClosePopup(popup);
        }

        public void OpenPopup(ScreenType popupScreenType, ScreenType LinkedScreen = ScreenType.None)
        {
            if (!activePopups.ContainsKey(LinkedScreen))
            {
                activePopups[LinkedScreen] = new List<ScreenType>();
            }

            getScreen(popupScreenType).Show();
            activePopups[LinkedScreen].Add(popupScreenType);
        }

        public void ClosePopup(ScreenType popupScreenType)
        {
            foreach (var screenType in activePopups.Keys)
            {

                foreach (var popup in activePopups[screenType])
                {

                    if (popup == popupScreenType)
                    {
                        getScreen(popupScreenType).Hide();
                        activePopups[screenType].Remove(popupScreenType);

                        if (activePopups[screenType].Count == 0)
                        {
                            activePopups.Remove(screenType);
                        }

                        return;
                    }
                }
            }
        }

        private void ClosePopupsLinkedWithScreen(ScreenType screenType)
        {
            if (activePopups.ContainsKey(screenType))
            {
                foreach (var popup in activePopups[screenType])
                {
                    getScreen(popup).Hide();
                }
                activePopups.Remove(screenType);
            }
        }

        public void Show3DObject(Transform parentTransform)
        {
            if (managed3DObject != null)
            {
                managed3DObject.SetActive(true);
            }
        }

        public void Hide3DObject()
        {
            if (managed3DObject != null)
            {
                managed3DObject.SetActive(false);
            }
        }

        public bool Is3DObjectActive()
        {
            return managed3DObject != null && managed3DObject.activeInHierarchy;
        }
    }
}
