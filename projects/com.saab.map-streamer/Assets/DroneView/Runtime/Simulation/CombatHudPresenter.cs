using StreamingMapDemo.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace StreamingMapDemo.Drones
{
    public sealed class CombatHudPresenter : MonoBehaviour
    {
        [SerializeField] private Text playerHealth;
        [SerializeField] private Text killCounter;
        [SerializeField] private Text lockStatus;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        private float destroyedStatusRemaining;

        private void Awake()
        {
            if (lockStatus != null && lockStatus.GetComponent<Outline>() == null)
            {
                Outline outline = lockStatus.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, .9f);
                outline.effectDistance = new Vector2(2f, -2f);
            }
        }

        private void Update()
        {
            if (destroyedStatusRemaining <= 0f) return;
            destroyedStatusRemaining -= Time.unscaledDeltaTime;
            if (destroyedStatusRemaining <= 0f) ApplyTargetLocked(false);
        }
        public void Present(MatchState state)
        {
            if (playerHealth != null) playerHealth.text = $"Health: {Mathf.CeilToInt(state.PlayerHealth)}/100";
            if (killCounter != null) killCounter.text = $"Enemies destroyed: {state.EnemyKills}/{state.RequiredKills}";
            if (victoryPanel != null) victoryPanel.SetActive(state.Outcome == MatchOutcome.Victory);
            if (defeatPanel != null) defeatPanel.SetActive(state.Outcome == MatchOutcome.Defeat);
        }

        public void SetTargetLocked(bool locked)
        {
            if (destroyedStatusRemaining > 0f) return;
            ApplyTargetLocked(locked);
        }

        public void ShowTargetDestroyed(float duration = 2.5f)
        {
            if (lockStatus == null) return;
            destroyedStatusRemaining = Mathf.Max(0f, duration);
            lockStatus.text = "TARGET DESTROYED";
            lockStatus.color = new Color(1f, .72f, .12f, 1f);
        }

        public void ResetTargetStatus()
        {
            destroyedStatusRemaining = 0f;
            ApplyTargetLocked(false);
        }

        private void ApplyTargetLocked(bool locked)
        {
            if (lockStatus == null) return;
            lockStatus.text = locked ? "TARGET LOCKED" : "NO TARGET LOCK";
            lockStatus.color = locked ? new Color(1f, .12f, .08f, 1f) : Color.white;
        }
    }
}
