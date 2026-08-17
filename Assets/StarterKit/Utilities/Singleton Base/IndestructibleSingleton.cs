using UnityEngine;

namespace StarterKit
{
    public class IndestructibleSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T instance { get; private set; }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this as T;
                DontDestroyOnLoad(this);
                OnAwake();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public virtual void OnAwake()
        {

        }
    }
}