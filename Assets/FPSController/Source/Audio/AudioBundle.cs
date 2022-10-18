using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace URC.Audio
{
    /// <summary>
    /// A collection of sounds bundled together.
    /// Allows for easy and clean swapping of collections of sounds.
    /// </summary>
    public class AudioBundle : ScriptableObject
    {
        /// <summary>
        /// Have an audio with a default volume
        /// </summary>
        [System.Serializable]
        public struct Audio
        {
            public AudioClip Clip;
            public float Volume;
            public string Tag;
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
        /// Returns a random audio from the bundle
        /// </summary>
        /// <returns></returns>
        public Audio GetRandomAudio()
        {
            return m_audioCollection[Random.Range(0, m_audioCollection.Length)];
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