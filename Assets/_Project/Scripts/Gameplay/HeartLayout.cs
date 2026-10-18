using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The passages of Mission 2, Cardiac Maelstrom: the right side of the heart, the route blood
    /// takes from the body to the lungs.
    ///
    ///   Vena cava          the big vein bringing blood back from the body (start here)
    ///   Right atrium       the first chamber: a large, thin-walled, pulsing space
    ///   Tricuspid passage  short neck through the tricuspid valve
    ///   Right ventricle    the muscular pumping chamber, ridged inside
    ///   Pulmonary trunk    the exit, through the pulmonary valve, toward the lungs (sealed for now)
    ///
    /// Neighbouring passages overlap where they meet, so the network opens between them. At each
    /// joint the narrower passage flares out to just wider than the chamber's opening, so the two
    /// walls overlap and no gap shows. The two chambers squeeze with the heartbeat
    /// (HeartBeat.AtriumScale / VentricleScale).
    /// </summary>
    public static class HeartLayout
    {
        /// <summary>Distance along each passage where its valve sits.</summary>
        public const float TricuspidValveS = 9f;
        public const float PulmonaryValveS = 8f;

        public static Passage VenaCava => PassageNetwork.All[0];
        public static Passage Atrium => PassageNetwork.All[1];
        public static Passage Tricuspid => PassageNetwork.All[2];
        public static Passage Ventricle => PassageNetwork.All[3];
        public static Passage Pulmonary => PassageNetwork.All[4];

        /// <summary>Width multiplier that widens to `atStart` / `atEnd` over the last 4 m at each end.</summary>
        private static float Flare(float s, float length, float atStart, float atEnd)
        {
            float start = Mathf.Lerp(atStart, 1f, Mathf.SmoothStep(0f, 1f, s / 4f));
            float end = Mathf.Lerp(1f, atEnd, Mathf.SmoothStep(0f, 1f, (s - (length - 4f)) / 4f));
            return start * end;
        }

        public static void BuildMission2(HeartBeat heart)
        {
            PassageNetwork.Clear();

            Passage venaCava = new Passage("Vena cava", new[]
            {
                new Vector3(0f, 0f, -14f), new Vector3(0f, 0.4f, 10f), new Vector3(-0.5f, 0.8f, 30f), new Vector3(-1f, 1f, 46f),
            }, new Vector2(5f, 4.4f), true, false) { Profile = PassageProfile.Vein };
            venaCava.WidthAt = s => Flare(s, venaCava.Length, 1f, 1.32f);

            Passage atrium = new Passage("Right atrium", new[]
            {
                new Vector3(-1f, 1f, 40f), new Vector3(-0.5f, 0.5f, 52f), new Vector3(0.5f, -0.8f, 64f), new Vector3(1f, -2f, 74f),
            }, new Vector2(14f, 11f), false, false)
            {
                Profile = PassageProfile.HeartChamber,
                // Bulbous: narrow where it joins its neighbours, wide in the middle. (Max keeps the
                // power off sin's tiny negative value at the very end, which would give NaN.)
                WidthAt = s => Mathf.Lerp(0.42f, 1f, Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(s / 34f) * Mathf.PI)), 0.7f)),
                PulseAmount = 0.03f,
            };

            Passage tricuspid = new Passage("Tricuspid passage", new[]
            {
                new Vector3(1f, -1.8f, 70f), new Vector3(1.4f, -3.5f, 79f), new Vector3(1.8f, -5.2f, 88f),
            }, new Vector2(4.8f, 4.4f), false, false) { Profile = PassageProfile.HeartChamber, PulseAmount = 0.02f };
            tricuspid.WidthAt = s => Flare(s, tricuspid.Length, 1.4f, 1.4f);

            Passage ventricle = new Passage("Right ventricle", new[]
            {
                new Vector3(1.6f, -5f, 83f), new Vector3(3f, -8.5f, 96f), new Vector3(6f, -9f, 108f), new Vector3(9.5f, -5.5f, 118f), new Vector3(11f, -1f, 124f),
            }, new Vector2(15f, 12f), false, false)
            {
                Profile = PassageProfile.HeartChamber,
                WidthAt = s => Mathf.Lerp(0.38f, 1f, Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(s / 47f) * Mathf.PI)), 0.6f)),
                PulseAmount = 0.03f,
            };

            Passage pulmonary = new Passage("Pulmonary trunk", new[]
            {
                new Vector3(10.5f, -2.5f, 120f), new Vector3(11.5f, 3f, 126f), new Vector3(12f, 10f, 131f), new Vector3(12f, 18f, 136f), new Vector3(11f, 28f, 140f),
            }, new Vector2(5.2f, 5f), false, true) { Profile = PassageProfile.Artery };
            pulmonary.WidthAt = s => Flare(s, pulmonary.Length, 1.35f, 1f);

            if (heart != null)
            {
                atrium.Squeeze = () => heart.AtriumScale;
                ventricle.Squeeze = () => heart.VentricleScale;
            }

            PassageNetwork.Register(venaCava);
            PassageNetwork.Register(atrium);
            PassageNetwork.Register(tricuspid);
            PassageNetwork.Register(ventricle);
            PassageNetwork.Register(pulmonary);
        }
    }
}
