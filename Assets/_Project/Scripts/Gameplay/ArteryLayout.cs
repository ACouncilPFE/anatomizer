using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The passages of Mission 1, Arterial Descent: the main artery (sealed at both ends), which
    /// forks near its far end into two narrower branches, one curving up and left and one down and
    /// right. The main artery also carries on past the fork to its own sealed end, so there are
    /// three ways to go.
    ///
    /// The main artery starts behind the ship's start point (world origin) and runs along +Z, with
    /// the same gentle wander it has always had.
    /// </summary>
    public static class ArteryLayout
    {
        public static readonly Vector2 MainRadii = new Vector2(6.5f, 4f);
        public static readonly Vector2 BranchRadii = new Vector2(4.4f, 3.1f);

        /// <summary>World z where the main artery begins and ends.</summary>
        public const float MainStartZ = -16f;
        public const float MainEndZ = 166f;
        /// <summary>World z where the branches leave the main artery.</summary>
        public const float ForkZ = 118f;

        /// <summary>The original artery centreline: a gentle sideways and vertical wander.</summary>
        public static Vector3 MainCenterline(float z)
        {
            return new Vector3(Mathf.Sin(z * 0.045f) * 2.6f, Mathf.Sin(z * 0.03f + 1.1f) * 1.6f, z);
        }

        public static Passage MainArtery()
        {
            List<Vector3> points = new List<Vector3>();
            for (float z = MainStartZ; z <= MainEndZ + 0.01f; z += 4f)
            {
                points.Add(MainCenterline(z));
            }
            return new Passage("Main artery", points, MainRadii, true, true)
            {
                PulsePhase = MainStartZ * 0.11f, // keep the ripple where it always was
            };
        }

        /// <summary>A branch leaving the main artery at the fork; side = -1 for up-left, +1 for down-right.</summary>
        public static Passage Branch(int side)
        {
            Vector3 start = MainCenterline(ForkZ);
            Vector3[] offsets =
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1.6f, 0.6f, 6f),
                new Vector3(6f, 2.2f, 16f),
                new Vector3(12f, 3.6f, 28f),
                new Vector3(17f, 4.2f, 42f),
                new Vector3(20f, 4.4f, 58f),
                new Vector3(21.5f, 4.4f, 74f),
                new Vector3(22f, 4.4f, 88f),
            };

            List<Vector3> points = new List<Vector3>();
            foreach (Vector3 o in offsets)
            {
                // Left branch climbs, right branch dips.
                points.Add(start + new Vector3(o.x * side, -o.y * side, o.z));
            }

            return new Passage(side < 0 ? "Left branch" : "Right branch", points, BranchRadii, false, true)
            {
                // Narrows a little as it goes.
                WidthAt = s => Mathf.Lerp(1f, 0.85f, Mathf.Clamp01(s / 90f)),
                PulsePhase = side * 1.7f,
            };
        }

        /// <summary>Builds Mission 1's passages and makes them the current network.</summary>
        public static void BuildMission1()
        {
            PassageNetwork.Clear();
            PassageNetwork.Register(MainArtery());
            PassageNetwork.Register(Branch(-1));
            PassageNetwork.Register(Branch(1));
        }
    }
}
