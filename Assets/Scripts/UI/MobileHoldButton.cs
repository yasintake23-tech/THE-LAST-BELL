using UnityEngine;
using UnityEngine.EventSystems;

namespace TheLastBell.UI
{
    /// <summary>
    /// Basılı tutma (Hold) mantığıyla çalışan dokunmatik buton kontrolcüsü.
    /// Koşma (Sprint) gibi aksiyonlarda parmak basıldığında başlar, kaldırıldığında durur.
    /// </summary>
    public class MobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action onPointerDown;
        public System.Action onPointerUp;

        public void OnPointerDown(PointerEventData eventData)
        {
            onPointerDown?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            onPointerUp?.Invoke();
        }
    }
}
