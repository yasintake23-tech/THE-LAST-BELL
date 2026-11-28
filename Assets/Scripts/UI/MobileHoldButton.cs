using UnityEngine;
using UnityEngine.EventSystems;

namespace TheLastBell.UI
{
    /// <summary>
    /// Basılı tutma (Hold) mantığıyla çalışan dokunmatik buton kontrolcüsü.
    /// Parmak kalktığında, buton alanından çıktığında veya UI kapandığında durumu güvenle sıfırlar.
    /// </summary>
    public class MobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public System.Action onPointerDown;
        public System.Action onPointerUp;

        private bool isHeld = false;
        private int activePointerId = int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isHeld) return;
            isHeld = true;
            activePointerId = eventData.pointerId;
            onPointerDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!isHeld || eventData.pointerId != activePointerId) return;
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isHeld || eventData.pointerId != activePointerId) return;
            Release();
        }

        private void OnDisable()
        {
            if (isHeld) Release();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isHeld) Release();
        }

        private void Release()
        {
            isHeld = false;
            activePointerId = int.MinValue;
            onPointerUp?.Invoke();
        }
    }
}
