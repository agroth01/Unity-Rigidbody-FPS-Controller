using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Utility;
using URC.Core;

namespace URC.Camera
{
    /// <summary>
    /// Controls the movement of the camera.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        #region Enums

        /// <summary>
        /// The methods to controll camera movement
        /// </summary>
        public enum CameraControlType
        {
            Mouse,      // Camera is controlled with mouse, like in modern fps games
            Keyboard    // Camera is controller with the keyboard, as seen in old school fps games
        }

        /// <summary>
        /// Options to lock the camera movement to certain axises
        /// </summary>
        public enum AxisLock
        {
            None,       // No restrictions
            Vertical,   // Camera can only move up and down
            Horizontal, // Camera can only look to the sides
            Full        // Camera cannot move.
        }

        #endregion

        #region Subclasses

        /// <summary>
        /// Additional settings for the camera that does not need to be changed often
        /// </summary>
        [System.Serializable]
        public class AdvancedCameraSettings
        {
            [Header("Individual multipliers")]
            [Tooltip("Multiplier for the mouse sensitivity horizontally")]
            public float HorizontalMultiplier = 1.0f;
            [Tooltip("Multiplier for the mouse sensitivity vertically")]
            public float VerticalMultiplier = 1.0f;

            [Header("Others")]
            [Tooltip("Restricts the camera movement to certain axises")]
            public AxisLock m_axisLock = AxisLock.None;
        }

        #endregion

        #region Public variables

        [Header("General settings")]
        [Tooltip("The player transform")]
        public Transform m_player;
        [Tooltip("How the camera should be controlled")]
        public CameraControlType m_controlType;
        [Tooltip("Where should the camera be located in relation to the player. 0 = bottom of the player, 1 = top of the player")]
        [Range(0, 1)]
        public float m_headHeight;

        [Header("Look settings")]
        [Tooltip("The sensitivity to input")]
        public float m_sensitivity;
        [Tooltip("The maximum angle the camera can look up and down")]
        public float m_maxViewAngle;
        [Tooltip("Should the vertical input be inverted?")]
        public bool m_inverted;

        [Header("Others")]
        [Tooltip("Optional settings that does not need to be changed by default")]
        public AdvancedCameraSettings m_advancedSettings;

        #endregion

        #region Private variables

        // Tracking
        private Vector2 m_rotation;

        // References
        private UnityEngine.Camera m_camera;
        private CapsuleCollider m_collider;

        #endregion

        #region Unity methods

        private void Awake()
        {
            VerifyCorrectSetup();
        }

        private void LateUpdate()
        {
            UpdateRotation();
            UpdatePosition();
        }

        private void FixedUpdate()
        {
            UpdatePlayerRotation();
        }

        #endregion

        #region Rotation

        /// <summary>
        /// Rotates the camera based on input
        /// </summary>
        private void UpdateRotation()
        {
            // Get the input and clamp vertically
            m_rotation += GetRotation();
            m_rotation.y = Mathf.Clamp(m_rotation.y, -m_maxViewAngle, m_maxViewAngle);

            // Set new rotation. Note the invertion of the x and y axis
            transform.localRotation = Quaternion.Euler(m_rotation.y, m_rotation.x, 0);
        }

        /// <summary>
        /// Gets the desired rotation from chosen input method
        /// </summary>
        /// <returns>The rotation</returns>
        private Vector2 GetRotation()
        {
            Vector3 rotation = Vector3.zero;
            
            // Gather input through the mouse
            if (m_controlType == CameraControlType.Mouse)
            {
                rotation = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            }
            
            // Gather input from the keyboard (arrow keys)
            else if (m_controlType == CameraControlType.Keyboard)
            {
                float x = (Input.GetKey(KeyCode.RightArrow) ? 1 : 0) + (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0);
                float y = (Input.GetKey(KeyCode.UpArrow) ? 1 : 0) + (Input.GetKey(KeyCode.DownArrow) ? -1 : 0);
                rotation = new Vector2(x, y);
            }

            // Scale input by sensitivity
            rotation.x *= m_sensitivity * m_advancedSettings.HorizontalMultiplier;
            rotation.y *= m_sensitivity * m_advancedSettings.VerticalMultiplier;

            // Invert y if needed
            rotation.y = (m_inverted) ? rotation.y : -rotation.y;

            // Return based on axis lock
            if (m_advancedSettings.m_axisLock == AxisLock.Full)
            {
                return Vector2.zero;
            }

            else if (m_advancedSettings.m_axisLock == AxisLock.Vertical)
            {
                return new Vector2(0.0f, rotation.y);
            }

            else if (m_advancedSettings.m_axisLock == AxisLock.Horizontal)
            {
                return new Vector2(rotation.x, 0.0f);
            }

            else
            {
                return rotation;
            }              
        }

        /// <summary>
        /// Updates the rotation of the player to match that of the camera from fixed update
        /// to make the physics system happy.
        /// </summary>
        private void UpdatePlayerRotation()
        {
            m_player.localRotation = Quaternion.Euler(0.0f, m_rotation.x, 0.0f);
        }

        #endregion

        #region Position

        /// <summary>
        /// Updates the position of the camera
        /// </summary>
        private void UpdatePosition()
        {
            Vector3 newPos = m_player.position + GetScaledOffset();
            transform.position = newPos;
        }

        /// <summary>
        /// Determines the offset from center of player transform, accounting for collider size and player size
        /// </summary>
        /// <returns>Offset from center</returns>
        private Vector3 GetScaledOffset()
        {
            Vector3 offset = new Vector3(0.0f, Mathf.Lerp(-m_collider.height / 2.0f, m_collider.height / 2.0f, m_headHeight), 0.0f);
            return offset * m_player.localScale.y;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Makes sure the controller is set up correctly and disable it if not.
        /// </summary>
        private void VerifyCorrectSetup()
        {
            // Check that player is set. If not, try to find motor component in scene.
            if (m_player == null)
            {
                m_player = FindObjectOfType<Motor>().transform;
                if (m_player == null)
                {
                    Logging.Log("Player has not been assigned in the camera controller, and no object with a motor component was found in the scene. Disabling camera controller.", LoggingLevel.Critical);
                    this.enabled = false;
                }
            }

            // Make sure the camera is not on this gameObject
            if (GetComponent<UnityEngine.Camera>())
            {
                Logging.Log(this.ClassName() + " is on the same object as the camera. It should be in the parent of the camera in a seperate holder.", LoggingLevel.Critical);
                this.enabled = false;
            }

            // Check that there is a camera on child object.
            m_camera = GetComponentInChildren<UnityEngine.Camera>();
            if (m_camera == null)
            {
                Logging.Log(this.ClassName() + " could not find a camera on a child object. Camera object should be a child of object with this class.", LoggingLevel.Critical);
                this.enabled = false;
            }

            // Get the collider
            m_collider = m_player.GetComponent<CapsuleCollider>();
            if (m_collider == null)
            {
                Logging.Log(this.ClassName() + " could not find a capsule collider on the player object. The player object should have a capsule collider.", LoggingLevel.Critical);
                this.enabled = false;
            }

            // Success message :)
            Logging.Log(this.ClassName() + " setup verified.", LoggingLevel.Dev);
        }

        #endregion
    }
}
