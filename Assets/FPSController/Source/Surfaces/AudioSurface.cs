using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using URC.Audio;

namespace URC.Surfaces
{
    /// <summary>
    /// A surface that will override the audio of player footsteps.
    /// </summary>
    public class AudioSurface : CustomSurfaceProperties
    {
        [Header("Audio")]
        [Tooltip("The footsteps to play on this surface.")]
        public AudioBundle m_footsteps;
        [Tooltip("The landing sound to play on this surface.")]
        public AudioBundle m_landingSounds;
    }
}