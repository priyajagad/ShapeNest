namespace StarterKit.StateMachine
{
    using UnityEngine;

    public class FSMController : Singleton<FSMController>
    {
        BaseState currentState;

        // Method to transition to a new state
        public void TransitionToState(BaseState nextState)
        {
            if (currentState != null)
            {
                currentState.Exit();
            }

            currentState = nextState;

            if (currentState != null)
            {
                currentState.Enter();
            }
        }
    }
}
