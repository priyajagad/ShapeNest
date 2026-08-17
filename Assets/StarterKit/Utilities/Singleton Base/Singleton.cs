using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarterKit
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T instance { get; private set; }

        private void Awake()
        {
            if(instance == null)
            {
                instance = this as T;
                OnAwake();
            }
        }

        public virtual void OnAwake()
        {

        }
    }
}