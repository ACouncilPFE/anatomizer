using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Plays one-shot sound effects from anywhere: GameAudio.Play(ProceduralAudio.Laser).
    /// Pass a position for sounds out in the vessel (hits, cells bursting) so they come from that
    /// direction; leave it out for the ship's own sounds. Repeats of the same clip within a short
    /// gap are skipped, so things called every frame (a beam on the wrong target) don't drone.
    /// </summary>
    public static class GameAudio
    {
        public static float MasterVolume = 0.9f;

        private const int PoolSize = 16;
        private static AudioSource[] pool;
        private static int next;
        private static readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f, float minGap = 0.05f)
        {
            Play(clip, null, volume, pitch, minGap);
        }

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float minGap = 0.05f)
        {
            Play(clip, position, volume, pitch, minGap);
        }

        private static void Play(AudioClip clip, Vector3? position, float volume, float pitch, float minGap)
        {
            if (clip == null || !Application.isPlaying)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(clip, out float last) && now - last < minGap)
            {
                return;
            }
            lastPlayed[clip] = now;

            EnsurePool();
            AudioSource source = pool[next];
            next = (next + 1) % pool.Length;

            source.Stop();
            source.clip = clip;
            source.volume = volume * MasterVolume;
            source.pitch = pitch;
            if (position.HasValue)
            {
                source.transform.position = position.Value;
                source.spatialBlend = 0.85f;
            }
            else
            {
                source.spatialBlend = 0f;
            }
            source.Play();
        }

        private static void EnsurePool()
        {
            if (pool != null && pool[0] != null)
            {
                return;
            }

            GameObject host = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(host);
            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                GameObject voice = new GameObject("Voice " + i);
                voice.transform.SetParent(host.transform, false);
                AudioSource source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 4f;
                source.maxDistance = 70f;
                source.dopplerLevel = 0f;
                pool[i] = source;
            }
        }
    }
}
