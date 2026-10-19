using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The gut's movement in Mission 3:
    ///   - peristalsis: rings of muscle squeeze in waves that travel down the oesophagus and the
    ///     small intestine, pushing contents along. A wave catching the ship carries it forward and
    ///     grinds it against the walls;
    ///   - the pylorus: the stomach's exit ring opens for a few seconds at a time.
    /// </summary>
    public class GutMotility : MonoBehaviour
    {
        [Header("Peristalsis")]
        [SerializeField] private float waveSpeed = 5f;      // m/s along the passage
        [SerializeField] private float waveSpacing = 45f;   // metres between waves
        [SerializeField] private float waveWidth = 7f;      // length of the squeezed band
        [SerializeField, Range(0f, 0.8f)] private float waveDepth = 0.42f;

        [Header("Pylorus")]
        [SerializeField] private float pylorusCycle = 7f;   // seconds between openings
        [SerializeField] private float pylorusOpenFor = 2.6f;

        private float clock;

        public float WaveSpeed => waveSpeed;
        public bool PylorusOpen => Mathf.Repeat(clock, pylorusCycle) < pylorusOpenFor;
        /// <summary>Seconds until the pylorus next opens (0 while open).</summary>
        public float PylorusOpensIn => PylorusOpen ? 0f : pylorusCycle - Mathf.Repeat(clock, pylorusCycle);

        private void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.CurrentState == GameState.InMission)
            {
                clock += Time.deltaTime;
            }
        }

        /// <summary>Width multiplier at s from the waves passing through (1 = relaxed).</summary>
        public float WaveSqueeze(float s)
        {
            float d = Mathf.Repeat(s - waveSpeed * clock, waveSpacing);
            if (d > waveWidth)
            {
                return 1f;
            }
            float bump = Mathf.Sin(d / waveWidth * Mathf.PI);
            return 1f - waveDepth * bump * bump;
        }

        /// <summary>True when a wave is within `ahead` metres behind s (about to catch up).</summary>
        public bool WaveComing(float s, float ahead = 10f)
        {
            for (float back = 2f; back <= ahead; back += 2f)
            {
                if (WaveSqueeze(s - back) < 0.9f)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
