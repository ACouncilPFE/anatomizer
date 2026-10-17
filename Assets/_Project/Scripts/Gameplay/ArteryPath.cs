using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// The shape of the Mission 01 artery, in one place.
    ///
    /// The tunnel mesh (ArteryEnvironment), the ship's tube assist and the pilot's containment all
    /// read from here, so the walls the player sees are the walls the crew steers by. The artery
    /// runs along +Z from the ship's start point; each cross-section is an ellipse whose centre
    /// wanders sideways and whose radius pulses slightly with Z.
    ///
    /// The vessel is closed: past StartZ..EndZ the walls don't exist, and over the last CapLength
    /// metres at each end the radius rounds off to zero, so both ends are sealed domes.
    /// </summary>
    public static class ArteryPath
    {
        public const float Length  = 150f;
        public const float RadiusX = 6.5f;
        public const float RadiusY = 4f;

        // The sealed vessel extends a little past the play length at both ends.
        public const float StartZ    = -16f;
        public const float EndZ      = Length + 16f;
        public const float CapLength = 8f;

        /// <summary>How far the ship (and anything else that moves) may travel along the artery.</summary>
        public const float MinTravelZ = StartZ + CapLength + 2f;
        public const float MaxTravelZ = EndZ - CapLength - 2f;

        /// <summary>Centre of the cross-section at depth z.</summary>
        public static Vector3 CenterAt(float z)
        {
            return new Vector3(Mathf.Sin(z * 0.045f) * 2.6f, Mathf.Sin(z * 0.03f + 1.1f) * 1.6f, z);
        }

        /// <summary>Wall radii (x, y) at depth z, including the pulse and the closed end caps.</summary>
        public static Vector2 RadiiAt(float z)
        {
            float scale = (1f + Mathf.Sin(z * 0.11f) * 0.06f) * CapTaper(z);
            return new Vector2(RadiusX * scale, RadiusY * scale);
        }

        /// <summary>1 along the open vessel, rounding off to 0 at StartZ and EndZ (a dome).</summary>
        public static float CapTaper(float z)
        {
            float d = 0f;
            if (z < StartZ + CapLength)
            {
                d = (StartZ + CapLength - z) / CapLength;
            }
            else if (z > EndZ - CapLength)
            {
                d = (z - (EndZ - CapLength)) / CapLength;
            }
            d = Mathf.Clamp01(d);
            return Mathf.Sqrt(1f - d * d);
        }

        /// <summary>Direction the artery runs at depth z (unit length).</summary>
        public static Vector3 TangentAt(float z)
        {
            const float step = 0.5f;
            return (CenterAt(z + step) - CenterAt(z - step)).normalized;
        }

        /// <summary>
        /// How far a point is from the centreline, as a fraction of the wall: 0 at the centre,
        /// 1 on the wall, above 1 outside it.
        /// </summary>
        public static float RadialFraction(Vector3 point)
        {
            Vector3 center = CenterAt(point.z);
            Vector2 radii  = RadiiAt(point.z);
            float nx = (point.x - center.x) / radii.x;
            float ny = (point.y - center.y) / radii.y;
            return Mathf.Sqrt(nx * nx + ny * ny);
        }

        /// <summary>
        /// Keeps a point inside the artery.
        ///   maxFraction  how close to the wall the point may go (0..1 of the wall radius).
        ///   margin       extra clearance in metres (for example the hull radius).
        ///   zMin / zMax  how far along the artery the point may go.
        /// Returns true if the point had to be moved. outwardNormal is the wall direction it was
        /// pushed back from (zero when nothing moved), so callers can cancel velocity into the wall.
        /// </summary>
        public static bool ClampInside(ref Vector3 point, float maxFraction, float margin, float zMin, float zMax, out Vector3 outwardNormal)
        {
            outwardNormal = Vector3.zero;
            bool moved = false;

            if (point.z < zMin)
            {
                point.z = zMin;
                outwardNormal += Vector3.back;
                moved = true;
            }
            else if (point.z > zMax)
            {
                point.z = zMax;
                outwardNormal += Vector3.forward;
                moved = true;
            }

            Vector3 center = CenterAt(point.z);
            Vector2 radii  = RadiiAt(point.z);
            float rx = Mathf.Max(0.1f, radii.x * maxFraction - margin);
            float ry = Mathf.Max(0.1f, radii.y * maxFraction - margin);

            float ox = point.x - center.x;
            float oy = point.y - center.y;
            float n  = Mathf.Sqrt((ox / rx) * (ox / rx) + (oy / ry) * (oy / ry));

            if (n > 1f)
            {
                ox /= n;
                oy /= n;
                point.x = center.x + ox;
                point.y = center.y + oy;
                // Gradient of the ellipse at the clamped point points straight out through the wall.
                outwardNormal += new Vector3(ox / (rx * rx), oy / (ry * ry), 0f).normalized;
                moved = true;
            }

            if (moved)
            {
                outwardNormal.Normalize();
            }
            return moved;
        }
    }
}
