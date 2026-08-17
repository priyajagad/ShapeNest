using UnityEngine;
using DG.Tweening;
using System;

namespace StarterKit.UIKit {

    public class ScaleAnimatable : Animatable {

        Transform _TargetTransform;
        Vector3 _initialScale;

        int MaxLayer;

        public override void Initialize(UIScreenBase screenView) {

            _TargetTransform = transform;
            _initialScale = _TargetTransform.localScale;

            _TargetTransform.localScale = Vector3.zero;

            MaxLayer = Enum.GetNames(typeof(AnimationLayer)).Length; ;

        }

        public override void ResetAnimator()
        {
            _TargetTransform.localScale = Vector3.zero;

        }

        public override void ShowAnimation(Sequence sequence, SequenceOperationType sequenceOperationType) {

            ResetAnimator();

            switch (sequenceOperationType)
            {
                case SequenceOperationType.Append:
                    sequence.Append(_TargetTransform.DOScale(_initialScale, InTime).SetEase(InAnimationEase));
                    break;
                case SequenceOperationType.Join:
                    sequence.Join(_TargetTransform.DOScale(_initialScale, InTime).SetEase(InAnimationEase));
                    break;
            }

        }

        public override void HideAnimation(Sequence sequence, SequenceOperationType sequenceOperationType) {

            switch (sequenceOperationType)
            {
                case SequenceOperationType.Append:
                    sequence.Prepend(_TargetTransform.DOScale(0, OutTime).SetEase(OutAnimationEase));
                    break;
                case SequenceOperationType.Join:
                    sequence.Join(_TargetTransform.DOScale(0, OutTime).SetEase(OutAnimationEase));
                    break;
            }

        }

    }
}

