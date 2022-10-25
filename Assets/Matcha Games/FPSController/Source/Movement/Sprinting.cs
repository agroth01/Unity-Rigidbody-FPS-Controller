using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Camera;
using URC.Utility;

namespace URC.Movement
{
    /// <summary>
    /// This module increases the movement speed of the player.
    /// </summary>
    public class Sprinting : Module
    {
        #region Public variables

        [Header("Activation")]
        [Tooltip("Key to start sprint")]
        public KeyCode m_sprintingKey;
        [Tooltip("Should the sprinting be a toggle or hold to sprint?")]
        public bool m_isToggle;
        [Tooltip("Minimum amount of time between sprinting.")]
        public float m_sprintStartDelay;
        [Tooltip("Can sprinting start while in the air?")]
        public bool m_mustStartGrounded;
        
        [Header("Sprinting settings")]        
        [Tooltip("Multiplier for movement speed of motor.")]
        public float m_speedMultiplier;
        [Tooltip("Should sprinting stop when becoming airborne?")]
        public bool m_requireGrounding;
        [Tooltip("Must the player be moving to sprint?")]
        public bool m_requireMovement;

        [Header("Camera settings")]
        [Tooltip("The FOV of the camera when sprinting. Set to 0 to disable")]
        public float m_sprintingFov;
        [Tooltip("How quickly the FOV should change.")]
        public float m_FovChangeSpeed;
        [Tooltip("How quickly the FOV will reset to original value")]
        public float m_FovResetSpeed;

        #endregion

        #region Private variables

        // Sprinting variables
        private bool m_isSprinting;
        private float m_sprintPreventionTimer;
        private bool m_startedThisFrame;

        // Components
        private CameraUtilities m_cameraUtils;

        #endregion

        #region Unity Methods

        public override void Awake()
        {
            base.Awake();

            // Attempt to find camera utils
            m_cameraUtils = UnityEngine.Camera.main.GetComponent<CameraUtilities>();
            if (m_cameraUtils == null && m_sprintingFov != 0)
            {
                // Throw error if sprinting modifies FOV but no camera utils are found
                Logging.Log("Sprinting FOV is set to a value other than 0, but no CameraUtilities component was found on the main camera. Sprinting will not affect FOV.", LoggingLevel.Critical);
                m_sprintingFov = 0.0f;
            }
        }

        private void Update()
        {
            // Decrement timer as long as we aren't sprinting
            if (m_sprintPreventionTimer > 0.0f && !m_isSprinting)
            {
                m_sprintPreventionTimer -= Time.deltaTime;
            }

            // Check for sprint input
            if (Input.GetKey(m_sprintingKey) && !m_isSprinting)
            {
                AttemptSprintStart();
            }

            // Update sprinting
            if (m_isSprinting) UpdateSprinting();
        }

        #endregion

        #region Sprinting

        /// <summary>
        /// Checks the requirements of sprinting and starts sprinting if possible.
        /// </summary>
        private void AttemptSprintStart()
        {
            // Is the player grounded?
            if ((m_mustStartGrounded && !Motor.Grounded) || (m_requireGrounding && !Motor.Grounded))
                return;

            // Has the minimum waiting delay been met?
            if (m_sprintPreventionTimer > 0.0f)
                return;

            // Is the player moving?
            if (m_requireMovement && (!InputHelper.DesiresMove() || !(Motor.HorizontalSpeed > 0.0f)))
                return;

            // Sprinting is valid
            StartSprint();
        }

        /// <summary>
        /// Starts the sprinting process
        /// </summary>
        private void StartSprint()
        {
            // Flag
            m_isSprinting = true;

            // Update speed of motor
            Motor.ModifySpeedMultiplier(m_speedMultiplier, Motor.ModifyType.Additive);

            // Prevent toggle from resetting instantly
            m_startedThisFrame = true;

            // Update camera if applicable
            if (m_sprintingFov != 0f)
            {
                m_cameraUtils.SetFov(m_sprintingFov, m_FovChangeSpeed);
            }
        }

        /// <summary>
        /// Logic for sprinting. Is responsible for checking for stopping conditions of sprint
        /// </summary>
        private void UpdateSprinting()
        {
            // Make sure motor is still grounded
            if (m_requireGrounding && !Motor.Grounded) StopSprint();

            // Make sure player is still moving
            if (m_requireMovement && !InputHelper.DesiresMove()) StopSprint();

            // Check for sprint input
            bool desiresStop = (m_isToggle) ? Input.GetKeyDown(m_sprintingKey) && !m_startedThisFrame : !Input.GetKey(m_sprintingKey);
            if (desiresStop) StopSprint();

            // We no longer started this frame
            if (m_startedThisFrame)
                m_startedThisFrame = false;
        }

        private void StopSprint()
        {
            // Flag
            m_isSprinting = false;

            // Reset speed of motor
            Motor.ModifySpeedMultiplier(-m_speedMultiplier, Motor.ModifyType.Additive);

            // Reset camera if applicable
            if (m_sprintingFov != 0f)
            {
                m_cameraUtils.ResetFov(m_FovResetSpeed);
            }

            // Set timer
            m_sprintPreventionTimer = m_sprintStartDelay;
        }
    }

    #endregion
}