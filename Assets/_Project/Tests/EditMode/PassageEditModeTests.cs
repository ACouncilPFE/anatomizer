using AIGame.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AIGame.Tests.EditMode
{
    /// <summary>
    /// Passages (the body's tubes) and the network that joins them: shape, sealed ends,
    /// containment, and flying from the main artery into a branch.
    /// </summary>
    public class PassageEditModeTests
    {
        private const float MaxFraction = 0.85f;
        private const float Margin = 0.5f;

        [SetUp]
        public void SetUp()
        {
            ArteryLayout.BuildMission1();
        }

        [TearDown]
        public void TearDown()
        {
            PassageNetwork.Clear();
        }

        private static Passage Main => PassageNetwork.Main;

        [Test]
        public void MainArtery_FollowsTheOriginalCentreline()
        {
            // The ship starts at the origin, inside the artery near its middle.
            Assert.Less(Main.RadialFraction(Vector3.zero), 0.6f);
            for (float z = 0f; z <= 140f; z += 20f)
            {
                Vector3 expected = ArteryLayout.MainCenterline(z);
                float s = Main.Project(expected, out Vector2 local, out _);
                Assert.Less(local.magnitude, 0.05f, $"Centreline drifted at z={z}");
                Assert.AreEqual(0f, Main.RadialFraction(Main.CenterAt(s)), 0.001f);
            }
        }

        [Test]
        public void MainArtery_IsSealedAtBothEnds_AndFullWidthWhereTheShipTravels()
        {
            Assert.AreEqual(0f, Main.CapTaper(0f), 0.0001f, "Near end must close.");
            Assert.AreEqual(0f, Main.CapTaper(Main.Length), 0.0001f, "Far end must close.");
            for (float s = Main.MinTravel; s <= Main.MaxTravel; s += 2f)
            {
                Assert.AreEqual(1f, Main.CapTaper(s), 0.0001f, $"Must be full width at s={s}.");
            }
        }

        [Test]
        public void Frames_StayPerpendicularToTheDirectionOfTravel()
        {
            foreach (Passage p in PassageNetwork.All)
            {
                for (float s = 0f; s <= p.Length; s += 5f)
                {
                    p.Frame(s, out _, out Vector3 tangent, out Vector3 right, out Vector3 up);
                    Assert.AreEqual(0f, Vector3.Dot(tangent, right), 0.02f, $"{p.Name} at {s}");
                    Assert.AreEqual(0f, Vector3.Dot(tangent, up), 0.02f, $"{p.Name} at {s}");
                    Assert.AreEqual(0f, Vector3.Dot(right, up), 0.02f, $"{p.Name} at {s}");
                }
            }
        }

        [Test]
        public void ClampInside_LeavesPointsNearTheCentreAlone()
        {
            Vector3 point = Main.PointAt(50f, 0.7f, 0.3f);
            Vector3 before = point;

            bool moved = PassageNetwork.ClampInside(ref point, MaxFraction, Margin, out Vector3 normal);

            Assert.IsFalse(moved);
            Assert.AreEqual(before, point);
            Assert.AreEqual(Vector3.zero, normal);
        }

        [Test]
        public void ClampInside_PullsAPointOutsideTheWallBackIn()
        {
            Main.Frame(60f, out Vector3 center, out _, out Vector3 right, out _);
            Vector3 point = center + right * 20f; // well past the side wall

            bool moved = PassageNetwork.ClampInside(ref point, MaxFraction, Margin, out Vector3 normal);

            Assert.IsTrue(moved);
            Assert.Less(Main.RadialFraction(point), 1f, "Point must end up inside the artery wall.");
            Assert.Greater(Vector3.Dot(normal, right), 0.9f, "Normal should point out through the wall it left by.");
        }

        [Test]
        public void ClampInside_StopsThePointLeavingEitherSealedEnd()
        {
            Vector3 behind = Main.CenterAt(0f) - Main.TangentAt(0f) * 5f;
            PassageNetwork.ClampInside(ref behind, MaxFraction, Margin, out _);
            Assert.AreEqual(Main.MinTravel, Main.Project(behind, out _, out _), 0.3f);

            Vector3 beyond = Main.CenterAt(Main.Length) + Main.TangentAt(Main.Length) * 20f;
            PassageNetwork.ClampInside(ref beyond, MaxFraction, Margin, out _);
            Assert.AreEqual(Main.MaxTravel, Main.Project(beyond, out _, out _), 0.3f);
        }

        [Test]
        public void Network_LetsTheShipIntoEitherBranch()
        {
            Passage left = PassageNetwork.All[1];
            Passage right = PassageNetwork.All[2];
            foreach (Passage branch in new[] { left, right })
            {
                // Well down the branch, far outside the main artery: still inside the body.
                Vector3 point = branch.PointAt(50f, 1f, 0.4f);
                Assert.Greater(Main.RadialFraction(point), 1f, $"{branch.Name} should leave the main artery.");

                Vector3 before = point;
                Assert.IsFalse(PassageNetwork.ClampInside(ref point, MaxFraction, Margin, out _), $"{branch.Name} point was clamped.");
                Assert.AreEqual(before, point);

                Assert.IsTrue(PassageNetwork.Locate(point, out Passage found, out _, out _, out _));
                Assert.AreSame(branch, found);
            }
        }

        [Test]
        public void Branches_SealAtTheirFarEnds()
        {
            Passage branch = PassageNetwork.All[1];
            Vector3 beyond = branch.CenterAt(branch.Length) + branch.TangentAt(branch.Length) * 15f;
            PassageNetwork.ClampInside(ref beyond, MaxFraction, Margin, out _);
            Assert.AreEqual(branch.MaxTravel, branch.Project(beyond, out _, out _), 0.5f);
        }
    }
}
