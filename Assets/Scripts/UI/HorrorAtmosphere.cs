using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL ana menüsü için sinematik korku atmosferi.
    /// Altyazı soluklaşma (pulse), arka plan loş ışık titremesi (flicker) sağlar.
    /// </summary>
    public class HorrorAtmosphere : MonoBehaviour
    {
        [Header("Altyazı Nefes Efekti")]
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private float subtitleMinAlpha = 0.35f;
        [SerializeField] private float subtitleMaxAlpha = 0.95f;
        [SerializeField] private float subtitlePulseSpeed = 1.2f;

        [Header("Arka Plan Korku Titremesi")]
        [SerializeField] private Image vignetteOverlay;
        [SerializeField] private Light backgroundLight;
        [SerializeField] private float lightFlickerChance = 0.08f;
        [SerializeField] private float baseLightIntensity = 0.4f;

        private Color initialSubColor;
        private float flickerTimer;

        private void Start()
        {
            if (subtitleText != null)
            {
                initialSubColor = subtitleText.color;
            }
        }

        private void Update()
        {
            // Altyazı soluklaşma (nefes alma efekti)
            if (subtitleText != null)
            {
                float alpha = Mathf.Lerp(subtitleMinAlpha, subtitleMaxAlpha, (Mathf.Sin(Time.time * subtitlePulseSpeed) + 1f) * 0.5f);
                Color c = initialSubColor;
                c.a = alpha;
                subtitleText.color = c;
            }

            // Rastgele hafif ışık titremesi
            if (backgroundLight != null)
            {
                flickerTimer -= Time.deltaTime;
                if (flickerTimer <= 0f)
                {
                    if (Random.value < lightFlickerChance)
                    {
                        backgroundLight.intensity = baseLightIntensity * Random.Range(0.2f, 0.7f);
                        flickerTimer = Random.Range(0.05f, 0.15f);
                    }
                    else
                    {
                        backgroundLight.intensity = Mathf.MoveTowards(backgroundLight.intensity, baseLightIntensity, Time.deltaTime * 2f);
                        flickerTimer = Random.Range(0.1f, 0.4f);
                    }
                }
            }
        }
    }
}
