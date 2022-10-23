using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URC.Surfaces
{
    /// <summary>
    /// The base for all classes that describes custom properties of a surface.
    /// </summary>
    public abstract class CustomSurfaceProperties : MonoBehaviour
    {
        #region Overrides
        public virtual void OnSurfaceEnter(Collision collision) { }
        public virtual void OnSurfaceStay(Collision collision) { }
        public virtual void OnSurfaceExit(Collision collision) { }
        #endregion

        #region Collision events
        private void OnCollisionEnter(Collision collision)
        {
            
        }

        private void OnCollisionStay(Collision collision)
        {
            
        }

        private void OnCollisionExit(Collision collision)
        {
            
        }
        #endregion
    }
}