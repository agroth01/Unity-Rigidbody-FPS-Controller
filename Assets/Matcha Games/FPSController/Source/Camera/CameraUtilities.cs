using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URC.Camera
{
    /// <summary>
    /// Contains various utility functions for the camera.
    /// Unlike the controller, this script should sit on same object as the camera.
    /// </summary>
    public class CameraUtilities : MonoBehaviour
    {
        [Header("Cursor")]
        [Tooltip("Should the cursor automatically be hidden when the game starts?")]
        public bool m_hideCursorOnAwake;

        [Header("FOV")]
        [Tooltip("The default FOV of the camera that will be used when resetting. Will use the current FOV if left at 0.")]
        public float m_defaultFov;

        private UnityEngine.Camera m_camera;
        private Coroutine m_fovCoroutine;

        private void Awake()
        {
            // hides cursor automatically if toggled
            if (m_hideCursorOnAwake) HideCursor();

            // Cache values
            m_camera = GetComponent<UnityEngine.Camera>();
            if (m_defaultFov == 0)
                m_defaultFov = m_camera.fieldOfView;
            else
                m_camera.fieldOfView = m_defaultFov;
        }

        #region Cursor
        /// <summary>
        /// Shows the cursor and unlocks it from screen
        /// </summary>
        public void ShowCursor()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>
        /// Hides the cursor and locks it to the screen
        /// </summary>
        public void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>
        /// Toggles the visibility of the cursor
        /// </summary>
        public void ToggleCursor()
        {
            Cursor.visible = !Cursor.visible;
            Cursor.lockState = (Cursor.lockState == CursorLockMode.None) ? CursorLockMode.Locked : CursorLockMode.None;
        }
        #endregion

        #region FOV

        /// <summary>
        /// Changes the FOV of the camera to the given value
        /// </summary>
        /// <param name="value">The fov to move to</param>
        /// <param name="time">The time to move to FOV</param>
        public void SetFov(float value, float time)
        {
            InternalSetFov(value, time);
        }

        /// <summary>
        /// Resets the FOV back to original value
        /// </summary>
        /// <param name="time">How fast to reset</param>
        public void ResetFov(float time)
        {
            InternalSetFov(m_defaultFov, time);
        }

        /// <summary>
        /// Internal method for starting a coroutine to change the FOV.
        /// Will cancel the current one if active.
        /// </summary>
        private void InternalSetFov(float v, float t)
        {
            // Stop in case of already running
            if (m_fovCoroutine != null)
                StopCoroutine(m_fovCoroutine);

            // Start new coroutine
            m_fovCoroutine = StartCoroutine(ChangeFov(v, t));
        }

        /// <summary>
        /// Internal coroutine for changing the FOV over time
        /// </summary>
        private IEnumerator ChangeFov(float v, float t)
        {
            // Cache initial values
            float startFov = m_camera.fieldOfView;
            float endFov = v;
            float time = t;
            float elapsedTime = 0.0f;

            // Loop until we reach the end time
            while (elapsedTime < time)
            {
                m_camera.fieldOfView = Mathf.Lerp(startFov, endFov, (elapsedTime / time));
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            // Explicitly set the value to the end value to prevent any rounding errors
            m_camera.fieldOfView = endFov;
        }

        #endregion
    }
}