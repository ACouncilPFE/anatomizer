using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The passages of Mission 3, Gut Run:
    ///   Oesophagus       a ribbed muscular tube leading down from the throat (start here)
    ///   Stomach          a huge J-shaped chamber with deep folds and an acid pool at the bottom
    ///   Pylorus          the narrow ring of muscle that lets food out of the stomach, a timed gate
    ///   Small intestine  a long winding tube lined with ring folds and villi (sealed far end)
    /// At each joint the narrower passage flares to just wider than its neighbour's opening, so the
    /// walls overlap without a gap. Peristalsis (GutMotility) squeezes the oesophagus and intestine.
    /// </summary>
    public static class GutLayout
    {
        public const float PylorusGateS = 6f;
        /// <summary>World height of the acid surface in the stomach.</summary>
        public const float AcidLevel = -22f;

        public static Passage Esophagus => PassageNetwork.All[0];
        public static Passage Stomach => PassageNetwork.All[1];
        public static Passage Pylorus => PassageNetwork.All[2];
        public static Passage Intestine => PassageNetwork.All[3];

        private static float Flare(float s, float length, float atStart, float atEnd)
        {
            float start = Mathf.Lerp(atStart, 1f, Mathf.SmoothStep(0f, 1f, s / 4f));
            float end = Mathf.Lerp(1f, atEnd, Mathf.SmoothStep(0f, 1f, (s - (length - 4f)) / 4f));
            return start * end;
        }

        public static void BuildMission3(GutMotility motility)
        {
            PassageNetwork.Clear();

            Passage esophagus = new Passage("Oesophagus", new[]
            {
                new Vector3(0f, 0f, -12f), new Vector3(0f, -1f, 15f), new Vector3(0.5f, -3f, 40f), new Vector3(1f, -6f, 62f),
            }, new Vector2(4.2f, 3.8f), true, false) { Profile = PassageProfile.Esophagus, PulseAmount = 0.02f };
            esophagus.WidthAt = s => Flare(s, esophagus.Length, 1f, 1.4f);

            Passage stomach = new Passage("Stomach", new[]
            {
                new Vector3(1f, -5.5f, 56f), new Vector3(4f, -12f, 72f), new Vector3(12f, -16f, 86f), new Vector3(22f, -14f, 94f), new Vector3(30f, -9f, 96f),
            }, new Vector2(17f, 14f), false, false) { Profile = PassageProfile.Stomach, PulseAmount = 0.03f };
            // Bulbous J: narrow where it joins the oesophagus and pylorus, cavernous in between.
            // (Max keeps the power off sin's tiny negative value at the very end, which would give NaN.)
            stomach.WidthAt = s => Mathf.Lerp(0.33f, 1f, Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(s / stomach.Length) * Mathf.PI)), 0.55f));

            Passage pylorus = new Passage("Pylorus", new[]
            {
                new Vector3(28f, -10f, 95.5f), new Vector3(34.5f, -8f, 96f), new Vector3(41f, -7f, 96.2f),
            }, new Vector2(3.2f, 3f), false, false) { Profile = PassageProfile.Esophagus, PulseAmount = 0.01f };
            pylorus.WidthAt = s => Flare(s, pylorus.Length, 2.05f, 1.85f);

            Passage intestine = new Passage("Small intestine", new[]
            {
                new Vector3(39f, -7f, 96f), new Vector3(50f, -6f, 92f), new Vector3(58f, -3f, 82f), new Vector3(56f, 0f, 70f),
                new Vector3(46f, 1f, 64f), new Vector3(38f, 3f, 70f), new Vector3(36f, 6f, 82f), new Vector3(44f, 8f, 90f),
                new Vector3(56f, 9f, 88f), new Vector3(62f, 10f, 76f), new Vector3(60f, 12f, 62f), new Vector3(50f, 12f, 56f),
            }, new Vector2(5f, 4.6f), false, true) { Profile = PassageProfile.Intestine, PulseAmount = 0.02f };

            if (motility != null)
            {
                esophagus.SqueezeAt = s => motility.WaveSqueeze(s);
                intestine.SqueezeAt = s => motility.WaveSqueeze(s);
            }

            PassageNetwork.Register(esophagus);
            PassageNetwork.Register(stomach);
            PassageNetwork.Register(pylorus);
            PassageNetwork.Register(intestine);
        }
    }
}
