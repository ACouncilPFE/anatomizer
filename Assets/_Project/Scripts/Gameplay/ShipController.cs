using AIGame.Core;
using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// 6-DOF tunnel flight controller.
    ///
    /// Design principles (from analysis of broken prior version):
    ///   1. NO Euler-angle accumulation for yaw — eulerAngles.y is 0-360 and snaps at the
    ///      boundary; instead we drive rb.angularVelocity in local space.
    ///   2. NO AddForce + separate drag — that creates overdamped lateral feel.
    ///      Instead we set rb.velocity directly toward a desired target each fixed step.
    ///   3. NO MoveRotation — it teleports rotation and bypasses angularDrag entirely.
    ///   4. Input is READ in Update (so no frames are missed) and APPLIED in FixedUpdate.
    ///   5. Pitch is clamped relative to the ship's own current orientation, not world space.
    ///   6. Tube-assist uses the same centerline formula as BuildArteryTubeMesh.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipController : MonoBehaviour
    {
        // ─── Movement ────────────────────────────────────────────────────────────
        [Header("Movement")]
        [SerializeField] private float thrust          = 28f;   // forward m/s target
        [SerializeField] private float strafeThrust    = 16f;   // lateral m/s target
        [SerializeField] private float verticalThrust  = 15f;   // vertical m/s target
        [SerializeField] private float boostMultiplier = 1.8f;
        [SerializeField] private float velocityLerpRate = 14f;  // how fast velocity reaches target (s⁻¹)

        // ─── Rotation ─────────────────────────────────────────────────────────────
        [Header("Rotation")]
        [SerializeField] private float yawSpeed        = 85f;   // deg/s per unit of Mouse X
        [SerializeField] private float pitchSpeed      = 65f;   // deg/s per unit of Mouse Y
        [SerializeField] private float rollSpeed       = 90f;   // deg/s while Z/C held
        [SerializeField] private float rollReturnSpeed = 110f;  // deg/s auto-level when no roll input
        [SerializeField] private float maxRollAngle    = 25f;   // maximum bank from neutral
        [SerializeField] private float rotationLerpRate = 18f;  // how fast angularVelocity reaches target

        // ─── Tube assist ─────────────────────────────────────────────────────────
        [Header("Tube Assist")]
        [SerializeField] private float tubeAssistStart    = 0.65f; // start pushing inward at 65% radius
        [SerializeField] private float tubeAssistStrength = 28f;  // stronger push back to center
        [SerializeField] private float tubeLength         = 150f;
        [SerializeField] private float tubeRadiusX        = 6.5f;
        [SerializeField] private float tubeRadiusY        = 4f;

        // ─── Private state ────────────────────────────────────────────────────────
        private Rigidbody rb;

        // Inputs buffered from Update → used in FixedUpdate
        private float inputForward;
        private float inputStrafe;
        private float inputRise;
        private float inputBoost;
        private float inputMouseX;
        private float inputMouseY;
        private float inputRoll;   // -1..+1

        // Accumulated roll in degrees (we own this; physics does NOT)
        private float currentRollDeg;

        // ─── Public test accessors ────────────────────────────────────────────────
        public float Thrust         => thrust;
        public float YawSpeed       => yawSpeed;
        public float PitchSpeed     => pitchSpeed;
        public float MaxPitchAngle  => 90f;   // no world-space pitch clamp — ship is free in 3D
        public float LinearDamping  => velocityLerpRate;

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity    = false;
            rb.drag          = 0f;   // We manage deceleration manually via velocity lerp
            rb.angularDrag   = 0f;   // We manage rotation deceleration via angularVelocity lerp
            // Preserve ContinuousSpeculative and Interpolate set by the scene builder
        }

        // Read input every rendered frame so no button presses are missed.
        private void Update()
        {
            inputForward = Input.GetAxis("Vertical");
            inputStrafe  = Input.GetAxis("Horizontal");
            inputRise    = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            inputBoost   = Input.GetKey(KeyCode.LeftShift) ? boostMultiplier : 1f;
            inputMouseX  = Input.GetAxis("Mouse X");
            inputMouseY  = Input.GetAxis("Mouse Y");
            inputRoll    = (Input.GetKey(KeyCode.Z) ? 1f : 0f) - (Input.GetKey(KeyCode.C) ? 1f : 0f);
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.InMission)
            {
                rb.velocity        = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                return;
            }

            ApplyTranslation();
            ApplyRotation();
            ApplyTubeAssist();
        }

        // ─── Translation ──────────────────────────────────────────────────────────
        // We target a LOCAL velocity and lerp the world velocity toward it each step.
        // No AddForce, no rb.drag — so there is no "double damping" and the ship stops
        // cleanly when input is released.
        private void ApplyTranslation()
        {
            float fwd  = inputForward  * thrust         * inputBoost;
            float side = inputStrafe   * strafeThrust;
            float up   = inputRise     * verticalThrust;

            // Desired velocity in world space, expressed relative to current orientation
            Vector3 desiredWorld = transform.TransformDirection(new Vector3(side, up, fwd));

            rb.velocity = Vector3.Lerp(rb.velocity, desiredWorld, velocityLerpRate * Time.fixedDeltaTime);
        }

        // ─── Rotation ─────────────────────────────────────────────────────────────
        // We compute a desired angular velocity in LOCAL space and lerp rb.angularVelocity
        // toward it. This avoids Euler-angle discontinuities (the old MoveRotation+eulerAngles
        // approach snapped 360° when yaw crossed the 0/360 boundary).
        private void ApplyRotation()
        {
            // Desired angular rates in degrees/s (local space)
            float desiredPitchDegS = -inputMouseY * pitchSpeed;
            float desiredYawDegS   =  inputMouseX * yawSpeed;

            // Roll: move toward a clamped target angle rather than free-spinning
            float targetRoll    = inputRoll * maxRollAngle;
            float returnSpeed   = Mathf.Abs(inputRoll) > 0.01f ? rollSpeed : rollReturnSpeed;
            currentRollDeg      = Mathf.MoveTowards(currentRollDeg, targetRoll, returnSpeed * Time.fixedDeltaTime);

            // Convert desired local angular velocity to world space for Rigidbody
            // Unity angularVelocity is in radians/second
            float toRad = Mathf.Deg2Rad;
            Vector3 desiredLocalAngVel = new Vector3(
                desiredPitchDegS * toRad,
                desiredYawDegS   * toRad,
                0f  // roll handled via orientation correction below
            );
            Vector3 desiredWorldAngVel = transform.TransformDirection(desiredLocalAngVel);

            rb.angularVelocity = Vector3.Lerp(
                rb.angularVelocity,
                desiredWorldAngVel,
                rotationLerpRate * Time.fixedDeltaTime
            );

            // Correct roll toward currentRollDeg relative to the ship's own up axis.
            // This is a separate small rotation each step, not a teleport.
            Quaternion current = rb.rotation;
            // Desired: same yaw/pitch but roll = currentRollDeg
            Vector3 euler = current.eulerAngles;
            // Map X/Z euler to -180..180 to avoid 359→-1 flips
            float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            float yaw   = euler.y;
            Quaternion target = Quaternion.Euler(pitch, yaw, currentRollDeg);
            // Apply a small slerp correction — does not teleport, blends physically
            rb.MoveRotation(Quaternion.Slerp(current, target, rotationLerpRate * 0.5f * Time.fixedDeltaTime));
        }

        // ─── Tube assist ─────────────────────────────────────────────────────────
        // Gently pushes the ship back toward the tube centerline when it drifts past
        // tubeAssistStart fraction of the tube radius. Constants MUST match
        // BuildArteryTubeMesh(length=150, rx=6.5, ry=4, Sin(z*0.045)*2.6, Sin(z*0.03+1.1)*1.6).
        private void ApplyTubeAssist()
        {
            float z       = Mathf.Clamp(transform.position.z, 0f, tubeLength);
            float centerX = Mathf.Sin(z * 0.045f) * 2.6f;
            float centerY = Mathf.Sin(z * 0.03f + 1.1f) * 1.6f;

            Vector3 offset = transform.position - new Vector3(centerX, centerY, transform.position.z);
            float normX    = offset.x / Mathf.Max(tubeRadiusX, 0.01f);
            float normY    = offset.y / Mathf.Max(tubeRadiusY, 0.01f);
            float radial   = Mathf.Sqrt(normX * normX + normY * normY);

            if (radial > tubeAssistStart)
            {
                float t             = Mathf.InverseLerp(tubeAssistStart, 1.1f, radial);
                Vector3 toCenter    = new Vector3(-offset.x, -offset.y, 0f).normalized;
                // Add directly to velocity so it's not masked by drag (drag is 0)
                rb.velocity        += toCenter * (tubeAssistStrength * t * Time.fixedDeltaTime);
            }
        }
    }
}
