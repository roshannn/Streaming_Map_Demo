using UnityEngine;
using UnityEngine.UI;

namespace StreamingMapDemo.Drones
{
    public sealed class DroneTargetingView : MonoBehaviour
    {
        [SerializeField] private Image acquisitionRing;
        [SerializeField] private RectTransform targetBrackets;
        [SerializeField] private RectTransform leadMarker;
        [SerializeField] private Canvas canvas;

        public void PresentAcquisition(Vector2 screenPosition, float progress)
        {
            SetScreenPosition(targetBrackets, screenPosition);
            if (targetBrackets != null) targetBrackets.gameObject.SetActive(true);
            SetBracketColor(new Color(.2f, .9f, 1f, 1f));
            if (acquisitionRing != null)
            {
                acquisitionRing.gameObject.SetActive(true);
                acquisitionRing.color = Color.white;
                float normalized = Mathf.Clamp01(progress);
                acquisitionRing.fillAmount = normalized;
                acquisitionRing.rectTransform.anchorMax = new Vector2(normalized, 1f);
            }
            if (leadMarker != null) leadMarker.gameObject.SetActive(false);
        }

        public void PresentLock(Vector2 targetPosition, Vector2 leadPosition, bool showLead, bool aligned = true)
        {
            SetScreenPosition(targetBrackets, targetPosition);
            SetScreenPosition(leadMarker, leadPosition);
            if (targetBrackets != null) targetBrackets.gameObject.SetActive(true);
            if (targetBrackets != null)
            {
                Color color = aligned ? new Color(1f, .12f, .08f, 1f) : new Color(1f, .68f, .12f, 1f);
                SetBracketColor(color);
            }
            if (acquisitionRing != null) acquisitionRing.gameObject.SetActive(false);
            if (leadMarker != null) leadMarker.gameObject.SetActive(showLead);
        }

        public void Clear()
        {
            SetBracketColor(new Color(.2f, .9f, 1f, 1f));
            if (acquisitionRing != null) acquisitionRing.color = Color.white;
            if (targetBrackets != null) targetBrackets.gameObject.SetActive(false);
            if (acquisitionRing != null) acquisitionRing.gameObject.SetActive(false);
            if (leadMarker != null) leadMarker.gameObject.SetActive(false);
        }

        private void SetBracketColor(Color color)
        {
            if (targetBrackets == null) return;
            Image[] graphics = targetBrackets.GetComponentsInChildren<Image>(true);
            foreach (Image graphic in graphics)
                if (graphic != acquisitionRing) graphic.color = color;
        }

        private void SetScreenPosition(RectTransform item, Vector2 screenPosition)
        {
            if (item == null) return;
            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : transform as RectTransform;
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, eventCamera, out Vector2 local)) item.anchoredPosition = local;
        }

        private void Awake() { if (canvas == null) canvas = GetComponentInParent<Canvas>(); Clear(); }
    }
}
