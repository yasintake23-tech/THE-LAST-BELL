using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace TheLastBell.UI
{
    /// <summary>
    /// Menü butonları için pürüzsüz animasyon ve renk geçişleri.
    /// Mobil dokunma ve PC fare üzerine gelme (hover) durumlarını destekler.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Animasyon Ayarları")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float pressedScale = 0.96f;
        [SerializeField] private float transitionSpeed = 12f;

        [Header("Görsel Vurgular")]
        [SerializeField] private Image accentBar;
        [SerializeField] private TextMeshProUGUI buttonText;
        [SerializeField] private Color normalTextColor = new Color(1f, 1f, 1f, 1f); // Net parlak beyaz
        [SerializeField] private Color hoverTextColor = new Color(1f, 0.25f, 0.25f, 1f); // Parlak korku kırmızısı
        [SerializeField] private Color normalAccentColor = new Color(0.5f, 0.1f, 0.1f, 0.25f);
        [SerializeField] private Color hoverAccentColor = new Color(0.95f, 0.15f, 0.15f, 1f);

        private Vector3 originalScale;
        private Vector3 targetScale;
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            originalScale = transform.localScale;
            targetScale = originalScale;

            if (buttonText == null)
            {
                buttonText = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (accentBar != null)
            {
                accentBar.color = normalAccentColor;
            }

            if (buttonText != null)
            {
                buttonText.color = normalTextColor;
            }
        }

        private void OnEnable()
        {
            transform.localScale = originalScale;
            targetScale = originalScale;
            if (accentBar != null) accentBar.color = normalAccentColor;
            if (buttonText != null) buttonText.color = normalTextColor;
        }

        private void Update()
        {
            if (transform.localScale != targetScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.unscaledDeltaTime * transitionSpeed);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            targetScale = originalScale * hoverScale;
            if (accentBar != null) accentBar.color = hoverAccentColor;
            if (buttonText != null) buttonText.color = hoverTextColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            targetScale = originalScale;
            if (accentBar != null) accentBar.color = normalAccentColor;
            if (buttonText != null) buttonText.color = normalTextColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            targetScale = originalScale * pressedScale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            targetScale = originalScale;
        }

        public void SetAccentBar(Image bar) => accentBar = bar;
        public void SetButtonText(TextMeshProUGUI txt) => buttonText = txt;
    }
}
