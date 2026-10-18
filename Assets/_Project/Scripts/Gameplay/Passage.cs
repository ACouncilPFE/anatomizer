using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// One tube-shaped passage of the body, for example an artery, an airway or a stretch of gut.
    ///
    /// A passage follows a smooth curve (a Catmull-Rom spline through control points) of any
    /// length and direction. Each cross-section is an ellipse with its own radii, which can
    /// widen or narrow along the length, pulse slightly, and round off to a sealed dome at an
    /// end that is capped. Positions along it are measured as <c>s</c>, metres from the start.
    ///
    /// Each point along the curve also has a frame: <c>tangent</c> (the direction of travel) and
    /// <c>right</c> / <c>up</c> (the axes of the cross-section, kept from twisting). Everything
    /// that needs the passage's shape asks it: the wall mesh, the pilot's containment, the camera,
    /// blood flow, spawning and wall damage.
    ///
    /// Passages join into a <see cref="PassageNetwork"/>; at a fork the walls open into each other.
    /// </summary>
    public sealed class Passage
    {
        public const float Step = 0.5f; // spacing of the precomputed samples, in metres

        public readonly string Name;
        public readonly float Length;
        public readonly Vector2 BaseRadii;
        public readonly bool CapStart;
        public readonly bool CapEnd;

        /// <summary>Length of the rounded end cap at a sealed end.</summary>
        public float CapLength = 8f;
        /// <summary>Width multiplier along the passage (1 = BaseRadii), e.g. a taper.</summary>
        public System.Func<float, float> WidthAt = s => 1f;
        /// <summary>Slow ripple in the width along the passage, as a fraction.</summary>
        public float PulseAmount = 0.06f;
        public float PulseFrequency = 0.11f;
        public float PulsePhase;
        /// <summary>Live size multiplier for the whole passage (a heart chamber squeezing), or null.</summary>
        public System.Func<float> Squeeze;
        /// <summary>What the passage looks like.</summary>
        public PassageProfile Profile = PassageProfile.Artery;

        private readonly Vector3[] centers;
        private readonly Vector3[] tangents;
        private readonly Vector3[] ups;
        private readonly Vector3[] rights;

        public Passage(string name, IList<Vector3> controlPoints, Vector2 baseRadii, bool capStart, bool capEnd)
        {
            Name = name;
            BaseRadii = baseRadii;
            CapStart = capStart;
            CapEnd = capEnd;

            // Trace the spline densely, then resample it at an even spacing along its length.
            List<Vector3> dense = new List<Vector3>();
            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                Vector3 p0 = controlPoints[Mathf.Max(i - 1, 0)];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];
                Vector3 p3 = controlPoints[Mathf.Min(i + 2, controlPoints.Count - 1)];
                const int steps = 24;
                for (int k = 0; k < steps; k++)
                {
                    dense.Add(CatmullRom(p0, p1, p2, p3, k / (float)steps));
                }
            }
            dense.Add(controlPoints[controlPoints.Count - 1]);

            float[] cumulative = new float[dense.Count];
            for (int i = 1; i < dense.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] + Vector3.Distance(dense[i - 1], dense[i]);
            }
            Length = cumulative[dense.Count - 1];

            int count = Mathf.Max(2, Mathf.CeilToInt(Length / Step) + 1);
            centers = new Vector3[count];
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                float target = Mathf.Min(i * Step, Length);
                while (j < dense.Count - 2 && cumulative[j + 1] < target)
                {
                    j++;
                }
                float span = Mathf.Max(cumulative[j + 1] - cumulative[j], 1e-5f);
                centers[i] = Vector3.Lerp(dense[j], dense[j + 1], (target - cumulative[j]) / span);
            }

            // Frames by parallel transport, so the cross-section doesn't twist along the curve.
            tangents = new Vector3[count];
            ups = new Vector3[count];
            rights = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 a = centers[Mathf.Max(i - 1, 0)];
                Vector3 b = centers[Mathf.Min(i + 1, count - 1)];
                tangents[i] = (b - a).normalized;
            }
            Vector3 up = Mathf.Abs(Vector3.Dot(tangents[0], Vector3.up)) < 0.95f ? Vector3.up : Vector3.forward;
            for (int i = 0; i < count; i++)
            {
                up = (up - Vector3.Dot(up, tangents[i]) * tangents[i]).normalized;
                ups[i] = up;
                rights[i] = Vector3.Cross(up, tangents[i]).normalized;
            }
        }

        /// <summary>How far along the passage the ship (and anything else that moves) may go.</summary>
        public float MinTravel => CapStart ? CapLength + 2f : 0f;
        public float MaxTravel => CapEnd ? Length - CapLength - 2f : Length;

        // ─── Shape ─────────────────────────────────────────────────────────────────

        public void Frame(float s, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up)
        {
            float f = Mathf.Clamp(s, 0f, Length) / Step;
            int i = Mathf.Min(Mathf.FloorToInt(f), centers.Length - 2);
            float t = Mathf.Clamp01(f - i);
            center = Vector3.Lerp(centers[i], centers[i + 1], t);
            tangent = Vector3.Slerp(tangents[i], tangents[i + 1], t).normalized;
            up = Vector3.Slerp(ups[i], ups[i + 1], t).normalized;
            right = Vector3.Slerp(rights[i], rights[i + 1], t).normalized;
        }

        public Vector3 CenterAt(float s)
        {
            Frame(s, out Vector3 c, out _, out _, out _);
            return c;
        }

        public Vector3 TangentAt(float s)
        {
            Frame(s, out _, out Vector3 t, out _, out _);
            return t;
        }

        /// <summary>Wall radii (along right, along up) at s, including the pulse and end caps.</summary>
        public Vector2 RadiiAt(float s)
        {
            float scale = WidthAt(s) * (1f + Mathf.Sin(s * PulseFrequency + PulsePhase) * PulseAmount) * CapTaper(s)
                * (Squeeze != null ? Squeeze() : 1f);
            return BaseRadii * scale;
        }

        /// <summary>1 along the open passage, rounding to 0 at a capped end (a dome).</summary>
        public float CapTaper(float s)
        {
            float d = 0f;
            if (CapStart && s < CapLength)
            {
                d = (CapLength - s) / CapLength;
            }
            else if (CapEnd && s > Length - CapLength)
            {
                d = (s - (Length - CapLength)) / CapLength;
            }
            d = Mathf.Clamp01(d);
            return Mathf.Sqrt(1f - d * d);
        }

        /// <summary>A point on the cross-section at s: angle round it, radial 0 = centre, 1 = wall.</summary>
        public Vector3 PointAt(float s, float angle, float radial)
        {
            Frame(s, out Vector3 c, out _, out Vector3 right, out Vector3 up);
            Vector2 r = RadiiAt(s);
            return c + right * (Mathf.Cos(angle) * r.x * radial) + up * (Mathf.Sin(angle) * r.y * radial);
        }

        // ─── Queries ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The nearest point on the centreline. Returns s; local is the offset in the
        /// cross-section (x along right, y along up); along is how far past either end the point
        /// lies (0 when it is beside the passage).
        /// </summary>
        public float Project(Vector3 point, out Vector2 local, out float along)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < centers.Length; i++)
            {
                float d = (centers[i] - point).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            // Refine on the neighbouring segments.
            float s = best * Step;
            float bestRefined = float.MaxValue;
            for (int k = -1; k <= 0; k++)
            {
                int a = best + k;
                if (a < 0 || a + 1 >= centers.Length)
                {
                    continue;
                }
                Vector3 seg = centers[a + 1] - centers[a];
                float t = Mathf.Clamp01(Vector3.Dot(point - centers[a], seg) / Mathf.Max(seg.sqrMagnitude, 1e-6f));
                float d = (centers[a] + seg * t - point).sqrMagnitude;
                if (d < bestRefined)
                {
                    bestRefined = d;
                    s = (a + t) * Step;
                }
            }
            s = Mathf.Clamp(s, 0f, Length);

            Frame(s, out Vector3 center, out Vector3 tangent, out Vector3 right, out Vector3 up);
            Vector3 offset = point - center;
            local = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, up));
            along = Vector3.Dot(offset, tangent);
            if (s > 0f && s < Length)
            {
                along = 0f;
            }
            return s;
        }

        /// <summary>How far a point is from the centreline as a share of the wall (0 centre, 1 wall).
        /// Points off either end of the passage count as far outside.</summary>
        public float RadialFraction(Vector3 point)
        {
            float s = Project(point, out Vector2 local, out float along);
            if (Mathf.Abs(along) > 0.25f)
            {
                return float.MaxValue;
            }
            Vector2 r = RadiiAt(s);
            if (r.x < 1e-4f || r.y < 1e-4f)
            {
                return float.MaxValue;
            }
            float nx = local.x / r.x;
            float ny = local.y / r.y;
            return Mathf.Sqrt(nx * nx + ny * ny);
        }

        /// <summary>
        /// How far, roughly in metres, a point is outside the containment limits (0 = inside). maxFraction is how close
        /// to the wall it may go, margin extra clearance in metres, travelSlack lets it go that much
        /// further into a capped end than the ship may.
        /// </summary>
        public float Violation(Vector3 point, float maxFraction, float margin, float travelSlack)
        {
            float s = Project(point, out Vector2 local, out float along);
            float excess = Mathf.Max(0f, (MinTravel - travelSlack) - s) + Mathf.Max(0f, s - (MaxTravel + travelSlack));
            if (!CapStart && s <= 0f) excess += Mathf.Max(0f, -along);
            if (!CapEnd && s >= Length) excess += Mathf.Max(0f, along);

            float sc = Mathf.Clamp(s, MinTravel - travelSlack, MaxTravel + travelSlack);
            Vector2 r = RadiiAt(sc);
            float rx = Mathf.Max(0.1f, r.x * maxFraction - margin);
            float ry = Mathf.Max(0.1f, r.y * maxFraction - margin);
            float n = Mathf.Sqrt((local.x / rx) * (local.x / rx) + (local.y / ry) * (local.y / ry));
            // Both parts in metres, so "past the end" and "through the side" compare fairly.
            return Mathf.Max(0f, n - 1f) * 0.5f * (rx + ry) + excess;
        }

        /// <summary>
        /// Moves a point back inside this passage (see Violation for the limits). Returns true if it
        /// moved; outwardNormal is the direction it was pushed back from, so callers can cancel
        /// velocity going that way.
        /// </summary>
        public bool ClampInside(ref Vector3 point, float maxFraction, float margin, float travelSlack, out Vector3 outwardNormal)
        {
            outwardNormal = Vector3.zero;
            float s = Project(point, out Vector2 local, out float along);
            bool moved = false;

            float sMin = MinTravel - travelSlack;
            float sMax = MaxTravel + travelSlack;
            Frame(Mathf.Clamp(s, sMin, sMax), out _, out Vector3 tangent, out _, out _);
            if (s < sMin || (!CapStart && s <= 0f && along < 0f))
            {
                s = Mathf.Max(s, sMin);
                along = 0f;
                outwardNormal -= tangent;
                moved = true;
            }
            else if (s > sMax || (!CapEnd && s >= Length && along > 0f))
            {
                s = Mathf.Min(s, sMax);
                along = 0f;
                outwardNormal += tangent;
                moved = true;
            }

            Frame(s, out Vector3 center, out tangent, out Vector3 right, out Vector3 up);
            Vector2 r = RadiiAt(s);
            float rx = Mathf.Max(0.1f, r.x * maxFraction - margin);
            float ry = Mathf.Max(0.1f, r.y * maxFraction - margin);
            float n = Mathf.Sqrt((local.x / rx) * (local.x / rx) + (local.y / ry) * (local.y / ry));
            if (n > 1f)
            {
                local /= n;
                outwardNormal += (right * (local.x / (rx * rx)) + up * (local.y / (ry * ry))).normalized;
                moved = true;
            }

            if (moved)
            {
                point = center + right * local.x + up * local.y + tangent * along;
                outwardNormal.Normalize();
            }
            return moved;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
