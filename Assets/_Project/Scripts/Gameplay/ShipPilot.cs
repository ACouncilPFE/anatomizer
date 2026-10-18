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
    /// In Assist mode (the default) you fly. Besides enforcing the walls, the pilot steers you off
    /// a wall before you reach it, and (only while you aren't touching the steering) brings the
    /// nose back if it swings too far off the line of the artery, so you can still aim at things.
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
        // Travel along each passage is limited to its MinTravel..MaxTravel, short of any sealed end.

        [Header("Assist")]
        [SerializeField] private bool steeringAssist = true;
        [SerializeField, Range(0f, 1f)] private float wallAvoidStart = 0.5f;  // push off the wall beyond this share of the radius
        [SerializeField, Range(0f, 90f)] private float noseFreedom = 25f;     // degrees off the artery line before the nose is brought back
        [SerializeField] private float noseReturnGain = 2f;

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
        /// fills in whatever the player isn't doing; in Assist it only adds the steering help.
        /// </summary>
        public ShipInput.State Steer(ShipInput.State input)
        {
            if (mode != Mode.Autopilot)
            {
                return steeringAssist ? AssistSteering(input) : input;
            }

            Vector3 position = transform.position;
            if (!PassageNetwork.Locate(position, out Passage passage, out float s, out _, out _))
            {
                return input;
            }

            // Heading: point the nose along the passage a little way ahead.
            float aheadS = Mathf.Clamp(s + lookAheadDistance, passage.MinTravel, passage.MaxTravel);
            Vector3 localHeading = transform.InverseTransformDirection(passage.TangentAt(aheadS));
            float yawError   = Mathf.Atan2(localHeading.x, localHeading.z) * Mathf.Rad2Deg;
            float pitchError = Mathf.Atan2(localHeading.y, new Vector2(localHeading.x, localHeading.z).magnitude) * Mathf.Rad2Deg;
            input.Yaw   = Mathf.Clamp(yawError   * headingGain / Mathf.Max(controller.YawSpeed,   1f), -maxSteerInput, maxSteerInput);
            input.Pitch = Mathf.Clamp(pitchError * headingGain / Mathf.Max(controller.PitchSpeed, 1f), -maxSteerInput, maxSteerInput);

            // Throttle: cruise unless the player is throttling, and ease off before the far end.
            if (Mathf.Abs(input.Forward) < InputDeadzone)
            {
                float remaining = passage.CapEnd ? passage.MaxTravel - s : float.MaxValue;
                input.Forward = cruiseThrottle * Mathf.Clamp01(remaining / Mathf.Max(slowDownDistance, 0.01f));
            }

            // Lane: drift back to the centreline on any axis the player isn't using.
            Vector3 toCenter = transform.InverseTransformDirection(passage.CenterAt(s) - position);
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

        // Gentle help for a human pilot: shove away from walls before contact, and pull the nose
        // back toward the artery line when it has swung far off and the player isn't steering.
        private ShipInput.State AssistSteering(ShipInput.State input)
        {
            Vector3 position = transform.position;
            if (!PassageNetwork.Locate(position, out Passage passage, out float s, out _, out float radial))
            {
                return input;
            }
            Vector3 center = passage.CenterAt(s);

            if (radial > wallAvoidStart)
            {
                float push = Mathf.Clamp01((radial - wallAvoidStart) / Mathf.Max(maxWallFraction - wallAvoidStart, 0.01f));
                Vector3 toCenter = transform.InverseTransformDirection(center - position).normalized;
                input.Strafe = Mathf.Clamp(input.Strafe + toCenter.x * push * 1.5f, -1f, 1f);
                input.Rise = Mathf.Clamp(input.Rise + toCenter.y * push * 1.5f, -1f, 1f);
                // Nose pointing into the wall: ease off the throttle instead of driving into it.
                if (toCenter.z < 0f && input.Forward > 0f)
                {
                    input.Forward *= 1f - push * Mathf.Clamp01(-toCenter.z);
                }
            }

            bool steering = Mathf.Abs(input.Yaw) > InputDeadzone || Mathf.Abs(input.Pitch) > InputDeadzone;
            if (!steering)
            {
                float aheadS = Mathf.Clamp(s + lookAheadDistance, passage.MinTravel, passage.MaxTravel);
                Vector3 line = passage.TangentAt(aheadS);
                // Flying backwards down the artery is fine too: use whichever way the nose points.
                if (Vector3.Dot(transform.forward, line) < 0f)
                {
                    line = -line;
                }

                float off = Vector3.Angle(transform.forward, line);
                if (off > noseFreedom)
                {
                    Vector3 local = transform.InverseTransformDirection(line);
                    float excess = (off - noseFreedom) / off;
                    float yawError = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg * excess;
                    float pitchError = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg * excess;
                    input.Yaw = Mathf.Clamp(yawError * noseReturnGain / Mathf.Max(controller.YawSpeed, 1f), -maxSteerInput, maxSteerInput);
                    input.Pitch = Mathf.Clamp(pitchError * noseReturnGain / Mathf.Max(controller.PitchSpeed, 1f), -maxSteerInput, maxSteerInput);
                }
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
            if (!PassageNetwork.ClampInside(ref position, maxWallFraction, hullClearance, out Vector3 outward))
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
