using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Utility;

namespace URC.Audio
{
    public class Footsteps : Module
    {
        /// <summary>
        /// Change the footsteps to something else for a specific layer.
        /// </summary>
        [System.Serializable]
        public struct LayerOverride
        {
            [Tooltip("The layers to override footsteps for")]
            public LayerMask Layers;
            [Tooltip("The footsteps to use")]
            public AudioBundle Footsteps;
        }

        [Header("Sounds")]
        [Tooltip("Default footsteps to use. If left empty, it will use the default footsteps that comes with controller.")]
        public AudioBundle m_footstepSounds;
        [Tooltip("Should the footsteps be played in the order they are in the array? If not, footsteps will be chosen at random.")]
        public bool m_playOrdered;
        [Tooltip("Random variation in pitch for footstep sounds. Can help make a small amount of footsteps sound less repetitive. Will not work unless an audio source is specified")]
        public float m_pitchVariation;
        [Tooltip("Random variation to volume. Preferably keep this value very low.")]
        public float m_volumeVariation;

        [Header("Overrides")]
        [Tooltip("Override footsteps for specific layers")]
        public LayerOverride[] m_layerOverrides;

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
        private AudioBundle m_defaultFootsteps;

        private void Start()
        {
            // If there are no sounds, use the default footsteps
            if (m_footstepSounds == null) FindDefaultFootsteps();

            // Cache the default footsteps
            m_defaultFootsteps = m_footstepSounds;

            // Make sure there are no overlapping override layers
            VerifyOverrideLayers();
        }

        private void Update()
        {
            if (Motor.Grounded && Motor.HorizontalSpeed > 0.0f && InputHelper.DesiresMove())
            {
                UpdateFootsteps();
            }

            CheckReset();
        }

        private void OnEnable()
        {
            Motor.OnNewSurfaceEnter += CheckForOverride;
        }

        private void OnDisable()
        {
            Motor.OnNewSurfaceEnter -= CheckForOverride;
        }

        /// <summary>
        /// Attempts to load the default footsteps from resources folder.
        /// Will throw a warning if the default footsteps are not found.
        /// </summary>
        private void FindDefaultFootsteps()
        {
            // Attempt to load it from resources
            m_footstepSounds = Resources.Load<AudioBundle>("AudioBundles/Default footsteps");

            // If it's still null, throw a warning
            if (m_footstepSounds == null)
            {
                Logging.Log("No audio bundle was assigned to footsteps, and default footsteps could not be found. Make sure to create one via asset menu!.", LoggingLevel.Critical);
                this.enabled = false;
            }
        }

        /// <summary>
        /// Ensures that there are no overlapping layers in the layermasks for overriding.
        /// Throws out a warning and disables script if there are overlapping layers.
        /// </summary>
        private void VerifyOverrideLayers()
        {
            // Loop through each override
            for (int i = 0; i < m_layerOverrides.Length; i++)
            {
                // Loop through each override again
                for (int j = 0; j < m_layerOverrides.Length; j++)
                {
                    // If the two overrides are the same, skip
                    if (i == j) continue;

                    // If the two overrides have overlapping layers, throw a warning and disable script
                    if ((m_layerOverrides[i].Layers.value & m_layerOverrides[j].Layers.value) != 0)
                    {
                        Logging.Log("Footsteps has overlapping layers in its overrides. Please fix this before continuing.", LoggingLevel.Critical);
                        this.enabled = false;
                        return;
                    }
                }
            }
        }


        /// <summary>
        /// Called every time the motor enters a new surface.
        /// Checks if the layer of the new surface is in the overrides and swap footsteps if true.
        /// Revert to default footsteps if the layer is not in the overrides.
        /// </summary>
        private void CheckForOverride()
        {
            // Ignore if no overrides
            if (m_layerOverrides.Length == 0) return;

            // Get layer of surface
            int layer = Motor.GroundLayer;
            
            // Loop through all overrides and check if the layer is in the override
            bool overridden = false;
            for (int i = 0; i < m_layerOverrides.Length; i++)
            {
                // Is layer in override?
                if (m_layerOverrides[i].Layers.Contains(layer))
                {
                    // Set footsteps
                    m_footstepSounds = m_layerOverrides[i].Footsteps;
                    overridden = true;
                    Logging.Log("Overriding footsteps for layer " + LayerMask.LayerToName(layer), LoggingLevel.Dev);
                }
            }

            // If the layer is not in the overrides, revert to default footsteps
            if (!overridden) m_footstepSounds = m_defaultFootsteps;
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
        /// Plays a footstep sound, ignoring timers and conditions
        /// </summary>
        public void ForceFootstep()
        {
            PlayFootstepSound();
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
            // For ease of use, catch and log if there are no clips in the audio bundle
            if (m_footstepSounds.Size == 0)
            {
                Logging.Log("There are no audio in the audio budle (" + m_footstepSounds.name + "). Make sure there are at least 1.", LoggingLevel.Critical);
                return;
            }

            // Get clip
            AudioBundle.Audio footstep = GetFootstepSound();

            // Play clip
            PlayAudio(footstep);
        }

        /// <summary>
        /// Returns a footstep based on settings
        /// </summary>
        /// <returns></returns>
        private AudioBundle.Audio GetFootstepSound()
        {
            // Check if we should play ordered or random
            if (m_playOrdered)
            {
                // Check if we have reached the end of the array
                if (m_footstepIndex >= m_footstepSounds.Size)
                {
                    // Reset index
                    m_footstepIndex = 0;
                }

                // Return footstep
                return m_footstepSounds.GetAudio(m_footstepIndex);
            }
            else
            {
                // Return random footstep
                return m_footstepSounds.GetRandomAudio();
            }
        }

        /// <summary>
        /// Plays the given audio 
        /// </summary>
        /// <param name="audio">The audio to play</param>
        private void PlayAudio(AudioBundle.Audio audio)
        {
            // Get parameters
            float pitch = 1 + Random.Range(-m_pitchVariation, m_pitchVariation);
            float volume = audio.Volume + Random.Range(-m_volumeVariation, m_volumeVariation);

            // Play at audio source if one is specified
            if (m_audioSource != null)
            {
                // Set pitch and volume
                m_audioSource.pitch = pitch;
                m_audioSource.volume = volume;

                // Play it
                m_audioSource.clip = audio.Clip;
                m_audioSource.Play();
            }

            // Play with PlayClipAtPoint otherwise. Note that this way, we cannot include pitch
            else
            {
                // Determine where to play sound
                Vector3 playPoint = Motor.GetPlayerBottom();

                // Account for velocity so that sound will not be played behind instead
                playPoint += Motor.HorizontalVelocity * Time.deltaTime;

                // Play it.
                AudioSource.PlayClipAtPoint(audio.Clip, playPoint, volume);
            }
        }

        /// <summary>
        /// Returns the frequency of footsteps, accounting for velocity if toggled
        /// </summary>
        /// <returns></returns>
        private float GetFrequency()
        {
            float frequency = (m_frquencyVelocityScaling) ? m_frequency / Motor.HorizontalSpeed : m_frequency;
            return frequency;
        }
    }
}