using StarterKit;
using UnityEngine;

namespace StarterKit.CameraEffects
{

    public class CameraShake : Singleton<CameraShake>
    {
        // Parameters for the camera shake
        public float shakeDuration = 0.5f;   // Duration of the shake effect
        public float shakeMagnitude = 0.3f;  // Magnitude of the shake effect
        public float dampingSpeed = 1.0f;    // Speed at which the shake effect diminishes

        private float initialShakeDuration;  // Store the initial shake duration
        private Vector3 initialPosition;     // Store the initial position of the camera

        void Start()
        {
            initialPosition = transform.localPosition;  // Store the initial position
            initialShakeDuration = shakeDuration;       // Store the initial shake duration
        }

        void Update()
        {
            if (shakeDuration > 0)
            {
                // Apply a random offset to the camera's position within the specified magnitude
                transform.localPosition = initialPosition + Random.insideUnitSphere * shakeMagnitude;

                // Decrease the shake duration based on the damping speed
                shakeDuration -= Time.deltaTime * dampingSpeed;
            }
            else
            {
                // Once the shake is done, reset the camera to its original position
                shakeDuration = 0f;
            }
        }

        // Public method to trigger the shake effect from other scripts
        public void TriggerShake(float duration, float magnitude, float damping)
        {
            shakeDuration = duration;
            shakeMagnitude = magnitude;
            dampingSpeed = damping;
            initialPosition = transform.localPosition;  // Reset the initial position
        }

        // Overloaded method to trigger the shake effect using default parameters
        public void TriggerShake()
        {
            shakeDuration = initialShakeDuration;
            initialPosition = transform.localPosition;  // Reset the initial position
        }
    }

}