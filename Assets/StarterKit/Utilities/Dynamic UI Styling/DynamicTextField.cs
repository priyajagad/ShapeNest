using UnityEngine;
using UnityEngine.UI;

namespace StarterKit.UIKit
{


    [RequireComponent(typeof(Text))]
    public class DynamicTextField : MonoBehaviour
    {
        public UIStyleData CurrentFontData;
        public TextFieldType FieldType;
        Text _text;

        private void Reset()
        {
            _text = GetComponent<Text>();
            UpdateFont();

        }

        public void UpdateFont()
        {
            if (CurrentFontData)
            {
                _text.font = CurrentFontData.UIFontStyle;

                switch (FieldType)
                {
                    case TextFieldType.PRIMARY:
                        _text.color = CurrentFontData.PrimaryColor;
                        _text.fontSize = CurrentFontData.PrimaryFontSize;
                        break;

                    case TextFieldType.SECONDARY:
                        _text.color = CurrentFontData.SecondaryColor;
                        _text.fontSize = CurrentFontData.SecondaryFontSize;
                        break;

                }
            }

        }
    }
}