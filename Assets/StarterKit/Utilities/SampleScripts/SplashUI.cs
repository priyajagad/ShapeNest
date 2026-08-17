using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StarterKit.UIKit;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

namespace StarterKit.UI
{

    public class SplashUI : UIBase
    {

        public Image ProgressCircle;


        private IEnumerator Start()
        {
            //Application.targetFrameRate = 60;

            yield return new WaitForSeconds(0.2f);

            Show();
        }

      
        IEnumerator LoadScene()
        {

            yield return new WaitForSeconds(2f);
            //Change the scene Index here to load after splash screen
            AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(1);
            asyncOperation.allowSceneActivation = false;
            while (!asyncOperation.isDone)
            {
                //Output the current progress
                if (ProgressCircle != null)
                {
                    ProgressCircle.fillAmount = asyncOperation.progress;
                }

                if (asyncOperation.progress >= 0.9f)
                {
                    yield return new WaitForSeconds(1f);
                    Hide();
                    yield return new WaitForSeconds(1f);
                    asyncOperation.allowSceneActivation = true;
                }

                yield return null;
            }
        }

    }
}