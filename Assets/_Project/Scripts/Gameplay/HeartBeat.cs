using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The patient's heartbeat, which drives Mission 2. Each beat runs through the cardiac cycle:
    ///   0.00-0.15  atria squeeze, topping up the ventricle
    ///   0.18-0.50  ventricles squeeze (systole): blood surges out through the pulmonary valve
    ///   0.50-1.00  everything relaxes and the ventricle refills (diastole)
    /// The tricuspid valve (atrium to ventricle) is open while the ventricle fills and shut while
    /// it squeezes; the pulmonary valve (out of the ventricle) is the opposite. So you cross the
    /// tricuspid between beats and leave on the next beat's surge.
    ///
    /// Scar tissue in the wall (see ArteryLesion) makes the rhythm irregular: beats come early or
    /// late and sometimes drop out, so valve timing gets hard until it is lasered away.
    /// </summary>
    public class HeartBeat : MonoBehaviour
    {
        public static HeartBeat Instance { get; private set; }

        [SerializeField] private float beatsPerMinute = 66f;
        [SerializeField, Range(0f, 0.5f)] private float chamberSqueeze = 0.14f; // how much the chambers shrink

        private float phase;
        private float interval;
        private int arrhythmiaSources;
        private float lastBeatTime = -999f;
        private readonly float[] recentBeats = new float[12];
        private int beatCount;

        /// <summary>Position in the current beat, 0..1.</summary>
        public float Phase => phase;
        /// <summary>Time of the last beat, for drawing the heart-rhythm trace.</summary>
        public float LastBeatTime => lastBeatTime;
        public float Interval => interval;
        public bool Arrhythmic => arrhythmiaSources > 0;

        /// <summary>Height of the heart-rhythm (ECG) trace at a past moment, summing recent beats.</summary>
        public float EcgAt(float time)
        {
            float v = 0f;
            int n = Mathf.Min(beatCount, recentBeats.Length);
            for (int i = 0; i < n; i++)
            {
                float since = time - recentBeats[i];
                if (since >= 0f && since < 0.6f)
                {
                    v += EcgShape(since);
                }
            }
            return v;
        }

        /// <summary>Raised at the start of every beat.</summary>
        public event System.Action Beat;

        /// <summary>Atrium size multiplier: 1 relaxed, smaller while it squeezes.</summary>
        public float AtriumScale => 1f - chamberSqueeze * Bump(phase, 0f, 0.15f);
        /// <summary>Ventricle size multiplier.</summary>
        public float VentricleScale => 1f - chamberSqueeze * Bump(phase, 0.18f, 0.5f);
        /// <summary>How hard blood is surging forward right now, 0..1 (peaks in ventricular systole).</summary>
        public float Surge => Mathf.Max(Bump(phase, 0.18f, 0.5f), 0.35f * Bump(phase, 0f, 0.15f));

        public bool TricuspidOpen => phase < 0.16f || phase > 0.52f;
        public bool PulmonaryOpen => phase > 0.2f && phase < 0.5f;

        public void AddArrhythmiaSource() => arrhythmiaSources++;
        public void RemoveArrhythmiaSource() => arrhythmiaSources = Mathf.Max(0, arrhythmiaSources - 1);

        private void Awake()
        {
            Instance = this;
            interval = 60f / beatsPerMinute;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
            {
                return;
            }

            phase += Time.deltaTime / Mathf.Max(interval, 0.1f);
            if (phase >= 1f)
            {
                phase -= 1f;
                StartBeat();
            }
        }

        private void StartBeat()
        {
            interval = 60f / beatsPerMinute;
            if (Arrhythmic)
            {
                // Irregular rhythm: early, late, and now and then a dropped beat.
                interval *= Random.Range(0.6f, 1.45f);
                if (Random.value < 0.15f)
                {
                    interval *= 1.8f;
                }
            }

            lastBeatTime = Time.time;
            recentBeats[beatCount % recentBeats.Length] = lastBeatTime;
            beatCount++;
            GameAudio.Play(ProceduralAudio.Heartbeat, Arrhythmic ? 0.95f : 0.8f, Arrhythmic ? Random.Range(0.9f, 1.15f) : 1f, 0.2f);
            Beat?.Invoke();
        }

        /// <summary>A smooth 0..1..0 hump between two phase points.</summary>
        private static float Bump(float p, float from, float to)
        {
            if (p < from || p > to)
            {
                return 0f;
            }
            return Mathf.Sin((p - from) / (to - from) * Mathf.PI);
        }

        /// <summary>A heart-rhythm (ECG) trace height for a time since a beat: P, QRS and T waves.</summary>
        public static float EcgShape(float secondsSinceBeat)
        {
            float t = secondsSinceBeat;
            float p = 0.12f * Mathf.Exp(-Mathf.Pow((t - 0.05f) / 0.025f, 2f));
            float q = -0.12f * Mathf.Exp(-Mathf.Pow((t - 0.14f) / 0.008f, 2f));
            float r = 1.0f * Mathf.Exp(-Mathf.Pow((t - 0.16f) / 0.01f, 2f));
            float sWave = -0.25f * Mathf.Exp(-Mathf.Pow((t - 0.18f) / 0.01f, 2f));
            float tWave = 0.25f * Mathf.Exp(-Mathf.Pow((t - 0.38f) / 0.05f, 2f));
            return p + q + r + sWave + tWave;
        }
    }
}
