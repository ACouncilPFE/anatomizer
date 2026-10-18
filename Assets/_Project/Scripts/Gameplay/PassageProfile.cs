using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// What a passage's wall looks like: its relief (folds and rings pressed into the lining) and
    /// how its texture is laid out. Each body system gets its own profile: an artery has soft
    /// longitudinal folds, an airway ribbed cartilage rings, the gut deep folds, and so on.
    /// </summary>
    public sealed class PassageProfile
    {
        public string Name = "Passage";

        /// <summary>Wall radius multiplier at (angle round the passage, s along it); 1 = plain tube.</summary>
        public System.Func<float, float, float> Relief = (angle, s) => 1f;

        /// <summary>Metres of wall per texture repeat, round and along the passage.</summary>
        public float MetresPerTile = 33f;

        /// <summary>Arteries: soft folds running along the vessel that twist slowly, and faint rings.</summary>
        public static readonly PassageProfile Artery = new PassageProfile
        {
            Name = "Artery",
            Relief = (angle, s) => 1f
                + 0.030f * Mathf.Sin(angle * 7f + s * 0.12f)
                + 0.018f * Mathf.Sin(angle * 13f - s * 0.21f + 1.3f)
                + 0.012f * Mathf.Sin(s * 0.9f + Mathf.Sin(angle * 3f) * 1.5f),
        };
    }
}
