using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The pilot crew member. Two jobs:
    ///
    ///   1. Always: keep the capsule inside the artery. After ShipController has moved the ship
    ///      each physics step, the pilot puts it back inside the wall (and short of both sealed ends)
    ///      and cancels any velocity heading into the wall. The mesh collider alone can't do this;
    ///      a fast capsule slips through a thin, hollow mesh.
    ///   2. Autopilot (toggle with P, or X on the left VR controller): the pilot flies the ship down
    ///      the artery, nose along the vessel and drifting back to the centreline. You can still
    ///      throttle (W/S), shift lanes (A/D, E/Q) and boost; the pilot only takes the stick when
    ///      you let go.
    ///
    /// In Assist mode (the default) you fly and the pilot only enforces the walls.
    /// </summary>
    [DefaultExecutionOrder(50)] // FixedUpdate after ShipController, so containment has the last word
    [RequireComponent(typeof(ShipController))]
    public class ShipPilot : MonoBehaviour
    {
        public enum Mode
        {
            Assist,
            Autopilot
        }

        [SerializeField] private Mode mode = Mode.Assist;

        [Header("Boundaries")]
        [SerializeField, Range(0.3f, 1f)] private float maxWallFraction = 0.85f; // how close to the wall the pilot allows
        [SerializeField] private float hullClearance = 0.5f;                      // metres kept between hull and wall
        // Travel along the vessel is limited to ArteryPath.MinTravelZ..MaxTravelZ, short of the sealed ends.

        [Header("Autopilot")]
        [SerializeField, Range(0f, 1f)] private float cruiseThrottle = 0.55f;
        [SerializeField] private float lookAheadDistance = 10f;
        [SerializeField] private float headingGain       = 3f;    // deg/s of turn per degree off-course
        [SerializeField] private float centeringGain     = 0.2f;  // strafe input per metre off the centreline
        [SerializeField] private float maxSteerInput     = 1.5f;
        [SerializeField] private float slowDownDistance  = 12f;   // ease off the throttle near the far end

        private const float InputDeadzone = 0.05f;

        private Rigidbody rb;
        private ShipController controller;
        private bool toggleHeld;
        private float lastCorrectionTime = -999f;

        public Mode CurrentMode => mode;
        /// <summary>True for a moment after the pilot had to push the ship back off a wall.</summary>
        public bool IsHoldingWall => Time.time - lastCorrectionTime < 0.35f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            controller = GetComponent<ShipController>();
            controller.SetPilot(this);
        }

        private void Update()
        {
            bool toggle = ShipInput.Read().TogglePilot;
            if (toggle && !toggleHeld)
            {
                SetMode(mode == Mode.Assist ? Mode.Autopilot : Mode.Assist);
            }
            toggleHeld = toggle;
        }

        public void SetMode(Mode newMode)
        {
            mode = newMode;
        }

        /// <summary>
        /// Called by ShipController with the player's input each frame. In Autopilot the pilot
        /// fills in whatever the player isn't doing; in Assist the input passes through untouched.
        /// </summary>
        public ShipInput.State Steer(ShipInput.State input)
        {
            if (mode != Mode.Autopilot)
            {
                return input;
            }

            Vector3 position = transform.position;

            // Heading: point the nose along the artery a little way ahead.
            float aheadZ = Mathf.Clamp(position.z + lookAheadDistance, ArteryPath.MinTravelZ, ArteryPath.MaxTravelZ);
            Vector3 localHeading = transform.InverseTransformDirection(ArteryPath.TangentAt(aheadZ));
            float yawError   = Mathf.Atan2(localHeading.x, localHeading.z) * Mathf.Rad2Deg;
            float pitchError = Mathf.Atan2(localHeading.y, new Vector2(localHeading.x, localHeading.z).magnitude) * Mathf.Rad2Deg;
            input.Yaw   = Mathf.Clamp(yawError   * headingGain / Mathf.Max(controller.YawSpeed,   1f), -maxSteerInput, maxSteerInput);
            input.Pitch = Mathf.Clamp(pitchError * headingGain / Mathf.Max(controller.PitchSpeed, 1f), -maxSteerInput, maxSteerInput);

            // Throttle: cruise unless the player is throttling, and ease off before the far end.
            if (Mathf.Abs(input.Forward) < InputDeadzone)
            {
                float remaining = ArteryPath.MaxTravelZ - position.z;
                input.Forward = cruiseThrottle * Mathf.Clamp01(remaining / Mathf.Max(slowDownDistance, 0.01f));
            }

            // Lane: drift back to the centreline on any axis the player isn't using.
            Vector3 toCenter = transform.InverseTransformDirection(ArteryPath.CenterAt(position.z) - position);
            if (Mathf.Abs(input.Strafe) < InputDeadzone)
            {
                input.Strafe = Mathf.Clamp(toCenter.x * centeringGain, -1f, 1f);
            }
            if (Mathf.Abs(input.Rise) < InputDeadzone)
            {
                input.Rise = Mathf.Clamp(toCenter.y * centeringGain, -1f, 1f);
            }

            return input;
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
            {
                return;
            }

            Vector3 position = rb.position;
            if (!ArteryPath.ClampInside(ref position, maxWallFraction, hullClearance, ArteryPath.MinTravelZ, ArteryPath.MaxTravelZ, out Vector3 outward))
            {
                return;
            }

            rb.position = position;
            transform.position = position;

            Vector3 velocity = rb.velocity;
            float intoWall = Vector3.Dot(velocity, outward);
            if (intoWall > 0f)
            {
                rb.velocity = velocity - outward * intoWall;
            }

            lastCorrectionTime = Time.time;
        }
    }
}
