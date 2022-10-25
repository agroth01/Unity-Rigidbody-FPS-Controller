using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Utility;
using URC.Surfaces;

namespace URC.Core
{
    /// <summary>
    /// This is the central class for connecting all modules of the controller. This is the only
    /// required class that any other module relies on.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class Motor : MonoBehaviour
    {
        #region Enums
        /// <summary>
        /// Different types of methods to modify values
        /// </summary>
        public enum ModifyType
        {
            Additive,       // Add to value
            Multiplicative, // Multiply the value
            Override        // Set the value directly
        }

        #endregion

        #region Subclasses
        /// <summary>
        /// A class containing information and functionality for surfaces that the motor collides with
        /// </summary>
        public class Surface
        {
            /// <summary>
            /// The normal of the surface
            /// </summary>
            public Vector3 Normal { get; private set; }

            /// <summary>
            /// The point where contact between motor and surface was made
            /// </summary>
            public Vector3 Point { get; private set; }

            /// <summary>
            /// The transform of the surface
            /// </summary>
            public Transform Transform { get; private set; }

            /// <summary>
            /// The angle of the normal relative to up in world space
            /// </summary>
            public float Angle
            {
                get { return Vector3.Angle(Vector3.up, Normal); }
            }

            // Store all custom surface properties
            private List<CustomSurfaceProperties> m_properties = new List<CustomSurfaceProperties>();

            /// <summary>
            /// Copies the values of another surface
            /// </summary>
            /// <param name="other">Surface to copy</param>
            public void Copy(Surface other)
            {
                Normal = other.Normal;
                Point = other.Point;
                Transform = other.Transform;
                m_properties = other.m_properties;
            }

            /// <summary>
            /// Returns all custom properties of the surface
            /// </summary>
            /// <returns></returns>
            public List<CustomSurfaceProperties> GetProperties()
            {
                return m_properties;
            }

            /// <summary>
            /// Returns the first custom property of the surface that matches the given type
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <returns></returns>
            public T GetProperties<T>() where T : CustomSurfaceProperties
            {
                if (m_properties.Count == 0) return null;

                for (int i = 0; i < m_properties.Count; i++)
                {
                    if (m_properties[i].GetType() == typeof(T))
                    {
                        return (T)m_properties[i];
                    }
                }
                return null;
            }

            public Surface(Collision collision)
            {
                // Get first contact point
                ContactPoint contact = collision.GetContact(0);

                // Store information
                Normal = contact.normal;
                Point = contact.point;
                Transform = collision.transform;

                // Get all custom surface properties
                m_properties = collision.transform.GetComponents<CustomSurfaceProperties>().ToList();
            }
        }

        /// <summary>
        /// Optional settings for the motor that can be set in the inspector
        /// </summary>
        [System.Serializable]
        public class AdvancedSettings
        {
            public LoggingLevel LoggingLevel = LoggingLevel.Critical;
            [Tooltip("Tries to use more optimization techniques in order to make the controller more performant. Leads to a less smooth experience.")]
            public bool m_useOptimizations;
        }

        #endregion

        #region Public variables

        [Header("Gravity")]
        [Tooltip("Is the motor affected by gravity?")]
        [SerializeField] private bool m_gravityEnabled = true;
        [Tooltip("Multiplier for the gravity applied to the motor.")]
        public float m_gravityScale;    

        [Header("Ground detection")]
        [Tooltip("The layers that the motor will consider as walkable. It is not recommended to use the default layer, as it can lead to unexpected behaviours.")]
        public LayerMask m_groundLayers;
        [Tooltip("The steepest slope angle that the motor will consider walkable.")]
        public float m_maxSlopeAngle;

        [Header("Others")]
        [Tooltip("Advanced settings for the motor that you usually do not need to change.")]
        public AdvancedSettings m_advancedSettings = new AdvancedSettings();

        #endregion

        #region Private variables

        // Movement
        private float m_speedMultiplier = 1.0f;            // Speed multiplier that can be used to modify movement speed from other modules via motor

        // Ground detection
        private Surface m_groundSurface;            // The current ground surface motor is touching
        private Surface m_wallSurface;              // The current wall surface motor is touching
        private bool m_isGrounded;                  // Flag if the motor is considered grounded or not
        private bool m_isSloped;                    // Flag if the motor is considered on a slope (steeper ground angle than m_maxSlopeAngle)
        private float m_groundPreventionTimer;      // Timer for preventing the motor from being considered grounded.
        private float m_airTime;                    // Time spent ungrounded.
        private float m_groundForceTime;            // Timer for forcing motor to be grounded

        // Component references
        private Rigidbody m_rigidbody;              // The rigidbody of the character
        private CapsuleCollider m_collider;         // The collider of the character

        // Modules
        private List<Module> m_modules = new List<Module>();             // List of all modules that are attached to the motor

        #endregion

        #region Events

        public event Action OnGroundEnter;        // Called when the motor is grounded
        public event Action OnGroundExit;         // Called when the motor is ungrounded
        public event Action OnNewSurfaceEnter;    // Called when the motor enters a new surface

        #endregion

        #region Unity methods

        private void Awake()
        {
            // We use the awake call to initialize motor
            GetComponents();
            InitializeRigidbody();
            GeneratePhysicsMaterial();

            // Set the logging level based on settings
            Logging.SetLoggingLevel(m_advancedSettings.LoggingLevel);
        }

        private void Update()
        {
            UpdateGrounding();
            UpdateTimers();
        }

        private void FixedUpdate()
        {
            ApplyGravity();
        }

        #endregion

        #region Initalization

        /// <summary>
        /// Find all the components that are needed by the motor.
        /// </summary>
        private void GetComponents()
        {
            m_rigidbody = GetComponentInParent<Rigidbody>();
            m_collider = GetComponentInParent<CapsuleCollider>();
        }

        /// <summary>
        /// Sets default settings for the rigidbody that the motor uses
        /// </summary>
        private void InitializeRigidbody()
        {
            m_rigidbody.useGravity = false;
            m_rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            m_rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        /// <summary>
        /// Generate a custom physics material for the collider that does not have any friction.
        /// This way we can control friction manually and not have to worry about friction from physics system.
        /// </summary>
        private void GeneratePhysicsMaterial()
        {
            // Create new material
            PhysicMaterial pm = new PhysicMaterial();

            // Set friction
            pm.frictionCombine = PhysicMaterialCombine.Minimum;
            pm.staticFriction = 0.0f;
            pm.dynamicFriction = 0.0f;

            // Also set bounciness
            pm.bounceCombine = PhysicMaterialCombine.Minimum;
            pm.bounciness = 0.0f;

            // Assign material
            m_collider.material = pm;
        }

        #endregion

        #region Modules
        /// <summary>
        /// Registers a module in the motor
        /// </summary>
        /// <param name="module">The module to register</param>
        public void RegisterModule(Module module)
        {
            // Add module to list
            m_modules.Add(module);
        }

        /// <summary>
        /// Unregisters a module from the motor
        /// </summary>
        /// <param name="module">Module to unregister</param>
        public void UnregisterModule(Module module)
        {
            // Remove module from list
            m_modules.Remove(module);
        }

        /// <summary>
        /// Gets a module of the specific type from list of modules.
        /// This is more performant by keeping a master list of all modules.
        /// </summary>
        /// <typeparam name="T">The type of module</typeparam>
        /// <returns></returns>
        public Module GetModule<T>() where T : Module
        {
            // Loop through all modules
            foreach (Module module in m_modules)
            {
                // Check if module is of type T
                if (module is T)
                {
                    // Return module
                    return module;
                }
            }

            // No module found
            return null;
        }

        #endregion

        #region Gravity
        /// <summary>
        /// The gravity multiplier for motor
        /// </summary>
        public float GravityScale
        {
            get { return m_gravityScale; }
            set { m_gravityScale = value; }
        }

        /// <summary>
        /// Does the motor use gravity
        /// </summary>
        public bool UsesGravity
        {
            get { return m_gravityEnabled; }
        }

        /// <summary>
        /// Let the motor be affected by gravity
        /// </summary>
        public void EnableGravity()
        {
            m_gravityEnabled = true;
        }

        /// <summary>
        /// Disable gravity for the motor
        /// </summary>
        public void DisableGravity()
        {
            m_gravityEnabled = false;
        }

        /// <summary>
        /// Toggle gravity for the motor
        /// </summary>
        public void ToggleGravity()
        {
            m_gravityEnabled = !m_gravityEnabled;
        }

        /// <summary>
        /// Applies gravity to the motor while in air or on a slope
        /// </summary>
        private void ApplyGravity()
        {
            // Dont apply gravity if disabled
            if (!m_gravityEnabled)
                return;

            if (!m_isGrounded || m_isSloped)
            {
                if (m_isSloped)
                {
                    Vector3 slopeGravityDir = Vector3.ProjectOnPlane(Physics.gravity, m_groundSurface.Normal);
                    m_rigidbody.AddForce(slopeGravityDir * m_gravityScale, ForceMode.Acceleration);
                }

                else
                {
                    m_rigidbody.AddForce(Physics.gravity * m_gravityScale, ForceMode.Acceleration);
                }            
            }
        }

        #endregion

        #region Movement
        /// <summary>
        /// The velocity of the rigidbody
        /// </summary>
        public Vector3 Velocity
        {
            get { return m_rigidbody.velocity; }
        }

        /// <summary>
        /// The total speed of the motor
        /// </summary>
        public float Speed
        {
            get { return m_rigidbody.velocity.magnitude; }
        }

        /// <summary>
        /// Mutliplier to use for modules affecting movement speed
        /// </summary>
        public float SpeedMutliplier
        {
            get { return m_speedMultiplier; }
            set { m_speedMultiplier = value; }
        }

        /// <summary>
        /// The horizontal velocity of the motor
        /// </summary>
        public Vector3 HorizontalVelocity
        {
            get { return new Vector3(m_rigidbody.velocity.x, 0, m_rigidbody.velocity.z); }
        }

        /// <summary>
        /// The horizontal speed of the motor, ignoring vertical speed
        /// </summary>
        public float HorizontalSpeed
        {
            get { return new Vector3(m_rigidbody.velocity.x, 0, m_rigidbody.velocity.z).magnitude; }
        }

        /// <summary>
        /// The vertical speed of the motor, or how fast it is falling.
        /// </summary>
        public float VerticalSpeed
        {
            get { return m_rigidbody.velocity.y; }
        }

        /// <summary>
        /// Prevents the rigidbody from being affected by forces.
        /// This essentially freezes the motor in place.
        /// </summary>
        public void DisableRigidbody()
        {
            m_rigidbody.isKinematic = true;
        }

        /// <summary>
        /// Allows the rigidbody to be affected by forces.
        /// </summary>
        public void EnableRigidbody()
        {
            m_rigidbody.isKinematic = false;
        }

        /// <summary>
        /// Allow changing of the speed multiplier
        /// </summary>
        /// <param name="value">The value to modify with</param>
        /// <param name="modifyType">How value should be used</param>
        public void ModifySpeedMultiplier(float value, ModifyType modifyType)
        {
            if (modifyType == ModifyType.Additive)
            {
                m_speedMultiplier += value;
            }

            else if (modifyType == ModifyType.Multiplicative)
            {
                m_speedMultiplier *= value;
            }

            else if (modifyType == ModifyType.Override)
            {
                m_speedMultiplier = value;
            }
        }

        /// <summary>
        /// Sets the velocity of the rigidbody directly
        /// </summary>
        /// <param name="velocity">The new velocity</param>
        public void SetVelocity(Vector3 velocity, float groundPrevention = 0.0f)
        {
            m_rigidbody.velocity = velocity;
            PreventGrounding(groundPrevention);
        }

        /// <summary>
        /// Adds force like a rigidbody would
        /// </summary>
        /// <param name="velocity">The velocity to add</param>
        public void AddForce(Vector3 velocity)
        {
            velocity = velocity * Time.fixedDeltaTime;
            m_rigidbody.velocity += velocity;
        }

        /// <summary>
        /// Adds a force unscaled by time   
        /// </summary>
        /// <param name="velocity">Velocity to add</param>
        public void AddImpulse(Vector3 velocity, float groundPrevention = 0.0f)
        {
            m_rigidbody.AddForce(velocity, ForceMode.Impulse);
            PreventGrounding(groundPrevention);
        }

        /// <summary>
        /// Performs a raycast right above player to check if there is a ceiling or other object there.
        /// Is used to prevent player from jumping when there is a ceiling above them and checking if player can stand up when crouched.
        /// </summary>
        /// <returns></returns>
        public bool IsBlocked()
        {
            return Physics.Raycast(GetPlayerTop(), Vector3.up, 0.05f, m_groundLayers);
        }

        /// <summary>
        /// Sets the position of the player directly
        /// </summary>
        /// <param name="position">The position to set</param>
        public void SetPosition(Vector3 position)
        {
            m_rigidbody.MovePosition(position);
        }

        /// <summary>
        /// Forces the motor to be considered grounded for an amount of time
        /// </summary>
        /// <param name="t"></param>
        public void ForceGrounded(float t)
        {

        }

        /// <summary>
        /// Prevents the motor from being considered grounded for a period of time.
        /// Usually used when jumping to prevent the motor from sticking to the ground due to inconsistencies between fixed and update.
        /// </summary>
        /// <param name="t"></param>
        private void PreventGrounding(float t)
        {
            m_groundPreventionTimer = t;

            if (t > 0.0f)
                m_isGrounded = false;
        }

        #endregion

        #region Ground Detection
        /// <summary>
        /// The grounded status of motor 
        /// </summary>
        public bool Grounded
        {
            get { return m_isGrounded; }
        }

        /// <summary>
        /// The layer that current ground surface is on
        /// </summary>
        public int GroundLayer
        {
            get { return m_groundSurface.Transform.gameObject.layer; }
        }

        /// <summary>
        /// Is the motor currently on a slope
        /// </summary>
        public bool OnSlope
        {
            get { return m_isSloped; }
        }

        /// <summary>
        /// Does the ground normal point up?
        /// </summary>
        public bool EvenGround
        {
            get { return m_groundSurface.Angle == 0f; }
        }

        /// <summary>
        /// The current surface of the ground
        /// </summary>
        public Surface GroundSurface
        {
            get { return m_groundSurface; }
        }

        /// <summary>
        /// The normal vector of the ground
        /// </summary>
        public Vector3 GroundNormal
        {
            get { return m_groundSurface.Normal; }
        }

        /// <summary>
        /// Time spent in air
        /// </summary>
        public float Airtime
        {
            get { return m_airTime; }
        }

        /// <summary>
        /// Return the layermask for ground
        /// </summary>
        public LayerMask GroundLayers
        {
            get { return m_groundLayers; }
        }

        /// <summary>
        /// Updates the grounding status of motor depending on current surfaces
        /// </summary>
        private void UpdateGrounding()
        {
            // Always be considered grounded if forced to do so
            if (m_groundForceTime > 0.0f)
            {
                m_isGrounded = true;
                m_isSloped = false;
            }
            
            // Make sure we are touching ground
            if (m_groundSurface == null)
            {
                // Call event if we left ground
                if (m_isGrounded)
                    OnUngrounded();
                
                m_isGrounded = false;
                m_isSloped = false;
                return;
            }

            // Determine if ground slope is too steep
            if (m_groundSurface.Angle > m_maxSlopeAngle)
            {
                m_isSloped = true;
                m_isGrounded = false;
                return;
            }

            // Normal grounded
            if (m_groundPreventionTimer <= 0.0f)
            {
                // Call event if previously not grounded
                if (!m_isGrounded)
                    OnGrounded();

                m_isGrounded = true;
                m_isSloped = false;
                return;
            }
        }
        
        /// <summary>
        /// Checks the given collision to update ground/wall detection
        /// </summary>
        /// <param name="collision"></param>
        private void CheckCollision(Collision collision)
        {
            // Ignore if the collision is not with a walkable layer
            if (!m_groundLayers.Contains(collision.gameObject.layer))
                return;

            // Create surface object from collision
            Surface surface = new Surface(collision);

            // Surface is considered ground
            if (surface.Angle < 90f)
            {
                HandleGroundSurface(surface);
            }
            
            // Surface is a wall
            else if (surface.Angle == 90.0f)
            {
                HandleWallSurface(surface);
            }               
        }

        /// <summary>
        /// Handles when checking collisions with a ground surface
        /// </summary>
        /// <param name="ground"></param>
        private void HandleGroundSurface(Surface ground)
        {
            // Do we already have a ground surface cached?
            if (m_groundSurface != null)
            {
                // If the transform has changed, signal event
                if (ground.Transform != m_groundSurface.Transform)
                    OnNewSurface();

                // Copy over values instead of creating a new instance to prevent multiple on grounded events
                m_groundSurface.Copy(ground);
            }

            // No ground surface cached, create new one
            else
            {
                m_groundSurface = ground;
                OnNewSurface(); // Call event since this is a new surface anyways
            }
        }

        /// <summary>
        /// Handles the checking of collisions with surfaces at 90 degrees
        /// </summary>
        /// <param name="wall"></param>
        private void HandleWallSurface(Surface wall)
        {
            // Do we already have a wall surface cached?
            if (m_wallSurface != null)
            {
                // Copy over values instead of creating a new instance to prevent multiple on grounded events
                m_wallSurface.Copy(wall);
            }

            // No wall surface cached, create new one
            else
            {
                m_wallSurface = wall;
                OnWall();
            }
        }

        /// <summary>
        /// Checks if the exit collision event is any of the surfaces that we are currently touching
        /// </summary>
        /// <param name="collision">Collision to check</param>
        private void CheckCollisionExit(Collision collision)
        {
            // Ground 
            if (m_groundSurface != null)
            {
                if (m_groundSurface.Transform == collision.transform)
                {
                    m_groundSurface = null;
                }
            }

            else if (m_wallSurface != null)
            {
                if (m_wallSurface.Transform == collision.transform)
                {
                    m_wallSurface = null;
                    OnWallExit();
                }
            }
        }

        /// <summary>
        /// Will be called when the motor goes from not grounded to grounded
        /// </summary>
        private void OnGrounded()
        {
            Logging.Log("Motor has become grounded", LoggingLevel.Dev);

            // Call event
            OnGroundEnter?.Invoke();
            
            // Reset airtime
            m_airTime = 0.0f;
        }

        /// <summary>
        /// Will be called when the motor goes from grounded to ungrounded
        /// </summary>
        private void OnUngrounded()
        {
            Logging.Log("Motor has become ungrounded", LoggingLevel.Dev);

            // Call event
            OnGroundExit?.Invoke();
        }

        /// <summary>
        /// Will be called when the motor touches a new wall surface
        /// </summary>
        private void OnWall()
        {
            Logging.Log("Motor has touched a new wall", LoggingLevel.Dev);
        }

        /// <summary>
        /// Called when the motor exits current wall it is touching
        /// </summary>
        private void OnWallExit()
        {
            Logging.Log("Motor has exited a wall", LoggingLevel.Dev);
        }

        /// <summary>
        /// Called when the motor comes into a new surface different to the current one
        /// </summary>
        private void OnNewSurface()
        {
            OnNewSurfaceEnter?.Invoke();
        }

        #endregion

        #region Collision Events
        /// <summary>
        /// Called when the collider of the motor enters a collision.
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionEnter(Collision collision)
        {
            CheckCollision(collision);
        }


        /// <summary>
        /// Called when the collider of the motor exits a collision.
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionExit(Collision collision)
        {
            CheckCollisionExit(collision);
        }
        #endregion

        #region Player dimensions
        /// <summary>
        /// Returns the position at the top of the player capsule
        /// </summary>
        /// <returns>Point in world spcace</returns>
        public Vector3 GetPlayerTop()
        {
            Vector3 offset = Vector3.up * (m_collider.height / 2);
            return transform.position + offset;
        }

        /// <summary>
        /// Returns the position at the bottom of the player capsule
        /// </summary>
        /// <returns>Point in world space</returns>
        public Vector3 GetPlayerBottom()
        {
            Vector3 offset = Vector3.down * (m_collider.height / 2);
            return transform.position + offset;
        }

        /// <summary>
        /// Returns the radius of the player collider
        /// </summary>
        /// <returns></returns>
        public float GetPlayerRadius()
        {
            return m_collider.radius;
        }

        /// <summary>
        /// Returns the height of the player collider
        /// </summary>
        /// <returns></returns>
        public float GetPlayerHeight()
        {
            return m_collider.height;
        }

        #endregion

        #region Timers
        /// <summary>
        /// Updates all timers
        /// </summary>
        private void UpdateTimers()
        {
            GroundPreventionTimer();
            AirTimeTimer();
            ForceGroundTimer();
        }

        /// <summary>
        /// Updates the timer for prevention of becoming grounded
        /// </summary>
        private void GroundPreventionTimer()
        {
            if (m_groundPreventionTimer > 0.0f)
                m_groundPreventionTimer -= Time.deltaTime;
        }

        /// <summary>
        /// Updates the timer for forcing motor to be considered grounded
        /// </summary>
        private void ForceGroundTimer()
        {
            if (m_groundForceTime > 0.0f)
                m_groundForceTime -= Time.deltaTime;
        }

        /// <summary>
        /// Updates the airtime timer when not grounded
        /// </summary>
        private void AirTimeTimer()
        {
            if (!m_isGrounded)
            {
                m_airTime += Time.deltaTime;
            }
        }

        #endregion
    }
}