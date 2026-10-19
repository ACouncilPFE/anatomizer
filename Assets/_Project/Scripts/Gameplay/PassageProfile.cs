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

        /// <summary>
        /// Heart chambers: the inner wall is covered in criss-crossing muscle ridges (trabeculae)
        /// rather than smooth folds.
        /// </summary>
        public static readonly PassageProfile HeartChamber = new PassageProfile
        {
            Name = "Heart chamber",
            MetresPerTile = 24f,
            Relief = (angle, s) => 1f
                - 0.06f * Mathf.Abs(Mathf.Sin(angle * 9f + s * 0.45f))
                - 0.04f * Mathf.Abs(Mathf.Sin(angle * 5f - s * 0.7f + 0.8f))
                + 0.02f * Mathf.Sin(s * 1.3f + angle * 2f),
        };

        /// <summary>Veins: thinner, smoother walls than arteries, with only faint folds.</summary>
        public static readonly PassageProfile Vein = new PassageProfile
        {
            Name = "Vein",
            Relief = (angle, s) => 1f + 0.015f * Mathf.Sin(angle * 6f + s * 0.1f) + 0.01f * Mathf.Sin(s * 0.7f),
        };

        /// <summary>Oesophagus: a muscular tube with soft lengthwise folds and faint rings.</summary>
        public static readonly PassageProfile Esophagus = new PassageProfile
        {
            Name = "Oesophagus",
            MetresPerTile = 20f,
            Relief = (angle, s) => 1f
                - 0.07f * Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 4f + s * 0.05f)), 3f)
                + 0.02f * Mathf.Sin(s * 1.1f),
        };

        /// <summary>Stomach: deep, winding folds of lining (rugae).</summary>
        public static readonly PassageProfile Stomach = new PassageProfile
        {
            Name = "Stomach",
            MetresPerTile = 26f,
            Relief = (angle, s) => 1f
                - 0.09f * Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 11f + Mathf.Sin(s * 0.25f) * 1.6f)), 2f)
                + 0.03f * Mathf.Sin(angle * 3f - s * 0.3f),
        };

        /// <summary>Small intestine: ring folds (plicae) all along it, covered in tiny villi bumps.</summary>
        public static readonly PassageProfile Intestine = new PassageProfile
        {
            Name = "Small intestine",
            MetresPerTile = 14f,
            Relief = (angle, s) => 1f
                - 0.12f * Mathf.Pow(Mathf.Abs(Mathf.Sin(s * 1.4f + Mathf.Sin(angle * 2f) * 0.6f)), 6f)
                + 0.025f * Mathf.Sin(angle * 40f) * Mathf.Sin(s * 9f),
        };

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
