using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Audio;
using URC.Surfaces;

namespace URC.Movement
{
    /// <summary>
    /// Allows the player to jump.
    /// 
    /// In this controller, jumps are defined through creating a sequence of jumps. This is in order to allow
    /// multiple jumps.
    /// </summary>
    public class Jumping : Module
    {
        #region Enums

        /// <summary>
        /// Modes for how the jump should interact with the velocity of motor
        /// </summary>
        public enum JumpApplicationMode
        {
            Additive,   // Jump adds force to the motor
            Override    // Jump overrides the velocity of motor
        }

        #endregion

        #region Structs

        /// <summary>
        /// Contains information about a single jump
        /// </summary>
        [System.Serializable]
        public struct Jump
        {
            [Tooltip("The strength of the jump")]
            public float force;
            [Tooltip("Does the jump require motor to be grounded? Will be skipped to next (if any) if not grounded")]
            public bool requiresGrounding;
            [Tooltip("How the jump should interact with current velocity of motor")]
            public JumpApplicationMode applicationMode;        
        }

        [System.Serializable]
        public class JumpAudioSettings
        {
            [Tooltip("Should the audio be enabled?")]
            public bool Enabled;
            [Tooltip("The audio to play")]
            public AudioBundle Audio;
            [Tooltip("Should random choice use weights?")]
            public bool WeightedRandom;

            // Track the default audio here
            private AudioBundle m_defaultAudio;

            /// <summary>
            /// Resets to default audio
            /// </summary>
            public AudioBundle GetDefaultAudio()
            {
                return m_defaultAudio;
            }

            /// <summary>
            /// Sets the new default audio of this
            /// </summary>
            public void SetDefaultBundle(AudioBundle defaultAudio)
            {
                m_defaultAudio = defaultAudio;
            }
        }

        #endregion

        #region Public variables

        [Header("Activation")]
        public KeyCode m_jumpKey;
        [Tooltip("Can the player hold down jump key to jump automatically?")]
        public bool m_autoJump;

        [Header("Sequence")]
        public List<Jump> m_jumpSequence;

        [Header("Settings")]    
        [Tooltip("Time after leaving ground where player can still jump. Only counts for first jump in sequence and first jump requires ground.")]
        public float m_coyoteTime;
        [Tooltip("The amount of time player can queue up a jump before becoming grounded. This allows for more responsive jumps.")]
        public float m_jumpQueueTime;
        [Tooltip("Multiplier for gravity when holding down space button while moving upwards. Allows for variable jump height.")]
        [Range(0, 1f)]
        public float m_jumpHoldGravityMultiplier = 1.0f;
        [Tooltip("Minimum amount of time between jumps")]
        public float m_minJumpCooldown;

        [Header("Audio settings")]
        [Tooltip("The audio source to play sound from. If none is selected, sound will be played in world space.")]
        public AudioSource m_audioSource;
        [Tooltip("Audio settings for when jumping while grounded.")]
        public JumpAudioSettings m_groundedJumpAudio;
        [Tooltip("Audio settings for additional jumps while in the air.")]
        public JumpAudioSettings m_additionalJumpAudio;
        [Tooltip("Audio settings for landing on the ground again.")]
        public JumpAudioSettings m_landingAudio;

        #endregion

        #region Private variables

        // Jump
        private int m_sequenceIndex;    // How far along the sequence we are
        private float m_jumpRequest;    // Time where jump will be excuted automatically if becoming grounded
        private float m_jumpCooldown;   // Time where player cannot queue jump

        #endregion

        #region Events

        // Events
        public event Action OnJump;     

        #endregion

        #region Unity methods

        private void Start()
        {
            FindDefaultSounds();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Motor.OnGroundEnter += OnGroundEnter;
            Motor.OnNewSurfaceEnter += OnNewSurface;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            Motor.OnGroundEnter -= OnGroundEnter;
            Motor.OnNewSurfaceEnter -= OnNewSurface;
        }

        private void Update()
        {
            UpdateJumpQueue();
            AttemptJump();
        }

        private void FixedUpdate()
        {
            ReverseGravity();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Attempts to find default sounds from resource folder
        /// </summary>
        private void FindDefaultSounds()
        {
            VerifyAudioSettings(m_groundedJumpAudio, "Default Jumps");
            VerifyAudioSettings(m_landingAudio, "Default Landings");
            //VerifyAudioSettings(m_additionalJumpAudio, "Default Additional Jumps");
        }

        private void VerifyAudioSettings(JumpAudioSettings settings, string bundleName)
        {
            // Attempt to find default audio if not set. Disable if default audio cant be found
            settings.SetDefaultBundle(Resources.Load<AudioBundle>("AudioBundles/" + bundleName));

            // Ignore if already set
            if (settings.Audio != null)
                return;

            // Set audio as default if nothing is set
            else if (settings.Enabled && settings.Audio == null)
            {
                settings.Audio = settings.GetDefaultAudio();
            }
        }

        #endregion

        #region Jumping

        /// <summary>
        /// Applies a force in the opposite direction of gravity when holding down space and going upwards.
        /// This is for allowing variable jump height by reducing gravity when holding down space.
        /// </summary>
        private void ReverseGravity()
        {
            if (Input.GetKey(m_jumpKey) && Motor.VerticalSpeed > 0.0f)
            {
                Vector3 defaultGravity = (Physics.gravity * Motor.GravityScale);
                Vector3 reverseGravity = (1 - m_jumpHoldGravityMultiplier) * -defaultGravity;
                Motor.AddForce(reverseGravity);
            }         
        }

        /// <summary>
        /// Listens for input and updates the jump queue accordingly.
        /// Also decrements jump request
        /// </summary>
        private void UpdateJumpQueue()
        {
            // Decrement jump cooldown and do nothing if above 0
            if (m_jumpCooldown > 0.0f)
            {
                m_jumpCooldown -= Time.deltaTime;
                return;
            }

            // Listen for input and issue request
            bool jumpIssued = (m_autoJump) ? Input.GetKey(m_jumpKey) : Input.GetKeyDown(m_jumpKey);
            m_jumpRequest = (jumpIssued) ? m_jumpQueueTime : m_jumpRequest -= Time.deltaTime;
        }

        /// <summary>
        /// Checks if the jump sequence can be executed and executes it if possible
        /// </summary>
        private void AttemptJump()
        {
            // Has there been a recent jump request?
            if (m_jumpRequest <= 0.0f)
            {
                return;
            }    

            // Make sure we are not at end of sequence
            if (m_sequenceIndex >= m_jumpSequence.Count)
            {
                return;
            }

            // Make sure we aren't blocked
            if (Motor.IsBlocked())
            {
                return;
            }

            // Get current jump from sequence
            Jump jump = m_jumpSequence[m_sequenceIndex];

            // Check if this is the first jump or not and needs grounding
            if (m_sequenceIndex == 0 && jump.requiresGrounding)
            {
                // Air time less than coyote time?
                if (Motor.Airtime <= m_coyoteTime)  
                {
                    // Do the jump.
                    ExecuteJump(jump);
                }

                // First jump no longer valid. Execute next jump in sequence if it exists
                else if (m_sequenceIndex + 1 < m_jumpSequence.Count)
                {
                    m_sequenceIndex++;
                    jump = m_jumpSequence[m_sequenceIndex];
                    ExecuteJump(jump);
                }
            }

            else
            {
                // Perform jump
                ExecuteJump(jump);
            }
        }

        /// <summary>
        /// Performs a jump with the given jump
        /// </summary>
        /// <param name="jump">The jump to perform</param>
        private void ExecuteJump(Jump jump)
        {
            // Play sound if enabled
            if (m_sequenceIndex == 0)
            {
                if (m_groundedJumpAudio.Enabled) 
                    PlaySound(m_groundedJumpAudio);
            }

            else
            {
                if (m_additionalJumpAudio.Enabled)
                    PlaySound(m_additionalJumpAudio);
            }

            // Increase sequence index if we are not at the end
            m_sequenceIndex += 1;

            // Add force to motor
            Vector3 force = Vector3.up * jump.force;
            if (jump.applicationMode == JumpApplicationMode.Additive)
            {
                Motor.AddImpulse(force, 0.1f);
            }
            
            // Directly set velocity instead
            else
            {
                Vector3 newVelocity = Motor.HorizontalVelocity + force;
                Motor.SetVelocity(newVelocity, 0.1f);
            }

            // Reset request and add cooldown
            m_jumpRequest = 0.0f;
            m_jumpCooldown = m_minJumpCooldown;

            // Invoke event
            OnJump?.Invoke();
        }

        /// <summary>
        /// Plays a random jump sound from audio bundle
        /// </summary>
        private void PlaySound(JumpAudioSettings settings)
        {
            // Choose sound either weighted or random
            AudioBundle.Audio audio = (settings.WeightedRandom) ? settings.Audio.GetWeightedAudio() : settings.Audio.GetRandomAudio();

            // Play based on if audio source is set or not
            if (m_audioSource != null)
            {
                m_audioSource.PlayOneShot(audio.Clip, audio.Volume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(audio.Clip, transform.position, audio.Volume);
            }
        }

        #endregion

        #region Subscriptions

        /// <summary>
        /// Resets the jump sequence. Subscribed to event from motor
        /// </summary>
        private void OnGroundEnter()
        {
            m_sequenceIndex = 0;

            // Play landing sound if enabled
            if (m_landingAudio.Enabled) PlaySound(m_landingAudio);
        }

        /// <summary>
        /// Checks if the surface has audio properties to override ground jump and landing sounds.
        /// Subscribed from motor
        /// </summary>
        private void OnNewSurface()
        {
            // Check if there is a audio surface on the current surface
            AudioSurface surface = Motor.GroundSurface.GetProperties<AudioSurface>();

            // If there is a surface, swap audio
            if (surface != null)
            {
                m_groundedJumpAudio.Audio = (surface.m_jumpingSounds == null) ? m_groundedJumpAudio.GetDefaultAudio() : surface.m_jumpingSounds;
                m_landingAudio.Audio = (surface.m_jumpingSounds == null) ? m_landingAudio.GetDefaultAudio() : surface.m_landingSounds;
            }

            else
            {
                // Revert to default sounds
                m_groundedJumpAudio.Audio = m_groundedJumpAudio.GetDefaultAudio();
                m_landingAudio.Audio = m_landingAudio.GetDefaultAudio();
            }
        }
        
        #endregion
    }  
}