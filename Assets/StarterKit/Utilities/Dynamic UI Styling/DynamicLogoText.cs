using UnityEngine;
using UnityEngine.UI;
namespace StarterKit.UIKit
{

    public class DynamicLogoText : MonoBehaviour
    {
        public UIStyleData CurrentUIStyleData;
        public TextFieldType FieldType;
        Text _text;

        private void Reset()
        {
            _text = GetComponent<Text>();
            UpdateFont();

        }

        public void UpdateFont()
        {
            if (CurrentUIStyleData)
            {
                _text.font = CurrentUIStyleData.LogoFontStyle;
                _text.color = CurrentUIStyleData.LogoColor;
            }

        }
    }
}