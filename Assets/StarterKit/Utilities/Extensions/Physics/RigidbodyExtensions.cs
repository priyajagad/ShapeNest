using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace StarterKit.PhysicsUtilities
{

    public static class RigidbodyExtensions
    {
        public static Vector3 CalculateInitialVelocityWithDrag(this Rigidbody rb, Vector3 targetPosition, float timeOfFlight, float drag, float adjustmentFactor = 1.04f)
        {
            Vector3 startPosition = rb.position;
            Vector3 planarStart = new Vector3(startPosition.x, 0, startPosition.z);
            Vector3 planarTarget = new Vector3(targetPosition.x, 0, targetPosition.z);

            float planarDistance = Vector3.Distance(planarStart, planarTarget);
            float heightDistance = targetPosition.y - startPosition.y;

            float gravity = Physics.gravity.y;

            float expDragTime = Mathf.Exp(-drag * timeOfFlight);
            float velocityXZ = planarDistance * drag / (1 - expDragTime);
            float velocityY = (heightDistance - (0.5f * gravity * timeOfFlight * timeOfFlight)) / timeOfFlight;

            Vector3 direction = (planarTarget - planarStart).normalized;

            Vector3 initialVelocity = new Vector3(
                direction.x * velocityXZ * adjustmentFactor,
                velocityY * adjustmentFactor,
                direction.z * velocityXZ * adjustmentFactor
            );

            return initialVelocity;
        }

        // Method to calculate future positions considering gravity, drag, and mass
        public static void GetTrajectory(this Rigidbody rb, out List<Vector3> trajectoryPoints, out List<float> trajectoryTimes, float maxTimeForTrajectory = 3, float groundLevel = 0.0f)
        {
            trajectoryPoints = new List<Vector3>();
            trajectoryTimes = new List<float>();

            Vector3 position = rb.position;
            Vector3 velocity = rb.linearVelocity;
            Vector3 gravity = Physics.gravity;

            float drag = rb.linearDamping;
            float mass = rb.mass;

            float timeStep = Time.fixedDeltaTime;
            int iterations = Mathf.CeilToInt(maxTimeForTrajectory / timeStep);

            float totalTime = 0;

            for (int i = 0; i < iterations; i++)
            {
                // Apply gravity
                velocity += gravity * timeStep;

                // Apply drag
                float dragFactor = 1.0f - (drag * timeStep / mass);
                velocity *= dragFactor;

                // Update position
                position += velocity * timeStep;

                totalTime += timeStep;

                // Break the loop if the Y-coordinate is non-positive
                if (position.y <= groundLevel)
                {
                    break;
                }

                trajectoryPoints.Add(position);
                trajectoryTimes.Add(totalTime);
            }
        }

        public static void GetTrajectoryWithBounces(this Rigidbody rb, out List<Vector3> trajectoryPoints, out List<float> trajectoryTimes, int numberOfBounces, float maxTimeForTrajectory = 3, float groundLevel = 0.0f)
        {
            trajectoryPoints = new List<Vector3>();
            trajectoryTimes = new List<float>();

            Vector3 position = rb.position;
            Vector3 velocity = rb.linearVelocity;
            Vector3 gravity = Physics.gravity;

            float drag = rb.linearDamping;
            float mass = rb.mass;

            float timeStep = Time.fixedDeltaTime;
            int iterations = Mathf.CeilToInt(maxTimeForTrajectory / timeStep);

            float totalTime = 0;
            int currentBounces = 0;

            for (int i = 0; i < iterations; i++)
            {
                // Apply gravity
                velocity += gravity * timeStep;

                // Apply drag
                float dragFactor = 1.0f - (drag * timeStep / mass);
                velocity *= dragFactor;

                // Update position
                position += velocity * timeStep;

                totalTime += timeStep;

                // Check if the Y-coordinate is at or below the ground level
                if (position.y <= groundLevel)
                {
                    currentBounces++;
                    // Invert Y velocity to simulate a bounce
                    velocity.y = -velocity.y;

                    // Stop predicting if we've reached the desired number of bounces
                    if (currentBounces >= numberOfBounces)
                    {
                        break;
                    }

                    // Adjust position to be on the ground after the bounce
                    position.y = groundLevel;
                }

                trajectoryPoints.Add(position);
                trajectoryTimes.Add(totalTime);
            }
        }

        public static float GetMaxHeightAfterFirstBounce(this Rigidbody rb, float bounceFactor, float groundLevel = 0.0f, float maxTimeForCalculation = 3)
        {
            Vector3 position = rb.position;
            Vector3 velocity = rb.linearVelocity;
            Vector3 gravity = Physics.gravity;

            float drag = rb.linearDamping;
            float mass = rb.mass;

            float timeStep = Time.fixedDeltaTime;
            int iterations = Mathf.CeilToInt(maxTimeForCalculation / timeStep);

            bool firstBounceOccurred = false;
            float maxHeightAfterFirstBounce = float.MinValue;

            for (int i = 0; i < iterations; i++)
            {
                // Apply gravity
                velocity += gravity * timeStep;

                // Apply drag
                float dragFactor = 1.0f - (drag * timeStep / mass);
                velocity *= dragFactor;

                // Update position
                position += velocity * timeStep;

                // Check if the Y-coordinate is at or below the ground level
                if (position.y <= groundLevel)
                {
                    if (!firstBounceOccurred)
                    {
                        firstBounceOccurred = true;

                        // Invert Y velocity to simulate a bounce, considering the bounce factor
                        velocity.y = -velocity.y * bounceFactor;

                        // The new velocity after the bounce will determine the max height
                        maxHeightAfterFirstBounce = (velocity.y * velocity.y) / (2 * -gravity.y);
                    }
                    else
                    {
                        break; // Exit after the first bounce has occurred and max height is calculated
                    }
                }
            }

            // If no bounce occurred, return 0
            return firstBounceOccurred ? maxHeightAfterFirstBounce : 0;
        }



        public static void GetTrajectory(this Rigidbody rb, out List<Vector3> trajectoryPoints, out List<float> trajectoryTimes, Vector3 swingDirection, float swingIntensity, float swingDuration, float maxTimeForTrajectory = 3, float groundLevel = 0.0f)
        {
            trajectoryPoints = new List<Vector3>();
            trajectoryTimes = new List<float>();

            Vector3 position = rb.position;
            Vector3 velocity = rb.linearVelocity;
            Vector3 gravity = Physics.gravity;

            float drag = rb.linearDamping;
            float mass = rb.mass;

            float timeStep = Time.fixedDeltaTime;
            int iterations = Mathf.CeilToInt(maxTimeForTrajectory / timeStep);

            float totalTime = 0;

            // Variables to apply the swing effect
            float elapsedTime = 0f;
            Vector3 swingForce = swingDirection * swingIntensity;

            for (int i = 0; i < iterations; i++)
            {
                // Apply gravity
                velocity += gravity * timeStep;

                // Apply swing effect if within swing duration
                if (elapsedTime < swingDuration)
                {
                    float forceMagnitude = Mathf.Lerp(1f, 0f, elapsedTime / swingDuration);
                    velocity += (swingForce * forceMagnitude * timeStep / mass);
                    elapsedTime += timeStep;
                }

                // Apply drag
                float dragFactor = 1.0f - (drag * timeStep / mass);
                velocity *= dragFactor;

                // Update position
                position += velocity * timeStep;

                totalTime += timeStep;

                // Break the loop if the Y-coordinate is non-positive
                if (position.y <= groundLevel)
                {
                    break;
                }

                trajectoryPoints.Add(position);
                trajectoryTimes.Add(totalTime);
            }
        }

        public static Vector3 PredictFirstBouncePoint(this Rigidbody rb, List<Vector3> points, float groundLevel = 0.0f, float tolerance = 0.1f)
        {
            foreach (Vector3 point in points)
            {
                if (Mathf.Abs(point.y - groundLevel) <= tolerance)
                {
                    return new Vector3(point.x, groundLevel, point.z);
                }
            }

            return points.LastOrDefault();
        }

        public static Vector3 PredictDirectionBeforeBounce(this Rigidbody rb, List<Vector3> points, float groundLevel = 0.0f, float tolerance = 0.1f)
        {
            for (int i = 1; i < points.Count; i++)
            {
                if (Mathf.Abs(points[i].y - groundLevel) <= tolerance)
                {

                    return (points[i] - points[i - 1]).normalized;
                }
            }

            return rb.linearVelocity.normalized;
        }

        //public static Vector3 PredictFirstBouncePoint(this Rigidbody rb, List<Vector3> points, float groundLevel = 0.0f)
        //{
        //    Vector3 closestPoint = points[0];
        //    float smallestDistance = Mathf.Abs(points[0].y - groundLevel);

        //    foreach (Vector3 point in points)
        //    {
        //        float distanceToGround = Mathf.Abs(point.y - groundLevel);
        //        if (distanceToGround < smallestDistance)
        //        {
        //            smallestDistance = distanceToGround;
        //            closestPoint = point;
        //        }
        //    }

        //    return new Vector3(closestPoint.x, groundLevel, closestPoint.z);
        //}
    }
}
