using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Utility;

namespace URC.Camera
{
    /// <summary>
    /// Moves the camera up and down based on the player's movement.
    /// </summary>
    public class Headbob : Module
    {
        #region Structs

        /// <summary>
        /// Struct for storing information about frequency and amplitude with added scaling option.
        /// The only reason to do this is that it looks better in the editor, without having to create a custom editor.
        /// </summary>
        [System.Serializable]
        public struct ScalingSetting
        {
            /// <summary>
            /// How the value should be scaled
            /// </summary>
            public enum ScalingMode
            {
                Linear,        // Value scales linearly
                Exponential    // Value scaled exponentially
            }
            
            [Tooltip("The value of this setting.")]
            public float Value;
            [Header("Scaling")]
            [Tooltip("Does it scale with velocity?")]
            public bool ScalesWithVelocity;
            [Tooltip("How it should scale")]
            public ScalingMode Scaling;
            [Tooltip("Multiplier used when scaling. When exponential, this is the exponent.")]
            public float ScalingMultiplier;
        }

        #endregion

        #region Public variables
        [Header("Headbob settings")]
        [Tooltip("Frequency is the speed at which headbob moves.")]
        public ScalingSetting m_frequency;
        [Tooltip("Amplitude is the strength of the headbob.")]
        public ScalingSetting m_amplitude;

        [Header("Others")]
        [Tooltip("Smoothing between headbob positions. Makes changes in amplitude due to velocity less sudden.")]
        public float m_smoothness = 1.0f;
        [Tooltip("Minimum velocity to start headbobbing. Can prevent headbob from being active when walking into walls.")]
        public float m_minVelocity;
        [Tooltip("How fast camera should move back to original position when not active.")]
        public float m_resetSpeed;
        [Tooltip("Time before the camera should start resetting. By having some delay, switching directions will not reset headbob.")]
        public float m_resetDelay;

        #endregion

        #region Private variables

        // Camera
        private Transform m_camera;

        // Headbob
        private float m_timer;
        private float m_initialY;
        private float m_targetY;
        private float m_resetDelayTimer;

        #endregion

        #region Unity methods
        public override void Awake()
        {
            base.Awake();

            // Get camera
            m_camera = UnityEngine.Camera.main.transform;
            m_initialY = m_camera.localPosition.y;

            // Set timer
            m_timer = 0;
        }

        private void LateUpdate()
        {
            // If we are meeting requirements for headbob, update the headbob
            if (Motor.HorizontalSpeed > m_minVelocity && Motor.Grounded && InputHelper.DesiresMove())
            {
                UpdateHeadbob();
            }

            // Else, move the camera back to its original position
            else
            {
                Resetting();
            }

            // Set camera position
            m_camera.localPosition = new Vector3(m_camera.localPosition.x, m_targetY, m_camera.localPosition.z);
        }

        #endregion

        #region Bobbing

        /// <summary>
        /// Get the position of the headbob based on the current timer, ignoring amplitude scaling.
        /// returns a float between -1 to 1 based on the timer.
        /// </summary>
        /// <returns></returns>
        public float GetNormalizedBobValue()
        {
           return Mathf.Sin(m_timer * GetFrequency());
        }

        private void UpdateHeadbob()
        {
            // Get speed
            float speed = Motor.HorizontalSpeed;

            // Get amplitude
            float amplitude = m_amplitude.Value / 10.0f; // Divide by 10 to make editor number larger
            if (m_amplitude.ScalesWithVelocity)
            {
                amplitude *= GetScaledValue(speed, m_amplitude);
            }

            // Get headbob
            float headbob = Mathf.Sin(m_timer * GetFrequency()) * amplitude;
            m_targetY = Mathf.MoveTowards(m_targetY, headbob, m_smoothness * Time.deltaTime);

            // Increment timer
            m_timer += Time.deltaTime;

            // Set delay
            m_resetDelayTimer = m_resetDelay;
        }

        /// <summary>
        /// Gets the frequency to use
        /// </summary>
        /// <returns></returns>
        private float GetFrequency()
        {
            float frequency = m_frequency.Value;
            if (m_frequency.ScalesWithVelocity)
            {
                frequency *= GetScaledValue(Motor.HorizontalSpeed, m_frequency);
            }
            return frequency;
        }

        private float GetScaledValue(float value, ScalingSetting scalingSetting)
        {
            // Get scaling mode
            ScalingSetting.ScalingMode scalingMode = scalingSetting.Scaling;

            // Get multiplier
            float multiplier = scalingSetting.ScalingMultiplier;

            // Get scaled value
            float scaledValue = 0;
            switch (scalingMode)
            {
                case ScalingSetting.ScalingMode.Linear:
                    scaledValue = value * multiplier;
                    break;
                case ScalingSetting.ScalingMode.Exponential:
                    scaledValue = Mathf.Pow(value, multiplier);
                    break;
            }

            return scaledValue;
        }

        /// <summary>
        /// Attempts to reset the position of the camera after a delay
        /// </summary>
        private void Resetting()
        {
            // Don't proceed while there is a delay
            if (m_resetDelayTimer > 0.0f)
            {
                m_resetDelayTimer -= Time.deltaTime;
                return;
            }

            m_targetY = Mathf.MoveTowards(m_targetY, m_initialY, m_resetSpeed * Time.deltaTime);
            if (Mathf.Abs(m_targetY - m_initialY) < 0.01f)
            {
                m_targetY = m_initialY;
            }

            // Reset timer if at target
            if (m_camera.localPosition.y == m_initialY)
            {
                m_timer = 0;
            }
        }

        #endregion
    }
}