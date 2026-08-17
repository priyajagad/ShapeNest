using UnityEngine;

namespace StarterKit.StateMachine
{
    public abstract class BaseState : MonoBehaviour
    {
        // Method called when entering the state
        public abstract void Enter();

        // Method called when exiting the state
        public abstract void Exit();
    }
}