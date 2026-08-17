using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace StarterKit.UIKit {

    public enum Direction {
        Top,
        Bottom,
        Left,
        Right,
    }

    public enum TransitionType
    {
        Same,
        Opposite
    }

    public enum AnimationLayer
    {
        Base = 1,
        Element,
        Element2,
        Element3
    }

    public enum ScreenAnimationType
    {
        None,
        Fade,
        Slide,
        Scale
    }

    public enum SequenceOperationType
    {
        Append,
        Join
    }


    [DisallowMultipleComponent]
    [System.Serializable]
    public abstract class Animatable : MonoBehaviour, IComparer<Animatable>, IComparable<Animatable> {

        [Header("Animation configration :")]
        public AnimationLayer AnimaionLayer = AnimationLayer.Element;

        public Ease InAnimationEase = Ease.OutExpo;
        public Ease OutAnimationEase = Ease.OutExpo;
        public float InTime = 0.3f;
        public float OutTime = 0.2f;

        public delegate void ShowAnimationCompleted();
        public ShowAnimationCompleted OnShowAnimationCompleted;

        public delegate void HideAnimationCompleted();
        public HideAnimationCompleted OnHideAnimationCompleted;

        public abstract void Initialize(UIScreenBase screenView);

        public abstract void ResetAnimator();


        public abstract void ShowAnimation(Sequence sequence, SequenceOperationType sequenceOperationType);
        public abstract void HideAnimation(Sequence sequence, SequenceOperationType sequenceOperationType);

        

        public int Compare(Animatable x, Animatable y) {
            return x.AnimaionLayer.CompareTo(y.AnimaionLayer);
        }

        public int CompareTo(Animatable other) {
            return AnimaionLayer.CompareTo(other.AnimaionLayer);

        }
    }
}

