using UnityEngine;
using System.Collections.Generic;
using System.Linq;


namespace StarterKit.PhysicsUtilities
{

    public static class Rigidbody2DExtensions
    {
        public static Vector2 CalculateInitialVelocityWithDrag(this Rigidbody2D rb, Vector2 targetPosition, float timeOfFlight, float drag, float adjustmentFactor = 1.0f)
        {
            Vector2 startPosition = rb.position;
            Vector2 planarStart = new Vector2(startPosition.x, 0);
            Vector2 planarTarget = new Vector2(targetPosition.x, 0);

            float planarDistance = Vector2.Distance(planarStart, planarTarget);
            float heightDistance = targetPosition.y - startPosition.y;

            float gravity = Physics2D.gravity.y;

            float expDragTime = Mathf.Exp(-drag * timeOfFlight);
            float velocityX = planarDistance * drag / (1 - expDragTime);
            float velocityY = (heightDistance - (0.5f * gravity * timeOfFlight * timeOfFlight)) / timeOfFlight;

            Vector2 direction = (planarTarget - planarStart).normalized;

            Vector2 initialVelocity = new Vector2(
                direction.x * velocityX * adjustmentFactor,
                velocityY * adjustmentFactor
            );

            return initialVelocity;
        }

        public static List<Vector2> GetBallTrajectoryPoints(this Rigidbody2D rb, float resolutionFactor, float groundLevel = 0.0f, float tolerance = 0.1f)
        {
            List<Vector2> trajectoryPoints = new List<Vector2>();
            Vector2 initialPosition = rb.position;
            Vector2 initialVelocity = rb.linearVelocity;

            float timeStep = resolutionFactor;
            float t = 0;
            bool hasBounced = false;

            while (!hasBounced)
            {
                Vector2 position = CalculateBallPosition(rb, t, initialPosition, initialVelocity);

                if (position.y <= groundLevel + tolerance)
                {
                    position.y = groundLevel;
                    trajectoryPoints.Add(position);
                    hasBounced = true;
                }
                else
                {
                    trajectoryPoints.Add(position);
                    t += timeStep;
                }
            }

            return trajectoryPoints;
        }

        public static Vector2 CalculateBallPosition(this Rigidbody2D rb, float t, Vector2 initialPosition, Vector2 initialVelocity)
        {
            float g = Physics2D.gravity.y;
            float dragCoefficient = rb.linearDamping;
            float mass = rb.mass;

            if (dragCoefficient != 0)
            {
                Vector2 terminalVelocity = mass * Physics2D.gravity / dragCoefficient;
                Vector2 velocityWithDrag = terminalVelocity + (initialVelocity - terminalVelocity) * Mathf.Exp(-dragCoefficient * t / mass);
                Vector2 displacementDueToDrag = terminalVelocity * t + (initialVelocity - terminalVelocity) * mass / dragCoefficient * (1 - Mathf.Exp(-dragCoefficient * t / mass));

                return initialPosition + displacementDueToDrag;
            }
            else
            {
                return initialPosition + initialVelocity * t + 0.5f * Physics2D.gravity * t * t;
            }
        }

        public static Vector2 PredictFirstBouncePoint(this Rigidbody2D rb, List<Vector2> points, float groundLevel = 0.0f, float tolerance = 0.1f)
        {
            foreach (Vector2 point in points)
            {
                if (Mathf.Abs(point.y - groundLevel) <= tolerance)
                {
                    return new Vector2(point.x, groundLevel);
                }
            }

            return points.LastOrDefault();
        }
    }
}