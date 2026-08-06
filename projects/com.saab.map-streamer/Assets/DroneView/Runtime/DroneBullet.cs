using UnityEngine;
using System;

namespace StreamingMapDemo.Drones
{
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class DroneBullet : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float speed = 25f;
        [SerializeField, Min(0f)] private float lifetime = 5f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField] private TrailRenderer tracer;

        private Rigidbody body;
        private Vector3 direction = Vector3.forward;
        private float remainingLifetime;
        private Action<DroneBullet> releaseToPool;
        private bool isLaunched;

        public float Damage => damage;
        public Vector3 Direction => direction;

        public void Launch(Vector3 worldDirection, Action<DroneBullet> release, Collider[] ignoredColliders = null)
        {
            if (body == null)
                body = GetComponent<Rigidbody>();

            tracer?.Clear();
            releaseToPool = release;
            isLaunched = true;
            if (worldDirection.sqrMagnitude > 0.0001f)
            {
                direction = worldDirection.normalized;
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }

            remainingLifetime = lifetime;
            body.velocity = direction * speed;
            SphereCollider ownCollider = GetComponent<SphereCollider>();
            if (ignoredColliders != null)
            {
                foreach (Collider ignoredCollider in ignoredColliders)
                {
                    if (ignoredCollider != null) Physics.IgnoreCollision(ownCollider, ignoredCollider, true);
                }
            }
        }

        public void ReturnToPool()
        {
            if (!isLaunched)
            {
                return;
            }

            isLaunched = false;
            tracer?.Clear();
            if (body != null) body.velocity = Vector3.zero;
            Action<DroneBullet> release = releaseToPool;
            releaseToPool = null;
            release?.Invoke(this);
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            remainingLifetime = lifetime;
        }

        private void FixedUpdate()
        {
            remainingLifetime -= Time.fixedDeltaTime;
            if (remainingLifetime <= 0f)
            {
                ReturnToPool();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.transform.IsChildOf(transform))
            {
                ReturnToPool();
            }
        }
    }
}
