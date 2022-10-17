using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Utility;

namespace URC.Audio
{
    public class Footsteps : Module
    {
        [Header("Sounds")]
        [Tooltip("All possible footstep sounds")]
        public AudioClip[] m_footstepSounds;
        [Tooltip("Should the footsteps be played in the order they are in the array? If not, footsteps will be chosen at random.")]
        public bool m_playOrdered;
        [Tooltip("Random variation in pitch for footstep sounds. Can help make a small amount of footsteps sound less repetitive. Will not work unless an audio source is specified")]
        public float m_pitchVariation;

        [Header("Playback")]
        [Tooltip("The volume of the footsteps")]
        public float m_volume;
        [Tooltip("Random variation to volume. Preferably keep this value very low.")]
        public float m_volumeVariation;

        [Header("Timing")]
        [Tooltip("How often should the footsteps be played?")]
        public float m_frequency;
        [Tooltip("Should the frequency scale with velocity?")]
        public bool m_frquencyVelocityScaling;
        [Tooltip("Random variation to frequency.")]
        public float m_frequencyVariation;
        [Tooltip("How long to wait after standing still before resetting the footsteps timer.")]
        public float m_resetDelay;
        [Tooltip("When going from standing still to moving, how far into the frequency should the footsteps start? With long frequencies, this can help with starting them sooner.")]
        [Range(0, 1)]
        public float m_footstepPrewarming;

        [Header("Others")]
        [Tooltip("Minimum velocity to start playing footsteps. Can prevent footsteps from being active when walking into walls.")]
        public float m_minVelocity;
        [Tooltip("Specify the audio source to play the footsteps from. If none is specified, sounds will be played with PlayClipAtPoint()")]
        public AudioSource m_audioSource;

        // Footsteps
        private float m_footstepTimer;
        private int m_footstepIndex;
        private float m_resetTimer;

        private void Update()
        {
            if (Motor.Grounded)
            {
                UpdateFootsteps();
            }

            CheckReset();
        }

        /// <summary>
        /// Updates the footstep timer and plays footstep sound when needed.
        /// This is a public method and can be used to enable footsteps for times when the motor is not grounded, like if you were to implement wallrunning.
        /// </summary>
        public void UpdateFootsteps()
        {
            // Has footsteps timer reached the frequency?
            if (m_footstepTimer >= GetFrequency())
            {
                // Play footstep sound
                PlayFootstepSound();

                // Reset footsteps timer
                m_footstepTimer = 0;
            }
            else
            {
                // Increase footsteps timer
                m_footstepTimer += Time.deltaTime;
            }
        }

        /// <summary>
        /// Tracks and resets the footsteps if the player is standing still.
        /// </summary>
        private void CheckReset()
        {
            if (!InputHelper.DesiresMove() && Motor.HorizontalSpeed > m_minVelocity || !Motor.Grounded)
            {
                // Decrement delay timer and do nothing unless its 0
                m_resetTimer -= Time.deltaTime;
                if (m_resetTimer > 0)
                {
                    return;
                }

                // Reset footsteps timer
                m_footstepTimer = GetFrequency() * m_footstepPrewarming;
            }

            else
            {
                // Reset delay as we are moving
                m_resetTimer = m_resetDelay;
            }
        }

        /// <summary>
        /// Selects and plays a footstep sound at chosen audio source
        /// </summary>
        private void PlayFootstepSound()
        {
            // Get clip
            AudioClip footstep = GetFootstepSound();

            // Play clip
            PlayAudio(footstep);
        }

        /// <summary>
        /// Returns a footstep based on settings
        /// </summary>
        /// <returns></returns>
        private AudioClip GetFootstepSound()
        {
            // Check if we should play ordered or random
            if (m_playOrdered)
            {
                // Check if we have reached the end of the array
                if (m_footstepIndex >= m_footstepSounds.Length)
                {
                    // Reset index
                    m_footstepIndex = 0;
                }

                // Return footstep
                return m_footstepSounds[m_footstepIndex++];
            }
            else
            {
                // Return random footstep
                return m_footstepSounds[Random.Range(0, m_footstepSounds.Length)];
            }
        }

        /// <summary>
        /// Plays the given clip with randomization options
        /// </summary>
        /// <param name="clip">The clip to play</param>
        private void PlayAudio(AudioClip clip)
        {
            // Get parameters
            float pitch = 1 + Random.Range(-m_pitchVariation, m_pitchVariation);
            float volume = m_volume + Random.Range(-m_volumeVariation, m_volumeVariation);

            // Play at audio source if one is specified
            if (m_audioSource != null)
            {
                // Set pitch and volume
                m_audioSource.pitch = pitch;
                m_audioSource.volume = volume;

                // Play it
                m_audioSource.clip = clip;
                m_audioSource.Play();
            }

            // Play with PlayClipAtPoint otherwise. Note that this way, we cannot include pitch
            else
            {
                // Determine where to play sound
                Vector3 playPoint = Motor.GetPlayerBottom();

                // Play it.
                AudioSource.PlayClipAtPoint(clip, playPoint, volume);
            }
        }

        /// <summary>
        /// Returns the frequency of footsteps, accounting for velocity if toggled
        /// </summary>
        /// <returns></returns>
        private float GetFrequency()
        {
            float frequency = (m_frquencyVelocityScaling) ? m_frequency * Motor.HorizontalSpeed : m_frequency;
            return frequency;
        }
    }
}