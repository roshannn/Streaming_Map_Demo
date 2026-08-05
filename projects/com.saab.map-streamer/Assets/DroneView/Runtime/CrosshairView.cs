using UnityEngine;
using UnityEngine.UI;

namespace StreamingMapDemo.Drones
{
    public sealed class CrosshairView : MonoBehaviour
    {
        [SerializeField] private Color color = Color.white;
        [SerializeField] private Graphic[] graphics = System.Array.Empty<Graphic>();

        public Color Color => color;

        public void SetColor(Color value)
        {
            color = value;
            ApplyColor();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void Awake()
        {
            ApplyColor();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyColor();
        }
#endif

        private void ApplyColor()
        {
            if (graphics == null) return;
            foreach (Graphic graphic in graphics)
            {
                if (graphic != null) graphic.color = color;
            }
        }
    }
}
