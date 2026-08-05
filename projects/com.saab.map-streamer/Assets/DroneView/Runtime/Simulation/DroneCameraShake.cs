using UnityEngine;

namespace StreamingMapDemo.Drones
{
    [DefaultExecutionOrder(1000)]
    public sealed class DroneCameraShake : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float defaultDuration = .22f;
        [SerializeField, Min(0f)] private float defaultPositionStrength = .16f;
        [SerializeField, Min(0f)] private float defaultRotationStrength = 1.6f;
        private float remaining, duration, positionStrength, rotationStrength;
        private Vector3 appliedPosition;
        private Quaternion appliedRotation = Quaternion.identity;

        public void Play()
        {
            duration = Mathf.Max(.01f, defaultDuration);
            remaining = duration;
            positionStrength = defaultPositionStrength;
            rotationStrength = defaultRotationStrength;
        }

        private void Update()
        {
            transform.position -= appliedPosition;
            transform.rotation *= Quaternion.Inverse(appliedRotation);
            appliedPosition = Vector3.zero;
            appliedRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (remaining <= 0f) return;
            remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
            float envelope = remaining / duration;
            appliedPosition = Random.insideUnitSphere * (positionStrength * envelope);
            appliedRotation = Quaternion.Euler(Random.insideUnitSphere * (rotationStrength * envelope));
            transform.position += appliedPosition;
            transform.rotation *= appliedRotation;
        }

        private void OnDisable()
        {
            transform.position -= appliedPosition;
            transform.rotation *= Quaternion.Inverse(appliedRotation);
            appliedPosition = Vector3.zero;
            appliedRotation = Quaternion.identity;
        }
    }
}
