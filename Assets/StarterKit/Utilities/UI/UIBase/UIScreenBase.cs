using DG.Tweening;
using StarterKit.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace StarterKit.UIKit
{
    public enum ScreenState
    {
        ShowStarted,
        ShowCompleted,
        Active,
        HideStarted,
        HideCompleted
    }

    public class UIScreenBase : UIBase
    {
        #region Animation Properties

        [Header("Screen Properties")]
        public ScreenAnimationType ScreenAnimationType = ScreenAnimationType.None;

        public Direction Direction = Direction.Right;

        public TransitionType TransitionType = TransitionType.Opposite;

        public Ease InAnimationEase = Ease.OutExpo;
        public Ease OutAnimationEase = Ease.OutExpo;
        public float InTime = 0.7f;
        public float OutTime = 0.5f;

        public bool FadeBackgoundWhileAnimating = true;

        #endregion

        #region Screen Properties
        List<Animatable> animatables;
        Dictionary<AnimationLayer, List<Animatable>> sequenceDictinary = new Dictionary<AnimationLayer, List<Animatable>>();
        ScreenState _screenState;

        public ScreenState ScreenState
        {
            get
            {
                return _screenState;
            }
            set
            {
                _screenState = value;

                switch (_screenState)
                {
                    case ScreenState.ShowStarted:
                        OnScreenShowAnimationStarted();
                        break;
                    case ScreenState.ShowCompleted:
                        OnScreenShowAnimationCompleted();
                        break;
                    case ScreenState.Active:
                        StartCoroutine(ScreenUpdate());
                        break;
                    case ScreenState.HideStarted:
                        OnScreenHideAnimationStarted();
                        break;
                    case ScreenState.HideCompleted:
                        OnScreenHideAnimationCompleted();
                        break;
                }


                OnScreenStateUpdated?.Invoke();
            }
        }

        public Action OnScreenStateUpdated;
        #endregion


        #region MonoBehaviour Events
        public override void OnAwake()
        {
            base.OnAwake();
            Initialize();
        }

        private void OnDestroy()
        {
            CleanUp();
        }

        #endregion

        #region Screen Animation Methods

        private void Initialize()
        {

            SetupScreen();


            if (animatables != null)
            {
                animatables.Clear();
            }

            animatables = new List<Animatable>(GetComponentsInChildren<Animatable>());
            animatables.Sort();

            foreach (Animatable animatable in animatables)
            {
                animatable.Initialize(this);
            }

            SetupSequenceDictonary();
        }


        private void SetupScreen()
        {
            Animatable attachedAnimatable = null;

            switch (ScreenAnimationType)
            {
                case ScreenAnimationType.Fade:
                    attachedAnimatable = Parent.gameObject.AddComponent<FadeAnimatable>();
                    break;
                case ScreenAnimationType.Slide:
                    attachedAnimatable = Parent.gameObject.AddComponent<SlideAnimatable>();
                    break;
                case ScreenAnimationType.Scale:
                    attachedAnimatable = Parent.gameObject.AddComponent<ScaleAnimatable>();
                    break;

                case ScreenAnimationType.None:
                    return;
            }

            attachedAnimatable.AnimaionLayer = AnimationLayer.Base;
            if (attachedAnimatable is SlideAnimatable)
            {
                ((SlideAnimatable)attachedAnimatable).AnimationTransition = TransitionType;

                ((SlideAnimatable)attachedAnimatable).Direction = Direction;
            }
            attachedAnimatable.InAnimationEase = InAnimationEase;
            attachedAnimatable.OutAnimationEase = OutAnimationEase;
            attachedAnimatable.InTime = InTime;
            attachedAnimatable.OutTime = OutTime;


            if (FadeBackgoundWhileAnimating)
            {
                attachedAnimatable = Background.gameObject.AddComponent<FadeAnimatable>();

                attachedAnimatable.AnimaionLayer = AnimationLayer.Base;
                attachedAnimatable.InAnimationEase = InAnimationEase;
                attachedAnimatable.OutAnimationEase = OutAnimationEase;
                attachedAnimatable.InTime = InTime;
                attachedAnimatable.OutTime = OutTime;

            }


        }

        // public void ToggleParralax(bool enable)
        // {
        //     if(Parallax == null)    return;

        //     if (enable)
        //     {
        //         Parallax.StartScrolling();
        //     }
        //     else
        //     {
        //         Parallax.StopScrolling();
        //     }
        // }

        private void CleanUp()
        {
            animatables.Clear();

        }
        private void SetupSequenceDictonary()
        {
            foreach (AnimationLayer layer in Enum.GetValues(typeof(AnimationLayer)))
            {
                sequenceDictinary.Add(layer, new List<Animatable>());
            }

            foreach (Animatable animatable in animatables)
            {
                sequenceDictinary[animatable.AnimaionLayer].Add(animatable);
            }
        }

        public override void Show(Action Callback = null)
        {
            Sequence showSequence = DOTween.Sequence();

            ScreenState = ScreenState.ShowStarted;

            base.Show(Callback);

            foreach (var layeritems in sequenceDictinary)
            {
                if (layeritems.Value.Count > 0)
                {

                    for (int i = 0; i < layeritems.Value.Count; i++)
                    {
                        if (i == 0)
                        {
                            layeritems.Value[i].ShowAnimation(showSequence, SequenceOperationType.Append);
                        }
                        else
                        {
                            layeritems.Value[i].ShowAnimation(showSequence, SequenceOperationType.Join);
                        }

                    }

                }
            }

            showSequence.OnComplete(() =>
            {
                ScreenState = ScreenState.ShowCompleted;
                ScreenState = ScreenState.Active;
            });

        }

        public override void Hide(Action Callback = null)
        {
            Sequence hideSequence = DOTween.Sequence();

            base.Hide(Callback);

            ScreenState = ScreenState.HideStarted;

            foreach (var layeritems in sequenceDictinary)
            {
                if (layeritems.Value.Count > 0)
                {

                    for (int i = 0; i < layeritems.Value.Count; i++)
                    {
                        if (i == 0)
                        {
                            layeritems.Value[i].HideAnimation(hideSequence, SequenceOperationType.Append);
                        }
                        else
                        {
                            layeritems.Value[i].HideAnimation(hideSequence, SequenceOperationType.Join);
                        }

                    }

                }
            }

            hideSequence.OnComplete(() =>
            {
                ScreenState = ScreenState.HideCompleted;
            });

        }




        IEnumerator ScreenUpdate()
        {
            while (ScreenState == ScreenState.Active)
            {
                OnScreenActive();
                yield return null;
            }
        }



        #endregion
    }
}