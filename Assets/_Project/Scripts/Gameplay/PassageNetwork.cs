using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// All the passages in the current mission, joined where they overlap (a fork). "Inside the
    /// body" means inside any of them, so the ship can fly from one passage into another through
    /// the opening where they meet. The first passage registered is the main one.
    /// </summary>
    public static class PassageNetwork
    {
        private static readonly List<Passage> passages = new List<Passage>();

        public static IReadOnlyList<Passage> All => passages;
        public static Passage Main => passages.Count > 0 ? passages[0] : null;

        public static void Clear() => passages.Clear();

        public static void Register(Passage passage)
        {
            if (passage != null && !passages.Contains(passage))
            {
                passages.Add(passage);
            }
        }

        /// <summary>
        /// Which passage a point is in (the one it is most comfortably inside), with its s,
        /// cross-section offset and radial fraction there. False when there are no passages.
        /// </summary>
        public static bool Locate(Vector3 point, out Passage passage, out float s, out Vector2 local, out float radial)
        {
            passage = null;
            s = 0f;
            local = Vector2.zero;
            radial = float.MaxValue;

            foreach (Passage p in passages)
            {
                float ps = p.Project(point, out Vector2 pl, out float along);
                Vector2 r = p.RadiiAt(Mathf.Clamp(ps, p.MinTravel, p.MaxTravel));
                float pr = r.x > 1e-4f && r.y > 1e-4f
                    ? Mathf.Sqrt((pl.x / r.x) * (pl.x / r.x) + (pl.y / r.y) * (pl.y / r.y))
                    : float.MaxValue;
                pr += Mathf.Abs(along); // off the end of an open passage: belongs to whatever it joins
                if (passage == null || pr < radial)
                {
                    passage = p;
                    s = ps;
                    local = pl;
                    radial = pr;
                }
            }
            return passage != null;
        }

        /// <summary>Radial fraction in whichever passage the point is in (large if outside all).</summary>
        public static float RadialFraction(Vector3 point)
        {
            return Locate(point, out _, out _, out _, out float radial) ? radial : float.MaxValue;
        }

        /// <summary>
        /// Keeps a point inside the network. If it is already inside any passage (within that
        /// passage's limits) nothing happens; otherwise it is pulled back into the passage it has
        /// strayed least from. See Passage.ClampInside for the parameters.
        /// </summary>
        public static bool ClampInside(ref Vector3 point, float maxFraction, float margin, out Vector3 outwardNormal, float travelSlack = 0f)
        {
            outwardNormal = Vector3.zero;
            Passage best = null;
            float bestViolation = float.MaxValue;
            foreach (Passage p in passages)
            {
                float v = p.Violation(point, maxFraction, margin, travelSlack);
                if (v <= 0f)
                {
                    return false;
                }
                if (v < bestViolation)
                {
                    bestViolation = v;
                    best = p;
                }
            }
            return best != null && best.ClampInside(ref point, maxFraction, margin, travelSlack, out outwardNormal);
        }

        /// <summary>True if the point is inside a passage other than `self` (used to open the walls at a fork).</summary>
        public static bool InsideAnother(Passage self, Vector3 point, float fraction)
        {
            foreach (Passage p in passages)
            {
                if (p != self && p.RadialFraction(point) < fraction)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
