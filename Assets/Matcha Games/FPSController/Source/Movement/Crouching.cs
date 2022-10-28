using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Utility;

namespace URC.Movement
{
    /// <summary>
    /// This class handles the crouching of the player.
    /// </summary>
    public class Crouching : Module
    {
        #region Public variables

        [Header("General")]
        [Tooltip("What key should start crouching?")]
        public KeyCode m_crouchKey;
        [Tooltip("Should the crouching be a toggle or a hold?")]
        public bool m_isToggle;
        [Tooltip("Does the player have to be grounded to crouch?")]
        public bool m_mustStartOnGround;
        [Tooltip("Should crouch be stopped when leaving ground (i.e. jumping)?")]
        public bool m_stopOnGroundLeave;

        [Header("Size")]
        [Range(0, 1)]
        [Tooltip("The reduction in size of the collider of the player.")]
        public float m_sizeReduction;
        [Tooltip("How fast the player should shrink.")]
        public float m_shrinkingSpeed;
        [Tooltip("How fast the player should grow")]
        public float m_growthSpeed;

        [Header("Movement")]
        [Tooltip("How much the movement speed should be reduced when crouching.")]
        [Range(0, 1)]
        public float m_speedReduction;

        #endregion

        #region Private variables

        // Size change
        private float m_targetSize;
        private float m_originalSize;
        private bool m_changedThisFrame;    // Used to prevent the size from being changed multiple times in one frame

        // Flags
        private bool m_isCrouching;
        private bool m_speedModified;   // Track when the speed of the motor has been changed by this module

        // Components
        private CapsuleCollider m_collider;

        #endregion

        #region Events

        public event Action OnCrouchStart;
        public event Action OnCrouchEnd;

        #endregion

        #region Unity methods

        public override void Awake()
        {
            base.Awake(); // Make sure we find motor
            VerifyCorrectSetup();
        }

        private void Start()
        {
            // Set target size as original size
            m_originalSize = m_collider.height;
            m_targetSize = m_originalSize;
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Motor.OnGroundExit += OnGroundExit;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            Motor.OnGroundExit -= OnGroundExit;
        }

        private void Update()
        {
            // Start crouch if possible
            if (Input.GetKey(m_crouchKey) && !m_isCrouching)
            {
                // Add this for toggle to work
                m_changedThisFrame = true;

                // Make sure we are on ground if needed
                if (m_mustStartOnGround && Motor.Grounded)
                    CrouchStart();

                // If we don't need to be grounded, just crouch
                else if (!m_mustStartOnGround)
                    CrouchStart();
            }

            else
            {
                m_changedThisFrame = false;
            }

            // Update crouch if needed
            if (m_isCrouching) InCrouch();

            // Always move towards target size
            Resizing();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Makes sure that we have a reference to the player collider.
        /// Otherwise throw log message and disable.
        /// </summary>
        private void VerifyCorrectSetup()
        {
            m_collider = FindCollider();
            if (m_collider == null)
            {
                Logging.Log(this.ClassName() + " could not find a capsule collider for player. Disabling.", LoggingLevel.Critical);
                this.enabled = false;
            }
        }

        #endregion

        #region Crouching logic

        /// <summary>
        /// Called when player is starting to crouch
        /// </summary>
        private void CrouchStart()
        {
            // Determine target size and speed
            m_targetSize = m_collider.height * m_sizeReduction;

            // flag that we are crouching
            m_isCrouching = true;

            // If already grounded, force grounding if not on even ground
            if (Motor.Grounded && !Motor.EvenGround)
            {
                Motor.ForceGrounded(0.25f);
            }

            // Call event
            OnCrouchStart?.Invoke();
        }

        /// <summary>
        /// Called every frame while player is crouching
        /// </summary>
        private void InCrouch()
        {
            // Check for release of input. Note that we do not flag as not crouching here, as resizing might be blocked.
            bool stop = (m_isToggle) ? Input.GetKeyDown(m_crouchKey) : Input.GetKeyUp(m_crouchKey);
            if (stop && !m_changedThisFrame)
            {
                // Reset the target size.
                m_targetSize = m_originalSize;
            }
        }

        /// <summary>
        /// Called once when the size is at crouching size.
        /// </summary>
        private void OnFullyCrouched()
        {
            // Set the movement speed to crouching speed
            Motor.ModifySpeedMultiplier(m_speedReduction, Motor.ModifyType.Multiplicative);
            m_speedModified = true;
        }

        /// <summary>
        /// Final method to be called when player stops crouching
        /// </summary>
        private void CrouchEnd()
        {
            // Set flag
            m_isCrouching = false;

            // Call event
            OnCrouchEnd?.Invoke();

            // Reset the movement speed
            if (m_speedModified)
            {
                Motor.ModifySpeedMultiplier(1 / m_speedReduction, Motor.ModifyType.Multiplicative);
                m_speedModified = false;
            }
        }

        #endregion

        #region Resizing

        /// <summary>
        /// Attempts to move towards the target size.
        /// </summary>
        private void Resizing()
        {
            // Ignore if already at target size
            if (m_collider.height == m_targetSize) return;

            // Determine if we are shrinking or growing
            bool shrinking = m_originalSize > m_targetSize;

            // Check if we are blocked from growing
            if (!shrinking && Motor.IsBlocked())
            {
                return;
            }

            // Calculate speed
            float speed = (shrinking) ? m_shrinkingSpeed : m_growthSpeed;

            // Calculate and clamp new size
            float newHeight = Mathf.MoveTowards(m_collider.height, m_targetSize, speed * Time.deltaTime);
           
            // Set new size
            m_collider.height = newHeight;

            // Update position of player
            Motor.transform.position = GetUpdatedPosition(newHeight, shrinking);

            // Call crouch end if we are done
            if (!shrinking && m_collider.height == m_targetSize)
            {
                CrouchEnd();
            }

            // Notify if we are fully crouched
            if (shrinking && m_collider.height == m_targetSize)
            {
                OnFullyCrouched();
            }
        }

        /// <summary>
        /// Returns the new position of the player after changing size.
        /// </summary>
        /// <param name="size">The new size of the player</param>
        /// <param name="shrinking">Is player shrinking or growing</param>
        /// <returns>The new position</returns>
        private Vector3 GetUpdatedPosition(float size, bool shrinking)
        {
            // Get the new position
            Vector3 newPosition = Motor.transform.position;

            // Update position normally as long as we are not growing and in air
            if (Motor.Grounded || shrinking)
            {
                newPosition = GetGroundPosition() + (Vector3.up * (size / 2.0f));
            }

            // Account for velocity of rb as we are setting position directly
            Vector3 vel = Motor.HorizontalVelocity * Time.deltaTime;
            if (Motor.Grounded)
                vel = Vector3.ProjectOnPlane(vel, Motor.GroundNormal);
            return newPosition + vel;
        }

        /// <summary>
        /// Finds the ground position from which to resize from.
        /// </summary>
        /// <returns></returns>
        private Vector3 GetGroundPosition()
        {
            // Default value in case we dont find ground
            Vector3 pos = Motor.transform.position + (Vector3.down * (m_collider.height / 2.0f));

            // Send a raycast down to find the ground
            float downDistance = (m_collider.height / 2.0f) + 0.1f;
            if (Physics.Raycast(Motor.transform.position, Vector3.down, out RaycastHit hit, downDistance, Motor.GroundLayers))
            {
                pos = hit.point;
            }

            return pos;
        }

        #endregion

        #region Reactions

        /// <summary>
        /// Subscribed to from motor.
        /// </summary>
        private void OnGroundExit()
        {
            if (m_stopOnGroundLeave)
            {
                m_targetSize = m_originalSize;
            }
        }

        

        #endregion
    }
}