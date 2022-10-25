using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Core;
using URC.Camera;
using URC.Utility;
using URC.Surfaces;

namespace URC.Audio
{
    /// <summary>
    /// The component for playing footsteps for the character
    /// </summary>
    public class Footsteps : Module
    {
        #region Structs

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

        #endregion

        #region Enums

        /// <summary>
        /// Options for how the footsteps should be controlled.
        /// </summary>
        public enum FootstepMode
        {
            Fixed,                 // Footsteps will be played at fixed intervals
            VelocityDriven,        // Footsteps will be controlled by movement of motor
            ManualApplication,     // Footsteps will be controlled manually by outside scripts
            HeadbobSyncronization  // Footsteps will sync to headbob module
        }

        /// <summary>
        /// Various ways to pick footsteps to play
        /// </summary>
        public enum FootstepSelectionMode
        {
            Random,         // Footsteps will be picked randomly from the bundle
            WeightedRandom, // Random, but with weights
            Ordered         // Footsteps will be played in order
        }

        #endregion

        #region Public variables

        [Header("General settings")]
        [Tooltip("How the footsteps should be controlled.")]
        public FootstepMode m_footstepMode;
        [Tooltip("The source to play footsteps from. Will play in world if left empty.")]
        public AudioSource m_audioSource;

        [Header("Sounds")]
        [Tooltip("Default footsteps. If left empty, default bundle will be loaded.")]
        public AudioBundle m_footstepSounds;
        [Tooltip("The mode to select what footsteps to play.")]
        public FootstepSelectionMode m_selectionMode;
        [Tooltip("Random variation in pitch of footsteps. Can help small amount of footstep sounds sound more varied.")]
        public float m_pitchVariation;
        [Tooltip("Random variation to volume. Preferably keep this low.")]
        public float m_volumeVariation;

        [Header("Overrides")]
        [Tooltip("Override footsteps for specific layers")]
        public LayerOverride[] m_layerOverrides;
        [Tooltip("When coming into contact with a surface that has custom audio properties, should layer overrides have higher priority?")]
        public bool m_overrideSurfaces;

        [Header("Resetting")]
        [Tooltip("Delay when stopping before footstep timer is reset.")]
        public float m_resetDelay;
        [Tooltip("When going from standing still to moving, how far into the frequency should the footsteps start? With long frequencies, this can help with starting them sooner.")]
        [Range(0, 1)]
        public float m_footstepPrewarming;

        [Header("Fixed mode")]
        [Tooltip("The interval between footsteps in fixed mode.")]
        public float m_fixedFrequency;

        [Header("Velocity driven mode")]
        [Tooltip("The minimum velocity required to play footsteps.")]
        public float m_velocityThreshold;
        [Tooltip("Speed of footsteps. This value is multiplied by magnitude of vertical velocity")]
        public float m_velocityDrivenFrequency;

        #endregion

        #region Private variables

        // Footstep
        private int m_footstepIndex;    // Index of the current footstep for ordered selection
        private float m_footstepTimer;  // Timer for footstep interval
        private float m_lastHeadbobValue;
        private bool m_headbobSoundPlayed;
        private AudioBundle m_defaultFootsteps;

        // Overriding
        private bool m_overridingFootsteps;     // Is the layer override currently active?

        // Resettings
        private float m_resetTimer;

        // Optional headbob module
        private Headbob m_headbobModule;

        #endregion

        #region Unity methods

        public override void Awake()
        {
            base.Awake();            
            VerifyOverrideLayers();            

            // If there are no sounds, use the default footsteps
            if (m_footstepSounds == null) FindDefaultFootsteps();

            // Cache the default footsteps
            m_defaultFootsteps = m_footstepSounds;
        }

        private void Start()
        {
            // Verify that headbob exists if we are using it.
            // Set in start, as modules are registered to motor in OnEnable(), which is after awake.
            VerifyHeadbob();
        }

        private void Update()
        {
            // Always check for resets
            Resetting();
            
            if (m_footstepMode == FootstepMode.Fixed)
            {
                FixedFootsteps();
            }

            else if (m_footstepMode == FootstepMode.VelocityDriven)
            {
                VelocityDriven();
            }

            else if (m_footstepMode == FootstepMode.HeadbobSyncronization)
            {
                HeadbobSyncronization();
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Motor.OnNewSurfaceEnter += CheckNewSurface;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            Motor.OnNewSurfaceEnter -= CheckNewSurface;
        }

        #endregion

        #region Initialization

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
        /// Attempts to find headbob module and throw message if not found
        /// and headbob mode is selected.
        /// </summary>
        private void VerifyHeadbob()
        {
            m_headbobModule = (Headbob)Motor.GetModule<Headbob>();
            if (m_footstepMode == FootstepMode.HeadbobSyncronization && m_headbobModule == null)
            {
                Logging.Log("Headbob syncing was chosen for footsteps, but no headbob module was found! Switching to default mode.", LoggingLevel.Critical);
                m_footstepMode = FootstepMode.VelocityDriven;
            }
        }

        #endregion

        #region Footsteps

        /// <summary>
        /// Plays a footstep audio.
        /// </summary>
        public void PlayFootstep()
        {
            // Notify if audio bundle is empty
            if (m_footstepSounds.Size == 0)
            {
                Logging.Log("There are no sounds in the footsteps bundle. No sound will be played", LoggingLevel.Critical);
                return;
            }

            AudioBundle.Audio audio = GetFootstepAudio();
            PlayAudio(audio);
        }

        /// <summary>
        /// Is called when the motor enters a new surface.
        /// Performs checks to see if the footstep audio should be overridden.
        /// </summary>
        private void CheckNewSurface()
        {
            CheckForOverride();
            CheckCustomAudio();
        }

        /// <summary>
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
            m_overridingFootsteps = overridden;
        }

        /// <summary>
        /// Checks if the current surface of the motor has a custom audio surface.
        /// Will swap footsteps with that, unless there is an override from the layer and layer override does not take priority.
        /// </summary>
        private void CheckCustomAudio()
        {
            // Check if there is a audio surface on the current surface
            AudioSurface surface = Motor.GroundSurface.GetProperties<AudioSurface>();

            // If there is a surface, swap if not overriding or if the override takes priority
            if (surface != null)
            {
                
                if (!m_overridingFootsteps || (m_overridingFootsteps && !m_overrideSurfaces))
                {
                    m_footstepSounds = surface.m_footstepSounds;
                }
            }

            else
            {
                // Go back to default as long as footsteps arent overriden by layer
                if (!m_overridingFootsteps)
                    m_footstepSounds = m_defaultFootsteps;
            }
        }

        /// <summary>
        /// Attempts to reset the footsteps timer if player has been standing still for a while.
        /// Does not apply to headbob syncronization
        /// </summary>
        private void Resetting()
        {
            // Increment timer when not moving
            if (!InputHelper.DesiresMove())
            {
                m_resetTimer += Time.deltaTime;
            }

            // Reset footsteps timer if standing still for long enough
            if (m_resetTimer >= m_resetDelay)
            {
                m_footstepTimer = GetFrequency() * m_footstepPrewarming;
                m_resetTimer = 0;
            }
        }

        /// <summary>
        /// Footsteps should be played at fixed intervals. Most basic form
        /// of footsteps.
        /// </summary>
        private void FixedFootsteps()
        {
            // Update timer if input is provided and grounded
            if (IsUpdateValid()) m_footstepTimer += Time.deltaTime;

            // Play footsteps if timer is over interval
            if (m_footstepTimer >= GetFrequency())
            {
                PlayFootstep();
                m_footstepTimer = 0;
            }
        }

        /// <summary>
        /// This logic is for when footsteps should be based on the velocity
        /// and movement of the motor.
        /// </summary>
        private void VelocityDriven()
        {
            // Make sure we are grounded and moving
            if (!IsUpdateValid() || Motor.HorizontalSpeed < m_velocityThreshold)
            {
                return;
            }

            m_footstepTimer += Time.deltaTime;

            // Play footsteps if timer is over interval
            if (m_footstepTimer >= GetFrequency())
            {
                PlayFootstep();
                m_footstepTimer = 0;
            }
        }

        /// <summary>
        /// This is the logic for when footsteps should be syncronized to headbobs.
        /// Plays footsteps every time that the headbob is at a peak.
        /// </summary>
        private void HeadbobSyncronization()
        {
            float headbob = m_headbobModule.GetNormalizedBobValue();

            // Play one time once headbob goes from decending value to ascending value
            if (headbob > m_lastHeadbobValue && !m_headbobSoundPlayed)
            {
                PlayFootstep();
                m_headbobSoundPlayed = true;
            }

            // Reset flag once headbob goes from ascending value to decending value
            else if (headbob < m_lastHeadbobValue)
            {
                m_headbobSoundPlayed = false;
            }
        }

        /// <summary>
        /// Calculates the frequency based on mode selected.
        /// </summary>
        /// <returns></returns>
        private float GetFrequency()
        {
            if (m_footstepMode == FootstepMode.VelocityDriven)
            {
                return m_velocityDrivenFrequency / Motor.HorizontalSpeed;
            }

            // Return fixed as default
            return m_fixedFrequency;
        }

        /// <summary>
        /// Checks that we are grounded and desires to move in order for update to be considered grounded
        /// </summary>
        private bool IsUpdateValid()
        {
            return (Motor.Grounded && InputHelper.DesiresMove());
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
        /// Picks a footstep sound from the bundle based on settings
        /// </summary>
        /// <returns></returns>
        private AudioBundle.Audio GetFootstepAudio()
        {
            AudioBundle.Audio audio = null;

            // ordered selection
            if (m_selectionMode == FootstepSelectionMode.Ordered)
            {
                // Loop if we are at the end of the bundle
                if (m_footstepIndex >= m_footstepSounds.Size)
                {
                    m_footstepIndex = 0;
                }

                audio = m_footstepSounds.GetAudio(m_footstepIndex);
                m_footstepIndex++;
            }

            // Random selection
            else
            {
                audio = (m_selectionMode == FootstepSelectionMode.WeightedRandom) ? m_footstepSounds.GetWeightedAudio() : m_footstepSounds.GetRandomAudio();
            }

            return audio;
        }
    }

    #endregion
}