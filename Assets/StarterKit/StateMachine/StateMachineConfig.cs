using UnityEngine;

namespace StarterKit.StateMachine
{
    [CreateAssetMenu(fileName = "StateMachineConfig", menuName = "StateMachine/Config", order = 1)]
    public class StateMachineConfig : ScriptableObject
    {
        public string[] stateNames;
    }
}
