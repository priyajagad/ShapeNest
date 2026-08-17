using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System;

namespace StarterKit.UIKit
{

    public class FadeAnimatable : Animatable
    {
        CanvasGroup _TargetCanvasGroup;

        float _initialAplha;

        int MaxLayer;

        public override void Initialize(UIScreenBase screenView)
        {
            TryGetComponent<CanvasGroup>(out _TargetCanvasGroup);

            if (_TargetCanvasGroup == null)
            {
                _TargetCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            _initialAplha = _TargetCanvasGroup.alpha;
        
            MaxLayer = Enum.GetNames(typeof(AnimationLayer)).Length;
        }

        public override void ResetAnimator()
        {
            _TargetCanvasGroup.alpha = 0;
        }

        public override void ShowAnimation(Sequence sequence, SequenceOperationType sequenceOperationType)
        {
            ResetAnimator();

            switch(sequenceOperationType)
            {
                case SequenceOperationType.Append:
                    sequence.Append(_TargetCanvasGroup.DOFade(_initialAplha, InTime).SetEase(InAnimationEase));
                    break;
                case SequenceOperationType.Join:
                    sequence.Join(_TargetCanvasGroup.DOFade(_initialAplha, InTime).SetEase(InAnimationEase));
                    break;
            }
        }

        public override void HideAnimation(Sequence sequence, SequenceOperationType sequenceOperationType)
        {
            switch (sequenceOperationType)
            {
                case SequenceOperationType.Append:
                    sequence.Prepend(_TargetCanvasGroup.DOFade(0, OutTime).SetEase(OutAnimationEase));
                    break;
                case SequenceOperationType.Join:
                    sequence.Join(_TargetCanvasGroup.DOFade(0, OutTime).SetEase(OutAnimationEase));
                    break;
            }
        }

    }
}

