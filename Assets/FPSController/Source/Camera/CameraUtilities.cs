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

        private void Awake()
        {
            // hides cursor automatically if toggled
            if (m_hideCursorOnAwake) HideCursor();
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
    }
}