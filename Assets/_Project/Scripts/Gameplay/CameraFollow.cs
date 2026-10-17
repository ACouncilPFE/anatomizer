using UnityEngine;

namespace AIGame.Gameplay
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -12f);
        [SerializeField] private float positionSmoothTime = 0.15f;
        [SerializeField] private float lookSmoothTime = 0.08f;
        [SerializeField] private float lookAheadDistance = 10f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (target == null) return;

            // Keep camera horizon stable by applying only yaw to follow offset.
            Quaternion yawOnly = Quaternion.Euler(0f, target.eulerAngles.y, 0f);
            Vector3 desiredPosition = target.position + yawOnly * offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _velocity, positionSmoothTime);

            // Keep look direction tied to yaw only to avoid tunnel "shifting" with pitch/roll input.
            Vector3 lookTarget = target.position + yawOnly * Vector3.forward * lookAheadDistance;
            Vector3 lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            float t = Mathf.Clamp01(Time.deltaTime / Mathf.Max(0.0001f, lookSmoothTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }
    }
}
