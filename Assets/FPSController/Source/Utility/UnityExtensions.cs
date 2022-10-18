using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URC.Utility
{
    /// <summary>
    /// Contains extension methods for Unity classes.
    /// </summary>
    public static class UnityExtensions
    {
        /// <summary>
        /// Checks if a layer is in a layermask.
        /// </summary>
        /// <param name="mask">The mask to check</param>
        /// <param name="layer">The layer to look for</param>
        /// <returns></returns>
        public static bool Contains(this LayerMask mask, int layer)
        {
            return mask == (mask | (1 << layer));
        }

        /// <summary>
        /// Gets the full name of a class as a string
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ClassName(this object obj)
        {
            string fullName = obj.GetType().ToString();
            return fullName.Split(".")[fullName.Split(".").Length - 1];
        }

    }

}