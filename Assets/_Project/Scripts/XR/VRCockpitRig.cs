using AIGame.Gameplay;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

namespace AIGame.XR
{
    /// <summary>
    /// Seats the player in a first-person cockpit on the ship when a VR headset is running,
    /// and turns off the desktop chase camera. On a flat screen it does nothing, so one build
    /// covers both.
    ///
    /// It attaches itself to the ship in every loaded scene, so no scene edits are needed.
    /// The rig is an XR Origin (from the XR Interaction Toolkit's core utilities) parented to
    /// the ship, so the cockpit is a fixed frame of reference while the head looks freely.
    /// </summary>
    public class VRCockpitRig : MonoBehaviour
    {
        // Eye position relative to the ship's origin: just above the hull, slightly forward.
        private static readonly Vector3 SeatOffset = new Vector3(0f, 0.45f, 0.2f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded += (scene, mode) => AttachToShip();
            AttachToShip();
        }

        private static void AttachToShip()
        {
            if (!ShipInput.IsXRActive)
            {
                return;
            }

            ShipController ship = FindObjectOfType<ShipController>();
            if (ship == null || ship.GetComponentInChildren<VRCockpitRig>(true) != null)
            {
                return;
            }

            // Build the rig while inactive so XROrigin.Awake sees its camera and offset already set.
            GameObject rigObj = new GameObject("VR Cockpit Rig");
            rigObj.SetActive(false);
            rigObj.transform.SetParent(ship.transform, false);
            rigObj.transform.localPosition = SeatOffset;
            rigObj.transform.localRotation = Quaternion.identity;
            rigObj.AddComponent<VRCockpitRig>();

            GameObject offsetObj = new GameObject("Camera Offset");
            offsetObj.transform.SetParent(rigObj.transform, false);

            GameObject camObj = new GameObject("VR Camera");
            camObj.transform.SetParent(offsetObj.transform, false);
            Camera cam = camObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 360f;
            camObj.AddComponent<AudioListener>();

            TrackedPoseDriver poseDriver = camObj.AddComponent<TrackedPoseDriver>();
            poseDriver.positionInput = new InputActionProperty(
                new InputAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            poseDriver.rotationInput = new InputActionProperty(
                new InputAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));

            XROrigin origin = rigObj.AddComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offsetObj;
            // Seated cockpit: track the head relative to where the headset started.
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0f;

            // Turn off the desktop chase camera before the VR camera takes over as MainCamera.
            foreach (CameraFollow follow in FindObjectsOfType<CameraFollow>())
            {
                follow.gameObject.SetActive(false);
            }

            camObj.tag = "MainCamera";
            rigObj.SetActive(true);
        }
    }
}
