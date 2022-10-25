using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URC.Audio
{
    /// <summary>
    /// A collection of sounds bundled together.
    /// Allows for easy and clean swapping of collections of sounds.
    /// </summary>
    [CreateAssetMenu(menuName = "URC/Audio/Audio Bundle")]
    public class AudioBundle : ScriptableObject
    {
        /// <summary>
        /// Have an audio with a default volume
        /// </summary>
        [System.Serializable]
        public class Audio
        {
            [Tooltip("The audio clip to play")]
            public AudioClip Clip;
            [Tooltip("Volume to play the audio at")]
            [Range(0, 1)]
            public float Volume = 1.0f;
            [Tooltip("Optional tag to identify the audio")]
            public string Tag = "Default";
            [Tooltip("The weight of this audio clip when considered in weighted random selection")]
            public float Weight = 1.0f;
        }

        [Header("Audio clips")]
        [Tooltip("All audio in the bundle")]
        [SerializeField] private Audio[] m_audioCollection;

        /// <summary>
        /// The number of audio in the collection
        /// </summary>
        public int Size
        {
            get { return m_audioCollection.Length; }
        }

        /// <summary>
        /// Returns a random audio from the bundle, ignoring weights
        /// </summary>
        /// <returns></returns>
        public Audio GetRandomAudio()
        {
            return m_audioCollection[Random.Range(0, m_audioCollection.Length)];
        }

        /// <summary>
        /// Returns a weighted random choice of audio from the bundle. Useful if you want to have some audio play more often than others.
        /// </summary>
        /// <returns></returns>
        public Audio GetWeightedAudio()
        {
            float totalWeight = 0;
            for (int i = 0; i < m_audioCollection.Length; i++)
            {
                totalWeight += m_audioCollection[i].Weight;
            }

            float randomWeight = Random.Range(0, totalWeight);
            for (int i = 0; i < m_audioCollection.Length; i++)
            {
                if (randomWeight < m_audioCollection[i].Weight)
                {
                    return m_audioCollection[i];
                }
                randomWeight -= m_audioCollection[i].Weight;
            }

            return m_audioCollection[0];
        }

        /// <summary>
        /// Returns a audio at the given index
        /// </summary>
        /// <param name="idx">The index of audio</param>
        /// <returns></returns>
        public Audio GetAudio(int idx)
        {
            return m_audioCollection[idx];
        }

        /// <summary>
        /// Returns the first audio in bundle with the tag. Will return empty audio if no audio with tag is found.
        /// </summary>
        /// <param name="tag">The tag to search for</param>
        /// <returns></returns>
        public Audio GetAudio(string tag)
        {
            for (int i = 0; i < m_audioCollection.Length; i++)
            {
                if (m_audioCollection[i].Tag == tag)
                {
                    return m_audioCollection[i];
                }
            }
            return new Audio();
        }

        /// <summary>
        /// Returns all audio in the bundle
        /// </summary>
        /// <returns></returns>
        public Audio[] GetAllAudio()
        {
            return m_audioCollection;
        }
        
        /// <summary>
        /// Returns all the audio in bundle with the given tag
        /// </summary>
        /// <param name="tag">The tag to search for</param>
        /// <returns></returns>
        public Audio[] GetAllAudio(string tag)
        {
            List<Audio> audioList = new List<Audio>();
            for (int i = 0; i < m_audioCollection.Length; i++)
            {
                if (m_audioCollection[i].Tag == tag)
                {
                    audioList.Add(m_audioCollection[i]);
                }
            }
            return audioList.ToArray();
        }
    }
}