using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System;

namespace StarterKit.UIKit
{

    public class SlideAnimatable : Animatable
    {

        public Direction Direction;
        public TransitionType AnimationTransition = TransitionType.Opposite;
        RectTransform _TargetTransform;
        Vector3 _initialPosition;

        float offsetX;
        float offsetY;

        int MaxLayer;

        public override void Initialize(UIScreenBase screenView)
        {


            _TargetTransform = GetComponent<RectTransform>();
            _initialPosition = _TargetTransform.anchoredPosition3D;

            offsetX = screenView.RefernceResolution.x * screenView.ScaleFactor;
            offsetY = screenView.RefernceResolution.y * screenView.ScaleFactor;


            switch (Direction)
            {
                case Direction.Top:
                    _TargetTransform.anchoredPosition = new Vector2(_initialPosition.x, _initialPosition.y + offsetY);
                    break;

                case Direction.Bottom:
                    _TargetTransform.anchoredPosition = new Vector2(_initialPosition.x, _initialPosition.y - offsetY);
                    break;

                case Direction.Left:
                    _TargetTransform.anchoredPosition = new Vector2(_initialPosition.x - offsetX, _initialPosition.y);
                    break;

                case Direction.Right:
                    _TargetTransform.anchoredPosition = new Vector2(_initialPosition.x + offsetX, _initialPosition.y);
                    break;
            }

            MaxLayer = Enum.GetNames(typeof(AnimationLayer)).Length; ;

        }

        public override void ResetAnimator()
        {
            Vector3 StartPos = Vector3.zero;


            switch (Direction)
            {
                case Direction.Top:
                    StartPos = new Vector2(_initialPosition.x, _initialPosition.y + offsetY);
                    break;

                case Direction.Bottom:
                    StartPos = new Vector2(_initialPosition.x, _initialPosition.y - offsetY);
                    break;

                case Direction.Left:
                    StartPos = new Vector2(_initialPosition.x - offsetX, _initialPosition.y);
                    break;

                case Direction.Right:
                    StartPos = new Vector2(_initialPosition.x + offsetX, _initialPosition.y);
                    break;
            }


            _TargetTransform.anchoredPosition = StartPos;
        }

        public override void ShowAnimation(Sequence sequence, SequenceOperationType sequenceOperationType)
        {
            ResetAnimator();

            switch (sequenceOperationType)
            {
                case SequenceOperationType.Append:
                    sequence.Append(_TargetTransform.DOAnchorPos(_initialPosition, InTime).SetEase(InAnimationEase));
                    break;
                case SequenceOperationType.Join:
                    sequence.Join(_TargetTransform.DOAnchorPos(_initialPosition, InTime).SetEase(InAnimationEase));
                    break;
            }

        }

        public override void HideAnimation(Sequence sequence, SequenceOperationType sequenceOperationType)
        {
           
            Vector2 _endPosition = Vector2.zero;

            switch (Direction)
            {

                case Direction.Top:

                    if (AnimationTransition == TransitionType.Same)
                    {
                        _endPosition = new Vector2(_initialPosition.x, _initialPosition.y + offsetY);

                    }
                    else
                    {
                        _endPosition = new Vector2(_initialPosition.x, _initialPosition.y - offsetY);
                    }

                    break;

                case Direction.Bottom:
                    if (AnimationTransition == TransitionType.Same)
                    {
                        _endPosition = new Vector2(_initialPosition.x, _initialPosition.y - offsetY);

                    }
                    else
                    {
                        _endPosition = new Vector2(_initialPosition.x, _initialPosition.y + offsetY);
                    }
                    break;

                case Direction.Left:

                    if (AnimationTransition == TransitionType.Same)
                    {
                        _endPosition = new Vector2(_initialPosition.x - offsetX, _initialPosition.y);
                    }
                    else
                    {
                        _endPosition = new Vector2(_initialPosition.x + offsetX, _initialPosition.y);
                    }

                    break;

                case Direction.Right:

                    if (AnimationTransition == TransitionType.Same)
                    {
                        _endPosition = new Vector2(_initialPosition.x + offsetX, _initialPosition.y);
                    }
                    else
                    {
                        _endPosition = new Vector2(_initialPosition.x - offsetX, _initialPosition.y);
                    }
                    break;
            }

            switch (sequenceOperationType)
            {
                case SequenceOperationType.Append:
                    sequence.Prepend(_TargetTransform.DOAnchorPos(_endPosition, OutTime).SetEase(OutAnimationEase));
                    break;
                case SequenceOperationType.Join:
                    sequence.Join(_TargetTransform.DOAnchorPos(_endPosition, OutTime).SetEase(OutAnimationEase));
                    break;
            }
        }

    }
}

