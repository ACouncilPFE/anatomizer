using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Every sound in the game, synthesised in code (no audio files). Each clip is built once on
    /// first use. Loops are made seamless by using whole-number frequencies over the clip length
    /// and cross-fading the noise at the seam.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int Rate = 44100;

        private static AudioClip flow, heartbeat, scrape, beamHum;
        private static AudioClip laser, medicine, hitSquish, hitWall, cellPop, cellHealed, lesionFixed, wrongTool;
        private static AudioClip[] rubs;

        // ─── Loops ──────────────────────────────────────────────────────────────────

        /// <summary>Blood rushing past the hull: deep, slowly surging filtered noise.</summary>
        public static AudioClip Flow => flow ??= Loop("Flow", 3f, (t, n) =>
        {
            float low = n.Brown(0.02f);
            float surge = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * (1f / 3f) * t);
            return low * surge * 1.6f;
        });

        /// <summary>The patient's heart, ~72 bpm: a "lub-dub" thump.</summary>
        public static AudioClip Heartbeat => heartbeat ??= Clip("Heartbeat", 60f / 72f, (t, n) =>
            Thump(t, 0f, 58f, 1f) + Thump(t, 0.28f, 50f, 0.65f) + n.Brown(0.05f) * 0.08f * Mathf.Exp(-t * 6f));

        /// <summary>Hull grinding along the vessel wall: rubbery, juddering friction.</summary>
        public static AudioClip Scrape => scrape ??= Loop("Scrape", 1f, (t, n) =>
        {
            float grit = n.Band(0.18f, 0.04f);
            float judder = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 11f * t));
            return grit * judder * 1.0f;
        });

        /// <summary>The surgical beam's hum (pitch it per tool).</summary>
        public static AudioClip BeamHum => beamHum ??= Loop("BeamHum", 1f, (t, n) =>
        {
            float vibrato = Mathf.Sin(2f * Mathf.PI * 6f * t) * 0.6f;
            return 0.35f * Mathf.Sin(2f * Mathf.PI * 220f * t + vibrato)
                 + 0.2f * Mathf.Sin(2f * Mathf.PI * 330f * t + vibrato * 1.5f)
                 + 0.08f * Mathf.Sin(2f * Mathf.PI * 880f * t)
                 + n.White() * 0.03f;
        });

        // ─── One-shots ──────────────────────────────────────────────────────────────

        /// <summary>Laser: a bright zap sweeping down.</summary>
        public static AudioClip Laser => laser ??= Clip("Laser", 0.22f, (t, n) =>
        {
            float phase = 2f * Mathf.PI * Sweep(1900f, 9f, 250f, t);
            float tone = Mathf.Sin(phase) + 0.35f * Mathf.Sign(Mathf.Sin(phase * 0.5f));
            return tone * Env(t, 0.003f, 0.22f) * 0.45f;
        });

        /// <summary>Medicine round: a soft pneumatic "pfft" with a rising, bubbly tone.</summary>
        public static AudioClip Medicine => medicine ??= Clip("Medicine", 0.3f, (t, n) =>
        {
            float puff = n.High(0.6f) * Mathf.Exp(-t * 25f) * 0.5f;
            float phase = 2f * Mathf.PI * (300f * t + 700f * t * t);
            float bubble = Mathf.Sin(phase) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 30f * t));
            return puff + bubble * Env(t, 0.01f, 0.3f) * 0.35f;
        });

        /// <summary>A round hitting a cell or tissue: a wet squish.</summary>
        public static AudioClip HitSquish => hitSquish ??= Clip("HitSquish", 0.25f, (t, n) =>
            (Thump(t, 0f, 110f, 0.7f) + n.Low(0.08f) * Env(t, 0.002f, 0.2f) * 0.9f) * 0.75f);

        /// <summary>A round splashing against the artery wall.</summary>
        public static AudioClip HitWall => hitWall ??= Clip("HitWall", 0.18f, (t, n) =>
            n.Low(0.05f) * Env(t, 0.002f, 0.18f) * 0.8f + Thump(t, 0f, 80f, 0.3f));

        /// <summary>A bad cell bursting.</summary>
        public static AudioClip CellPop => cellPop ??= Clip("CellPop", 0.45f, (t, n) =>
            (Thump(t, 0f, 70f, 1f) + n.Low(0.15f) * Env(t, 0.003f, 0.4f) * 1.2f
            + Mathf.Sin(2f * Mathf.PI * (400f * t - 300f * t * t)) * Env(t, 0.001f, 0.12f) * 0.3f) * 0.6f);

        /// <summary>A cell cured: a soft, rising two-note chime.</summary>
        public static AudioClip CellHealed => cellHealed ??= Clip("CellHealed", 0.7f, (t, n) =>
            Bell(t, 0f, 660f) * 0.5f + Bell(t, 0.12f, 990f) * 0.45f);

        /// <summary>Artery damage repaired: a bright three-note arpeggio.</summary>
        public static AudioClip LesionFixed => lesionFixed ??= Clip("LesionFixed", 0.9f, (t, n) =>
            Bell(t, 0f, 523f) * 0.4f + Bell(t, 0.1f, 659f) * 0.4f + Bell(t, 0.2f, 784f) * 0.45f);

        /// <summary>Wrong tool: a short low buzz.</summary>
        public static AudioClip WrongTool => wrongTool ??= Clip("WrongTool", 0.22f, (t, n) =>
            Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 110f * t)) * 0.25f * Env(t, 0.005f, 0.22f)
            + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 116f * t)) * 0.15f * Env(t, 0.005f, 0.22f));

        /// <summary>A few variations of a soft, squelchy rub, for cells brushing the capsule.</summary>
        public static AudioClip[] Rubs
        {
            get
            {
                if (rubs == null)
                {
                    rubs = new AudioClip[4];
                    for (int i = 0; i < rubs.Length; i++)
                    {
                        float wobble = 18f + i * 7f;
                        float tone = 140f + i * 25f;
                        rubs[i] = Clip("Rub" + i, 0.4f + i * 0.05f, (t, n) =>
                        {
                            float body = n.Low(0.06f + i * 0.01f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * wobble * t));
                            float squelch = Mathf.Sin(2f * Mathf.PI * (tone * t - 60f * t * t)) * 0.25f;
                            return (body * 1.4f + squelch) * Env(t, 0.04f, 0.38f + i * 0.05f);
                        }, seed: 7 + i);
                    }
                }
                return rubs;
            }
        }

        // ─── Building blocks ────────────────────────────────────────────────────────

        // Attack-then-exponential-decay envelope over `length` seconds.
        private static float Env(float t, float attack, float length)
        {
            if (t < attack)
            {
                return t / attack;
            }
            return Mathf.Exp(-(t - attack) / Mathf.Max(length * 0.25f, 0.001f));
        }

        // A low sine "thump" that drops in pitch, starting at `start` seconds.
        private static float Thump(float t, float start, float freq, float gain)
        {
            float u = t - start;
            if (u < 0f)
            {
                return 0f;
            }
            float phase = 2f * Mathf.PI * (freq * u - freq * 0.3f * u * u * 4f);
            return Mathf.Sin(phase) * Mathf.Exp(-u * 16f) * gain * Mathf.Min(1f, u / 0.004f);
        }

        // A bell-like tone with a couple of inharmonic partials.
        private static float Bell(float t, float start, float freq)
        {
            float u = t - start;
            if (u < 0f)
            {
                return 0f;
            }
            float e = Mathf.Exp(-u * 5f) * Mathf.Min(1f, u / 0.003f);
            return e * (Mathf.Sin(2f * Mathf.PI * freq * u)
                      + 0.4f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * u) * Mathf.Exp(-u * 6f)
                      + 0.2f * Mathf.Sin(2f * Mathf.PI * freq * 5.4f * u) * Mathf.Exp(-u * 10f));
        }

        // Phase (in cycles) of a sweep f(t) = a*e^(-k t) + b.
        private static float Sweep(float a, float k, float b, float t) => a / k * (1f - Mathf.Exp(-k * t)) + b * t;

        /// <summary>Filtered noise sources with their own state, one per clip.</summary>
        private class Noise
        {
            private System.Random random;
            private float brown, low, bandLow, bandHigh, highPrev, highOut;

            public Noise(int seed) { random = new System.Random(seed); }

            public float White() => (float)(random.NextDouble() * 2.0 - 1.0);

            // Integrated (red/brown) noise, leaking back toward zero.
            public float Brown(float step)
            {
                brown = Mathf.Clamp(brown * 0.995f + White() * step, -1f, 1f);
                return brown;
            }

            // One-pole low-pass of white noise; smaller a = darker.
            public float Low(float a)
            {
                low += (White() - low) * a;
                return low * 2f;
            }

            // Crude band-pass: difference of two low-passes.
            public float Band(float a, float b)
            {
                float w = White();
                bandLow += (w - bandLow) * a;
                bandHigh += (w - bandHigh) * b;
                return (bandLow - bandHigh) * 2f;
            }

            // One-pole high-pass.
            public float High(float a)
            {
                float w = White();
                highOut = a * (highOut + w - highPrev);
                highPrev = w;
                return highOut;
            }
        }

        private static AudioClip Clip(string name, float seconds, System.Func<float, Noise, float> sample, int seed = 1)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            float[] data = new float[count];
            Noise noise = new Noise(seed);
            for (int i = 0; i < count; i++)
            {
                data[i] = Mathf.Clamp(sample(i / (float)Rate, noise), -1f, 1f);
            }
            // Fade the last few milliseconds so nothing clicks.
            int fade = Mathf.Min(count, Rate / 200);
            for (int i = 0; i < fade; i++)
            {
                data[count - 1 - i] *= i / (float)fade;
            }
            return Make(name, data);
        }

        // Loops: render a little extra and cross-fade it into the start, so the seam is smooth.
        private static AudioClip Loop(string name, float seconds, System.Func<float, Noise, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            int overlap = Rate / 10;
            float[] raw = new float[count + overlap];
            Noise noise = new Noise(3);
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = sample(i / (float)Rate, noise);
            }

            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = raw[i];
                if (i < overlap)
                {
                    float w = i / (float)overlap;
                    data[i] = raw[i] * w + raw[count + i] * (1f - w);
                }
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
            }
            return Make(name, data);
        }

        private static AudioClip Make(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
