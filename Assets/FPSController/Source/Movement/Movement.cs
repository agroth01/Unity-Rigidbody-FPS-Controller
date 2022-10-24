using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Utility;

namespace URC.Movement
{
    /// <summary>
    /// This is the module responsible for driving movement on the horizontal plane.
    /// 
    /// It is important to understand that the movement while grounded and in air is fundementally different.
    /// Ground movement is driven by setting velocity directly.
    /// Air movement is driven by adding force to the rigidbody.
    /// </summary>
    public class Movement : Module
    {
        /// <summary>
        /// Settings for how the movement should work
        /// </summary>
        [System.Serializable]
        public struct MovementSettings
        {
            [Tooltip("The speed the motor will reach through it's own acceleration")]
            public float TopSpeed;
            [Tooltip("How fast the motor will reach top speed")]
            public float Acceleration;
            [Tooltip("How fast the motor will stop when no input is given")]
            public float Deceleration;
        }


        [Header("Ground settings")]
        [Tooltip("Settings while motor is grounded")]
        public MovementSettings m_groundSettings;
        

        [Header("Air settings")]
        [Tooltip("Settings while motor is in air")]
        public MovementSettings m_airSettings;

        [Header("Step settings")]
        [Tooltip("The highest step that the controller can step up")]
        public float m_stepHeight;
        [Tooltip("How fast to perform the step.")]
        public float m_stepSpeed;

        // Movement
        private Vector3 m_desiredDirection;

        private void Update()
        {
            // Grab desired input in update to be as up to date as possible
            m_desiredDirection = GetDirection();
        }

        private void FixedUpdate()
        {
            // Process movement in fixed update to comply with physics
            if (Motor.Grounded && !Motor.OnSlope)
            {
                GroundMovement();
            }

            else if (Motor.OnSlope)
            {
                SlopeMovement();
            }

            else
            {
                AirMovement();
            }
        }
        
        /// <summary>
        /// Main logic for movement on the ground.
        /// 
        /// Will set the velocity of the rigidbody directly.
        /// </summary>
        private void GroundMovement()
        {
            // Calculate desired velocity based on direction
            Vector3 desiredVelocity = m_desiredDirection * m_groundSettings.TopSpeed * Motor.SpeedMutliplier;
            desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, Motor.GroundNormal);

            // Determine if we should accelerate or decelerate
            float speedChange = (m_desiredDirection == Vector3.zero) ? m_groundSettings.Deceleration : m_groundSettings.Acceleration;

            // Find new velocity and apply it
            Vector3 newVelocity = Vector3.MoveTowards(Motor.Velocity, desiredVelocity, speedChange);

            Motor.SetVelocity(newVelocity);
        }

        /// <summary>
        /// Main logic for movement on ground when angle is too steep.
        /// Prevents air climbing slopes
        /// </summary>
        private void SlopeMovement()
        {
            // Calculate desired velocity based on direction
            Vector3 force = m_desiredDirection * m_airSettings.Acceleration;

            // Prevent slope climbing
            Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Vector3.up, Motor.GroundNormal), Vector3.up).normalized;
            force = Vector3.ProjectOnPlane(force, perpenticularObstructionNormal);

            Motor.AddForce(force);
        }
        
        /// <summary>
        /// Main logic for movement while in air.
        /// 
        /// In order to allow for external forces to push motor, we do not set the velocity directly,
        /// but instead add forces.
        /// </summary>
        private void AirMovement()
        {
            // Calculate desired velocity based on direction
            Vector3 force = m_desiredDirection * m_airSettings.Acceleration;

            // If we are moving faster than top speed, add force based on how far away from velocity dir we are
            if (Motor.HorizontalSpeed > m_airSettings.TopSpeed)
            {
                // Multiply force by dot
                float dot = Vector3.Dot(m_desiredDirection, Motor.HorizontalVelocity.normalized);
                float multiplier = (1f - dot);

                // If close to 0, make it 0
                if (Mathf.Approximately(multiplier, 0))
                    multiplier = 0;

                // Clamp to 1 in case of moving backwards
                multiplier = Mathf.Clamp(multiplier, 0, 1);

                // Multiply force
                force *= multiplier;

                // Add force to slow down
                if (force != Vector3.zero)
                    force += -Motor.HorizontalVelocity.normalized * m_airSettings.Acceleration * multiplier;
            }

            // Add force
            Motor.AddForce(force);
        }


        /// <summary>
        /// Gets the current direction player wants to move in
        /// </summary>
        /// <returns></returns>
        private Vector3 GetDirection()
        {
            Vector3 worldDir = InputHelper.DesiredDirection().normalized;
            return transform.TransformDirection(worldDir);
        }
    }
}