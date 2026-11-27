using UnityEngine;

namespace TheLastBell.UI
{
    /// <summary>
    /// Adapts a UI RectTransform to Screen.safeArea so mobile controls never clip
    /// behind punch-hole cameras, notches, or rounded screen corners.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Rect lastSafeArea = Rect.zero;
        private Vector2Int lastScreenSize = Vector2Int.zero;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea ||
                lastScreenSize.x != Screen.width ||
                lastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safeArea = Screen.safeArea;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
