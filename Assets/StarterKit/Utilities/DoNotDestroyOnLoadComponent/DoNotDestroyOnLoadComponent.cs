using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarterKit.Utilities
{
    public class DoNotDestroyOnLoadComponent : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(this);
        }

    }

}