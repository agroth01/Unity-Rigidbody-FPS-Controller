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

        public virtual void OnEnable()
        {
            Motor.RegisterModule(this);
        }

        public virtual void OnDisable()
        {
            Motor.UnregisterModule(this);
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