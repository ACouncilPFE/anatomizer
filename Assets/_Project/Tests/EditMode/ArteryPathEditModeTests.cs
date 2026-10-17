using AIGame.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AIGame.Tests.EditMode
{
    /// <summary>
    /// The pilot keeps the ship inside the artery using ArteryPath.ClampInside.
    /// </summary>
    public class ArteryPathEditModeTests
    {
        private const float MaxFraction = 0.85f;
        private const float Margin = 0.5f;

        [Test]
        public void Centerline_IsInsideTheWall()
        {
            for (float z = 0f; z <= ArteryPath.Length; z += 10f)
            {
                Assert.AreEqual(0f, ArteryPath.RadialFraction(ArteryPath.CenterAt(z)), 0.0001f);
            }
        }

        [Test]
        public void Artery_IsSealedAtBothEnds_AndOpenWhereTheShipTravels()
        {
            Assert.AreEqual(0f, ArteryPath.CapTaper(ArteryPath.StartZ), 0.0001f, "Near end must close.");
            Assert.AreEqual(0f, ArteryPath.CapTaper(ArteryPath.EndZ), 0.0001f, "Far end must close.");
            Assert.AreEqual(0f, ArteryPath.RadiiAt(ArteryPath.EndZ + 5f).x, 0.0001f, "Nothing beyond the sealed end.");

            for (float z = ArteryPath.MinTravelZ - 1f; z <= ArteryPath.MaxTravelZ + 1f; z += 1f)
            {
                Assert.AreEqual(1f, ArteryPath.CapTaper(z), 0.0001f, $"Vessel must be full width at z={z}.");
            }
        }

        [Test]
        public void ClampInside_LeavesPointsNearTheCentreAlone()
        {
            Vector3 point = ArteryPath.CenterAt(40f) + new Vector3(1f, 0.5f, 0f);
            Vector3 before = point;

            bool moved = ArteryPath.ClampInside(ref point, MaxFraction, Margin, 0f, ArteryPath.Length, out Vector3 normal);

            Assert.IsFalse(moved);
            Assert.AreEqual(before, point);
            Assert.AreEqual(Vector3.zero, normal);
        }

        [Test]
        public void ClampInside_PullsAPointOutsideTheWallBackIn()
        {
            float z = 60f;
            Vector3 point = ArteryPath.CenterAt(z) + new Vector3(20f, 0f, 0f); // well past the side wall

            bool moved = ArteryPath.ClampInside(ref point, MaxFraction, Margin, 0f, ArteryPath.Length, out Vector3 normal);

            Assert.IsTrue(moved);
            Assert.Less(ArteryPath.RadialFraction(point), 1f, "Point must end up inside the artery wall.");
            Assert.Greater(normal.x, 0.9f, "Wall normal should point out through the side the point left by.");
            Assert.AreEqual(z, point.z, 0.0001f);
        }

        [Test]
        public void ClampInside_StopsThePointLeavingEitherEnd()
        {
            Vector3 behind = ArteryPath.CenterAt(0f) + new Vector3(0f, 0f, -5f);
            ArteryPath.ClampInside(ref behind, MaxFraction, Margin, 0.5f, ArteryPath.Length - 3f, out Vector3 backNormal);
            Assert.AreEqual(0.5f, behind.z, 0.0001f);
            Assert.Less(backNormal.z, 0f);

            Vector3 beyond = new Vector3(0f, 0f, ArteryPath.Length + 20f);
            ArteryPath.ClampInside(ref beyond, MaxFraction, Margin, 0.5f, ArteryPath.Length - 3f, out Vector3 endNormal);
            Assert.AreEqual(ArteryPath.Length - 3f, beyond.z, 0.0001f);
            Assert.Greater(endNormal.z, 0f);
        }
    }
}
