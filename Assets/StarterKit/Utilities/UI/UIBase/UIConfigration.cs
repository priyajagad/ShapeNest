using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace StarterKit.UIKit
{
    [CreateAssetMenu(fileName = "UIConfigrationData", menuName = "ScriptableObjects/UIConfigrationDataScriptableObject", order = 1)]
    public class UIConfigration : ScriptableObject
    {
        [Header("Screens Configration")]
        public bool AddNotchSupport;
    }
}