using UnityEngine;
using UnityEngine.UI;

namespace StarterKit.UIKit.UIEffects
{
    /// <summary>
    /// Toggles greyscale effect on all UI elements under this component in the hierarchy.
    /// </summary>
    public class GreyscaleUIHierarchy : MonoBehaviour
    {
        [SerializeField] private Material greyscaleMaterial; // Assign the greyscale material in the inspector
        private bool isGreyscaleActive = false;
        private Graphic[] uiGraphics;

        public bool IsGreyscaleActive => isGreyscaleActive;

        private void Awake()
        {
            uiGraphics = GetComponentsInChildren<Graphic>(true);
        }

        /// <summary>
        /// Toggles greyscale on all UI elements under this component in the hierarchy.
        /// </summary>
        public void ToggleGreyscale(bool value)
        {
            isGreyscaleActive = value;
            ApplyGreyscaleToHierarchy(value);
        }

        /// <summary>
        /// Applies or removes the greyscale effect to the given transform's hierarchy.
        /// </summary>
        /// <param name="applyGreyscale">Should the greyscale effect be applied or removed?</param>
        private void ApplyGreyscaleToHierarchy(bool applyGreyscale)
        {
            if (uiGraphics == null)
                uiGraphics = GetComponentsInChildren<Graphic>(true);
            else
            {
                foreach (var graphic in uiGraphics)
                {
                    if (graphic == null)
                    {
                        uiGraphics = GetComponentsInChildren<Graphic>(true);
                        break;
                    }
                }
            }
            foreach (var graphic in uiGraphics)
            {
                if (graphic.GetComponent<IgnoreGreyscaleObject>() != null) continue;
                graphic.material = applyGreyscale ? greyscaleMaterial : null;
            }
        }
    }
}