# anatomizer

Unity 2021.3 project for Pulse Vector: Inner Frontier, a shrunken-ship voyage through the body. One build plays on a monitor and in a VR headset.

## Desktop and VR

The ship reads its controls through `ShipInput`, which merges keyboard and mouse with VR controllers. When a headset is running, `VRCockpitRig` seats the player in a first-person cockpit on the ship and turns off the chase camera. Without a headset the game runs exactly as before.

| Action | Desktop | VR controllers |
|--------|---------|----------------|
| Throttle / strafe | W/S, A/D | Left stick |
| Steer | Mouse | Right stick |
| Rise / descend | E / Q | Right grip / left grip |
| Boost | Left Shift | Left trigger |
| Fire | Left click | Right trigger |
| Roll | Z / C | None, the cockpit stays level for comfort |

### One-time XR setup in the editor

The packages (XR Interaction Toolkit, OpenXR, Input System) come in through `Packages/manifest.json`. Unity installs them the first time you open the project. Then:

1. If Unity asks to restart for the new Input System, choose **Yes**. Active Input Handling is set to **Both**, so the existing keyboard and mouse code keeps working.
2. Open **Edit > Project Settings > XR Plug-in Management**. Unity creates the XR settings the first time you open this page.
3. On the **PC** tab, tick **OpenXR** and leave **Initialize XR on Startup** on. With no headset connected, OpenXR fails to start and the game runs on the monitor.
4. Under **OpenXR** on the PC tab, add an interaction profile for your controllers, such as **Oculus Touch Controller Profile**.
5. For Quest standalone builds, switch to the **Android** tab, tick **OpenXR**, enable the **Meta Quest Support** feature, and add the same controller profile. Set the minimum Android API level to 29 or higher in Player Settings.

To play in VR from the editor, connect the headset (Quest Link or Air Link on PC), then press Play.
