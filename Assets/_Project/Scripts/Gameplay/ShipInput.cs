using UnityEngine;
using UnityEngine.XR;

namespace AIGame.Gameplay
{
    /// <summary>
    /// One place that reads ship controls, so the same ship flies on a flat screen and in VR.
    ///
    /// Desktop (unchanged from before):
    ///   W/S forward, A/D strafe, E/Q rise/descend, Shift boost, Z/C roll, mouse steer.
    ///   Left click destroy round (laser), right click medicine round.
    ///   Surgical beam: 1-4 pick suture / stent / balloon / grabber, hold F to use it.
    ///   P autopilot on/off (ShipPilot), R co-pilot on/off (ShipCoPilot), V cockpit window (CockpitView).
    /// VR controllers (Quest-style, read through UnityEngine.XR so it works with any OpenXR headset):
    ///   Left stick   throttle (Y) and strafe (X)
    ///   Right stick  steer: yaw (X) and pitch (Y)
    ///   Left trigger boost, right trigger destroy round, B (right) medicine round
    ///   Right grip   rise, left grip descend
    ///   X (left)     autopilot on/off, A (right) co-pilot on/off, Y (left) surgical beam,
    ///                left stick click cycles the beam tool
    ///   No roll in VR: the cockpit stays level for comfort.
    ///
    /// Both sources are added together, so a keyboard still works while a headset is on.
    /// </summary>
    public static class ShipInput
    {
        public struct State
        {
            public float Forward;  // -1..+1
            public float Strafe;   // -1..+1
            public float Rise;     // -1..+1
            public float Yaw;      // mouse-delta units (desktop) or -1..+1 (stick)
            public float Pitch;    // mouse-delta units (desktop) or -1..+1 (stick)
            public float Roll;     // -1..+1
            public bool  Boost;
            public bool  Fire;           // destroy round
            public bool  FireMedicine;   // medicine round
            public bool  Beam;           // surgical beam (held)
            public bool  CycleBeamTool;  // held state; SurgicalBeam acts on the press
            public int   BeamToolSelect; // 1-4 when a number key is pressed this frame, else 0
            public bool  ToggleCockpitView; // held state; CockpitView acts on the press
            public bool  TogglePilot;    // held state; ShipPilot acts on the press
            public bool  ToggleCoPilot;  // held state; ShipCoPilot acts on the press
        }

        private const float StickDeadzone = 0.15f;
        private const float ButtonThreshold = 0.5f;

        /// <summary>True when a headset is running, so the game is in VR mode.</summary>
        public static bool IsXRActive => XRSettings.isDeviceActive;

        public static State Read()
        {
            State s = ReadDesktop();
            if (IsXRActive)
            {
                AddXR(ref s);
            }
            return s;
        }

        private static State ReadDesktop()
        {
            return new State
            {
                Forward = Input.GetAxis("Vertical"),
                Strafe  = Input.GetAxis("Horizontal"),
                Rise    = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f),
                Yaw     = Input.GetAxis("Mouse X"),
                Pitch   = Input.GetAxis("Mouse Y"),
                Roll    = (Input.GetKey(KeyCode.Z) ? 1f : 0f) - (Input.GetKey(KeyCode.C) ? 1f : 0f),
                Boost   = Input.GetKey(KeyCode.LeftShift),
                Fire    = Input.GetMouseButton(0),
                FireMedicine  = Input.GetMouseButton(1),
                Beam          = Input.GetKey(KeyCode.F),
                ToggleCockpitView = Input.GetKey(KeyCode.V),
                BeamToolSelect = Input.GetKeyDown(KeyCode.Alpha1) ? 1 : Input.GetKeyDown(KeyCode.Alpha2) ? 2
                               : Input.GetKeyDown(KeyCode.Alpha3) ? 3 : Input.GetKeyDown(KeyCode.Alpha4) ? 4 : 0,
                TogglePilot   = Input.GetKey(KeyCode.P),
                ToggleCoPilot = Input.GetKey(KeyCode.R),
            };
        }

        private static void AddXR(ref State s)
        {
            InputDevice left  = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            InputDevice right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            Vector2 leftStick  = Deadzone(ReadAxis2D(left));
            Vector2 rightStick = Deadzone(ReadAxis2D(right));

            s.Forward = Mathf.Clamp(s.Forward + leftStick.y, -1f, 1f);
            s.Strafe  = Mathf.Clamp(s.Strafe  + leftStick.x, -1f, 1f);
            s.Yaw    += rightStick.x;
            s.Pitch  += rightStick.y;

            float rise = (ReadFloat(right, CommonUsages.grip) > ButtonThreshold ? 1f : 0f)
                       - (ReadFloat(left,  CommonUsages.grip) > ButtonThreshold ? 1f : 0f);
            s.Rise = Mathf.Clamp(s.Rise + rise, -1f, 1f);

            s.Boost |= ReadFloat(left,  CommonUsages.trigger) > ButtonThreshold;
            s.Fire  |= ReadFloat(right, CommonUsages.trigger) > ButtonThreshold;

            s.FireMedicine  |= ReadBool(right, CommonUsages.secondaryButton);
            s.Beam          |= ReadBool(left,  CommonUsages.secondaryButton);
            s.CycleBeamTool |= ReadBool(left,  CommonUsages.primary2DAxisClick);
            s.TogglePilot   |= ReadBool(left,  CommonUsages.primaryButton);
            s.ToggleCoPilot |= ReadBool(right, CommonUsages.primaryButton);
        }

        private static bool ReadBool(InputDevice device, InputFeatureUsage<bool> usage)
        {
            return device.isValid && device.TryGetFeatureValue(usage, out bool v) && v;
        }

        private static Vector2 ReadAxis2D(InputDevice device)
        {
            return device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 v) ? v : Vector2.zero;
        }

        private static float ReadFloat(InputDevice device, InputFeatureUsage<float> usage)
        {
            return device.isValid && device.TryGetFeatureValue(usage, out float v) ? v : 0f;
        }

        private static Vector2 Deadzone(Vector2 v)
        {
            return v.magnitude < StickDeadzone ? Vector2.zero : v;
        }
    }
}
