using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URC.Utility
{
    /// <summary>
    /// Functions to easily get status of input
    /// </summary>
    public static class InputHelper
    {
        /// <summary>
        /// Checks if any the input keys for moving on the horizontal plane is pressed
        /// </summary>
        /// <returns></returns>
        public static bool DesiresMove()
        {
            return DesiredDirection() != Vector3.zero;
        }

        /// <summary>
        /// Returns the input vector for moving on the horizontal plane
        /// </summary>
        /// <returns></returns>
        public static Vector3 DesiredDirection()
        {
            Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
            return input;
        }
    }
}