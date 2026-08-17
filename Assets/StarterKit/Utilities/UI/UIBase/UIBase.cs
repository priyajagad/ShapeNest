using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarterKit.UIKit
{
    public enum State
    {
        Active,
        Inactive
    }


    public abstract class UIBase : MonoBehaviour
    {
        public static string BACKGROUND = "Background";
        public static string PARALLAX = "Parallax";
        public static string PARENT = "Parent";


        public delegate void CanvasStateChanged(State _state);
        public event CanvasStateChanged OnCanvasStateChanged;

        [HideInInspector]
        public RectTransform Background;
        //[HideInInspector]
        //public ParallaxUVScroller Parallax;
        [HideInInspector]
        public RectTransform Parent;

        protected Action InternalCallback;

        Canvas _canvas;

        GraphicRaycaster _raycaster;

        CanvasScaler _canvasScaler;

        public Vector2 RefernceResolution
        {
            get
            {
                return _canvasScaler.referenceResolution;
            }
        }

        public float ScaleFactor
        {
            get
            {
                return _canvasScaler.scaleFactor;
            }
        }


        public State CanvasState
        {
            get
            {
                return _canvasState;
            }
            set
            {
                _canvasState = value;

                _canvas.enabled = value == State.Active;

                OnCanvasStateChanged?.Invoke(value);
            }
        }

        State _canvasState;


        public State RaycasterState
        {
            get
            {
                return _raycasterState;
            }
            set
            {
                _raycasterState = value;

                _raycaster.enabled = value == State.Active;
            }
        }


        State _raycasterState;



        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();
            _canvasScaler = GetComponent<CanvasScaler>();

            Background = transform.Find(BACKGROUND).GetComponent<RectTransform>();
            //Parallax = Background.GetComponentInChildren<ParallaxUVScroller>();
            Parent = transform.Find(PARENT).GetComponent<RectTransform>();


            ResetCanvas();
            OnAwake();
        }

        public virtual void OnAwake() { }



        #region Canvas_Operations
        void ResetCanvas()
        {
            _raycaster.enabled = false;
            _canvas.enabled = false;
            _canvasState = State.Inactive;
        }

        public virtual void Show(Action Callback = null)
        {
            CanvasState = State.Active;
            InternalCallback = Callback;
        }
        public virtual void Hide(Action Callback = null)
        {
            InternalCallback = Callback;
        }

        public virtual void OnScreenShowAnimationStarted() { }

        public virtual void OnScreenShowAnimationCompleted()
        {
            InternalCallback?.Invoke();
            InternalCallback = null;
            RaycasterState = State.Active;
        }

        public virtual void OnScreenActive() { }

        public virtual void OnBackPress() { }

        public virtual void OnScreenHideAnimationStarted()
        {
            RaycasterState = State.Inactive;
        }

        public virtual void OnScreenHideAnimationCompleted()
        {
            CanvasState = State.Inactive;
            InternalCallback?.Invoke();
            InternalCallback = null;
        }

        #endregion


    }
}