using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Utility;

namespace URC.Core
{
    /// <summary>
    /// Base class for all modules for the controller.
    /// </summary>
    public abstract class Module : MonoBehaviour
    {
        /// <summary>
        /// The motor that this module is attached to.
        /// </summary>
        public Motor Motor { get; private set; }

        public virtual void Awake()
        {
            // Check for motor class and disable if not found.
            if (!GetMotor())
            {
                Logging.Log("Module " + this.ClassName() + " could not find a motor to attach to. Module will be disabled.", LoggingLevel.Critical);
                this.enabled = false;
            }
        }

        /// <summary>
        /// Looks for the capsule collider of the player.
        /// Will return null if no collider is found
        /// </summary>
        /// <returns></returns>
        public CapsuleCollider FindCollider()
        {
            CapsuleCollider collider;

            // Check if it is on this object
            collider = GetComponent<CapsuleCollider>();
            if (collider != null)
            {
                return collider;
            }

            // Check if it is on parent object
            collider = GetComponentInParent<CapsuleCollider>();
            if (collider != null)
            {
                return collider;
            }

            // Default return value
            return collider;
        }

        /// <summary>
        /// Attempts to find a class with the given type on both the object of this
        /// module and parent.
        /// </summary>
        /// <typeparam name="T">The type to find</typeparam>
        /// <returns></returns>
        public T FindClass<T>() where T : Component
        {
            T component;

            // Check if it is on this object
            component = GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            // Check if it is on parent object
            component = GetComponentInParent<T>();
            if (component != null)
            {
                return component;
            }

            // Default return value
            return component;
        }

        /// <summary>
        /// Looks for the motor class on both this gameObject and parent object
        /// </summary>
        private bool GetMotor()
        {
            // Check if this gameObject has a motor
            Motor = GetComponent<Motor>();
            if (Motor != null)
                return true;

            // Check if the parent gameObject has a motor
            Motor = GetComponentInParent<Motor>();
            if (Motor != null)
                return true;

            // No motor was found.
            return false;
        }
    }
}