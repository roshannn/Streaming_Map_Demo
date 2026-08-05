using StreamingMapDemo.Pooling;
using StreamingMapDemo.Simulation;
using UnityEngine;

namespace StreamingMapDemo.Drones
{
    public sealed class CombatVfxPresenter : MonoBehaviour
    {
        [SerializeField] private ParticleSystem impactPrefab;
        [SerializeField] private ParticleSystem destructionPrefab;
        private ComponentPool<ParticleSystem> impacts, destructions;
        public void Initialize() { if (impactPrefab != null) impacts = new ComponentPool<ParticleSystem>(impactPrefab, 8, transform); if (destructionPrefab != null) destructions = new ComponentPool<ParticleSystem>(destructionPrefab, 3, transform); }
        public void Present(SimulationEvent e, IWorldOrigin origin)
        {
            ComponentPool<ParticleSystem> pool = e.Type == SimulationEventType.EntityDestroyed ? destructions : e.Type == SimulationEventType.ProjectileImpacted ? impacts : null;
            if (pool == null) return;
            ParticleSystem effect = pool.Get(origin.ToLocal(e.Position), Quaternion.LookRotation(e.Normal.ToVector3().sqrMagnitude > 0 ? e.Normal.ToVector3() : Vector3.up));
            effect.Clear(true); effect.Play(true);
            StartCoroutine(ReleaseAfter(pool, effect));
        }
        private System.Collections.IEnumerator ReleaseAfter(ComponentPool<ParticleSystem> pool, ParticleSystem effect) { yield return new WaitForSeconds(2f); if (effect != null) { effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); pool.Release(effect); } }
        private void OnDestroy() { impacts?.Dispose(); destructions?.Dispose(); }
    }
}
