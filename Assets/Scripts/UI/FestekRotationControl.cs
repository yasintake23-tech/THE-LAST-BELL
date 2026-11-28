using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TheLastBell.Interaction;

namespace TheLastBell.UI
{
    public sealed class FestekRotationControl : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private FestekSocketInteractable socket;
        private RectTransform dial;
        private RectTransform marker;
        private Text progress;
        private bool hasAngle;
        private int finger = int.MinValue;
        private float lastAngle;

        public static GameObject Create(Transform canvas, FestekSocketInteractable socket)
        {
            var panel = new GameObject("Festek_Rotation_SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            var safe = (RectTransform)panel.transform;
            safe.SetParent(canvas, false);
            safe.anchorMin = Vector2.zero;
            safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            var background = new GameObject("Feştek Kadranı", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            var target = background.rectTransform;
            target.SetParent(safe, false);
            target.anchorMin = target.anchorMax = new Vector2(0.78f, 0.34f);
            target.sizeDelta = new Vector2(274, 260);
            background.color = new Color(0.07f, 0.08f, 0.085f, 0.9f);
            var label = CreateText(target, "Feştek’i çevirmek için sürükle.", new Vector2(.5f,.19f), Vector2.zero, new Vector2(252, 46), 20);
            label.raycastTarget = false;
            var ring = CreateText(target, "◯", new Vector2(.5f,.57f), Vector2.zero, new Vector2(170, 170), 140);
            ring.raycastTarget = false;
            var dot = CreateText(target, "●", new Vector2(.5f,.5f), Vector2.zero, new Vector2(35, 35), 23);
            dot.raycastTarget = false;
            dot.color = new Color(0.87f, 0.67f, 0.39f);

            var control = target.gameObject.AddComponent<FestekRotationControl>();
            control.socket = socket;
            control.dial = target;
            control.marker = dot.rectTransform;
            control.progress = CreateText(target, "", new Vector2(.5f, .57f), Vector2.zero, new Vector2(100, 42), 23);
            control.progress.raycastTarget = false;
            control.UpdateMarker();

            var cancel = CreateText(safe, "İptal", new Vector2(.78f,.34f),
                new Vector2(0,-163), new Vector2(112,48), 21);
            cancel.gameObject.AddComponent<Button>().onClick.AddListener(socket.CancelAndWithdraw);
            return panel;
        }

        private static Text CreateText(Transform parent, string value, Vector2 anchor, Vector2 offset, Vector2 size, int fontSize)
        {
            var text = new GameObject("Feştek", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.text = value;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (finger != int.MinValue || eventData.button != PointerEventData.InputButton.Left ||
                Time.timeScale <= 0f || !TryAngle(eventData, out lastAngle)) return;
            finger = eventData.pointerId;
            hasAngle = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != finger) return;
            if (Time.timeScale <= 0f || !TryAngle(eventData, out float angle))
            {
                hasAngle = false;
                return;
            }
            if (hasAngle)
                socket.ApplyRotationDelta(Mathf.DeltaAngle(angle, lastAngle));
            lastAngle = angle;
            hasAngle = true;
            UpdateMarker();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (finger == eventData.pointerId) ClearGesture();
        }

        private void OnDisable() => ClearGesture();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ClearGesture();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) ClearGesture();
        }

        private void ClearGesture()
        {
            finger = int.MinValue;
            hasAngle = false;
        }

        private bool TryAngle(PointerEventData data, out float angle)
        {
            angle = 0f;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(dial, data.position, data.pressEventCamera, out Vector2 point))
                return false;
            point.y -= 18f;
            if (point.sqrMagnitude < 18f * 18f) return false;
            angle = Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg;
            return true;
        }

        private void UpdateMarker()
        {
            float radians = (90f - socket.RotationDegrees) * Mathf.Deg2Rad;
            progress.text = $"{Mathf.RoundToInt(socket.RotationDegrees)}° / 90°";
            marker.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 67f + Vector2.up * 18f;
        }
    }
}
