using AIGame.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace AIGame.Tests.EditMode
{
    /// <summary>
    /// Validates that ShipController's configured values produce
    /// manageable, human-playable flight characteristics.
    ///
    /// Current controller design uses:
    ///   - Velocity targeting (no Rigidbody.drag damping)
    ///   - Angular-velocity steering (no Euler accumulation)
    ///   - Tube assist near walls for recoverability
    /// </summary>
    public class ShipControllerBehaviorTests
    {
        private GameObject shipObj;
        private ShipController controller;

        [SetUp]
        public void SetUp()
        {
            shipObj = new GameObject("Ship");
            shipObj.AddComponent<Rigidbody>();
            controller = shipObj.AddComponent<ShipController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(shipObj);
        }

        // --- Turn speed: must be slow enough to aim precisely but fast enough to dodge ---
        [Test]
        public void YawSpeed_IsWithinManageableRange()
        {
            // Above 90 gets twitchy in a narrow tube. Below 35 feels sluggish.
            Assert.IsTrue(controller.YawSpeed >= 35f && controller.YawSpeed <= 90f,
                $"YawSpeed {controller.YawSpeed} is outside manageable range [35, 90]. " +
                "Too high = twitchy; too low = unresponsive.");
        }

        [Test]
        public void PitchSpeed_IsWithinManageableRange()
        {
            // Pitch should be a bit slower than old model for curved tunnel tracking.
            Assert.IsTrue(controller.PitchSpeed >= 25f && controller.PitchSpeed <= 70f,
                $"PitchSpeed {controller.PitchSpeed} outside manageable range [25, 70].");
        }

        // --- Pitch freedom: new model allows full-space orientation ---
        [Test]
        public void MaxPitchAngle_ReportsThreeDimensionalFreedom()
        {
            Assert.GreaterOrEqual(controller.MaxPitchAngle, 80f,
                $"MaxPitchAngle {controller.MaxPitchAngle} should reflect unconstrained 3D pitch behavior.");
        }

        // --- Thrust: must be enough to traverse a 150-unit tunnel in reasonable time ---
        [Test]
        public void Thrust_IsEnoughToTraverseTunnel()
        {
            Assert.IsTrue(controller.Thrust >= 12f && controller.Thrust <= 28f,
                $"Thrust {controller.Thrust} outside playable range [12, 28].");
        }

        // --- Velocity damping rate: controls settling without relying on Rigidbody.drag ---
        [Test]
        public void LinearDamping_IsWithinPlayableVelocityLerpRange()
        {
            // This property now reflects velocity lerp rate, not Rigidbody.drag.
            Assert.IsTrue(controller.LinearDamping >= 5f && controller.LinearDamping <= 16f,
                $"LinearDamping {controller.LinearDamping} outside playable range [5, 16]. " +
                "Too low = endless drift; too high = sluggish.");
        }

        // --- Rigidbody wiring ---
        [Test]
        public void ShipController_SetsUpRigidbody_WithNoGravity()
        {
            Rigidbody rb = shipObj.GetComponent<Rigidbody>();
            Assert.IsFalse(rb.useGravity, "Ship Rigidbody must have gravity disabled.");
        }

        [Test]
        public void ShipController_UsesManualVelocityDamping_InsteadOfRigidBodyDrag()
        {
            Rigidbody rb = shipObj.GetComponent<Rigidbody>();
            Assert.AreEqual(0f, rb.drag, 0.01f,
                "Rigidbody.drag should be zero because ShipController handles damping directly.");
        }
    }
}
