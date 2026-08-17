using System;
using UnityEngine;

namespace StarterKit.UIKit
{


    #region Enums
    public enum TextFieldType
    {
        PRIMARY,
        SECONDARY,
        NONE
    }

    #endregion

    [CreateAssetMenu(fileName = "UIStyleData", menuName = "ScriptableObjects/UIStyleDataScriptableObject", order = 1)]
    public class UIStyleData : ScriptableObject
    {
        [Header("Logo")]
        public Font LogoFontStyle;
        public Color LogoColor;

        [Header("UI")]
        public Font UIFontStyle;
        public int PrimaryFontSize;
        public Color PrimaryColor;
        public int SecondaryFontSize;
        public Color SecondaryColor;






        #region previousData
        [Header("Logo")]
        Font PreLogoFontStyle;

        [Header("UI")]
        Font PreUIFontStyle;
        #endregion


        private void OnValidate()
        {
            if (PreLogoFontStyle != LogoFontStyle)
            {
                OnLogoStyleUpdated();
            }

            if (PreUIFontStyle != UIFontStyle)
            {
                OnFontStyleUpdated();
            }

        }

        #region Methods
        private void OnFontStyleUpdated()
        {
            foreach (var dynamicField in FindObjectsOfType<DynamicTextField>())
            {
                dynamicField.UpdateFont();
            }

            PreUIFontStyle = UIFontStyle;
        }

        private void OnLogoStyleUpdated()
        {
            foreach (var dynamicField in FindObjectsOfType<DynamicLogoText>())
            {
                dynamicField.UpdateFont();
            }

            PreLogoFontStyle = LogoFontStyle;
        }

        #endregion
    }
}
