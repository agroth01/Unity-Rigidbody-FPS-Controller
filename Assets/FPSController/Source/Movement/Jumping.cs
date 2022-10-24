using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Audio;
using URC.Utility;
using UnityEngine.SceneManagement;

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
        /// <summary>
        /// Modes for how the jump should interact with the velocity of motor
        /// </summary>
        public enum JumpApplicationMode
        {
            Additive,   // Jump adds force to the motor
            Override    // Jump overrides the velocity of motor
        }

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

        [Header("Sequence")]
        public List<Jump> m_jumpSequence;

        [Header("Settings")]
        [Tooltip("Can the player hold down space to jump automatically?")]
        public bool m_autoJump;
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
        [Tooltip("Should jumping sounds be enabled?")]
        public bool m_playJumpSound;
        [Tooltip("The audio source the sounds will be played from. If left empty, the audio will be played in the world instead.")]
        public AudioSource m_jumpAudioSource;
        [Tooltip("The sound to play when jumping. If no sound is set, the default sound will be played.")]
        public AudioBundle m_jumpSounds;
        [Tooltip("Use weights when picking random sound?")]
        public bool m_useWeightsForAudio;

        // Jump
        private int m_sequenceIndex;    // How far along the sequence we are
        private float m_jumpRequest;    // Time where jump will be excuted automatically if becoming grounded
        private float m_jumpCooldown;   // Time where player cannot queue jump

        private void Start()
        {
            FindDefaultSounds();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Motor.OnGroundEnter += OnGroundEnter;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            Motor.OnGroundEnter -= OnGroundEnter;
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

        /// <summary>
        /// Attempts to load the default jumping sounds from resources folder.
        /// Will throw a warning if the default sounds are not found.
        /// </summary>
        private void FindDefaultSounds()
        {
            // Attempt to load it from resources
            m_jumpSounds = Resources.Load<AudioBundle>("AudioBundles/Default jumps");

            // If it's still null, throw a warning
            if (m_jumpSounds == null)
            {
                Logging.Log("No audio bundle was assigned to jump sounds, and default sounds could not be found. Jumping sounds will not be played!.", LoggingLevel.Critical);
                m_playJumpSound = false;
            }
        }

        /// <summary>
        /// Applies a force in the opposite direction of gravity when holding down space and going upwards.
        /// This is for allowing variable jump height by reducing gravity when holding down space.
        /// </summary>
        private void ReverseGravity()
        {
            if (Input.GetButton("Jump") && Motor.VerticalSpeed > 0.0f)
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
            bool jumpIssued = (m_autoJump) ? Input.GetButton("Jump") : Input.GetButtonDown("Jump");
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

            // Play sound if enabled
            if (m_playJumpSound) PlayJumpSound();
        }

        /// <summary>
        /// Plays a random jump sound from audio bundle
        /// </summary>
        private void PlayJumpSound()
        {
            // Choose sound either weighted or random
            AudioBundle.Audio audio = (m_useWeightsForAudio) ? m_jumpSounds.GetWeightedAudio() : m_jumpSounds.GetRandomAudio();

            // Play based on if audio source is set or not
            if (m_jumpAudioSource != null)
            {
                m_jumpAudioSource.PlayOneShot(audio.Clip, audio.Volume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(audio.Clip, transform.position, audio.Volume);
            }
        }

        /// <summary>
        /// Resets the jump sequence. Subscribed to event from motor
        /// </summary>
        private void OnGroundEnter()
        {
            m_sequenceIndex = 0;
        }
    }
}