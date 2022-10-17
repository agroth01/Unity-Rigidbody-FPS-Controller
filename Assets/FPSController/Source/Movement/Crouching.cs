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
        [Tooltip("How much the movement speed should be reduced when crouching. Will only work if the movement module is also present.")]
        [Range(0, 1)]
        public float m_speedReduction;
        [Tooltip("How fast the transition between the two settings should be.")]
        public float m_speedChangeTime;

        #endregion

        #region Private variables

        // Size change
        private float m_targetSize;
        private float m_originalSize;
        private bool m_changedThisFrame;    // Used to prevent the size from being changed multiple times in one frame

        // Flags
        private bool m_isCrouching;

        // Components
        private CapsuleCollider m_collider;
        private Movement m_movement;

        #endregion

        #region Unity methods

        public override void Awake()
        {
            base.Awake(); // Make sure we find motor

            VerifyCorrectSetup();

            // Find modules
            m_movement = GetComponent<Movement>();
        }

        private void Start()
        {
            // Set target size as original size
            m_originalSize = m_collider.height;
            m_targetSize = m_originalSize;
        }

        private void OnEnable()
        {
            Motor.OnGroundExit += OnGroundExit;
        }

        private void OnDisable()
        {
            Motor.OnGroundExit -= OnGroundExit;
        }

        private void Update()
        {
            // Start crouch if possible
            if (Input.GetKey(KeyCode.LeftControl) && !m_isCrouching)
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
        }

        /// <summary>
        /// Called every frame while player is crouching
        /// </summary>
        private void InCrouch()
        {
            // Check for release of input. Note that we do not flag as not crouching here, as resizing might be blocked.
            bool stop = (m_isToggle) ? Input.GetKeyDown(KeyCode.LeftControl) : Input.GetKeyUp(KeyCode.LeftControl);
            if (stop && !m_changedThisFrame)
            {
                // Reset the target size.
                m_targetSize = m_originalSize;
            }
        }

        /// <summary>
        /// Final method to be called when player stops crouching
        /// </summary>
        private void CrouchEnd()
        {
            // Set flag
            m_isCrouching = false;
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
            newPosition += Motor.HorizontalVelocity * Time.deltaTime;
            return newPosition;
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

        #region Events

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