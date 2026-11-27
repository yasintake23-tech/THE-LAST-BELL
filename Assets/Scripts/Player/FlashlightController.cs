using System;
using UnityEngine;

namespace TheLastBell.Player
{
    /// <summary>
    /// THE LAST BELL - Gereki El Feneri Sistemi (Flashlight Controller).
    /// Kameray doal bir gecikmeyle (sway/lag) takip eder, a/kapa klik sesleri,
    /// pil tketimi, dk pil titremesi ve yeniden pil doldurma mekaniklerini ynetir.
    /// </summary>
    public class FlashlightController : MonoBehaviour
    {
        [Header("--- Işık Kaynağı (Spot Light) ---")]
        [SerializeField] private Light flashlightLight;
        [SerializeField] private float baseIntensity = 3.5f;
        [SerializeField] private bool isOn = true;

        [Header("--- Montaj & Kamera Takibi (Sway) ---")]
        [SerializeField] private Vector3 mountOffset = new Vector3(0.25f, -0.25f, 0.35f);
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float swaySmoothSpeed = 8f;

        [Header("--- Pil Sistemi ---")]
        [SerializeField] private bool useBattery = true;
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float currentBattery = 100f;
        [SerializeField] private float drainRatePerSecond = 0.25f; // Yaklak 7 dakika pil mr
        [SerializeField] private float lowBatteryThreshold = 20f;

        [Header("--- Ses Efektleri ---")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip switchOnClick;
        [SerializeField] private AudioClip switchOffClick;

        private PlayerInventory playerInventory;
        private float flickerTimer = 0f;

        public event Action<float> OnBatteryChanged;
        public event Action<bool> OnFlashlightToggled;

        public bool IsOn => isOn;
        public float BatteryPercent => currentBattery / maxBattery;

        private void Awake()
        {
            if (flashlightLight == null)
            {
                var spot = transform.Find("SpotLight")?.GetComponent<Light>();
                flashlightLight = spot != null ? spot : GetComponentInChildren<Light>();
            }
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            playerInventory = GetComponentInParent<PlayerInventory>();

            if (flashlightLight != null)
            {
                flashlightLight.type = LightType.Spot;
                flashlightLight.range = 25f;
                flashlightLight.spotAngle = 55f;
                flashlightLight.innerSpotAngle = 30f;
                flashlightLight.intensity = baseIntensity;
                flashlightLight.shadows = LightShadows.Soft;
                flashlightLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
                flashlightLight.color = new Color(1.0f, 0.9725f, 0.9059f, 1.0f); // #FFF8E7
                flashlightLight.enabled = isOn;
                flashlightLight.transform.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            HandleSway();

            // PC Girdi: 'F' feneri aar/kapatr
            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleFlashlight();
            }

            // PC Girdi: 'R' pil deitir
            if (Input.GetKeyDown(KeyCode.R))
            {
                ReloadBattery();
            }

            HandleBattery();
        }

        private void HandleSway()
        {
            // Fener pozisyonu karakterin sağ omzunun önünde (0.3f, 1.5f, 0.5f) sabit kalır
            if (transform.parent != null)
            {
                transform.localPosition = mountOffset;
                // Işık konisinin TAM KARŞIYA (dünyaya/koridora) vurmasını sağla; oyuncuyu aydınlatmasını kesinlikle engelle
                transform.localRotation = Quaternion.identity;
            }
        }

        private void HandleBattery()
        {
            if (!useBattery || !isOn) return;

            currentBattery = Mathf.Max(0f, currentBattery - drainRatePerSecond * Time.deltaTime);
            OnBatteryChanged?.Invoke(BatteryPercent);

            if (currentBattery <= 0f)
            {
                if (flashlightLight != null) flashlightLight.enabled = false;
                isOn = false;
                OnFlashlightToggled?.Invoke(false);
                return;
            }

            // Dk pil titremesi
            if (currentBattery < lowBatteryThreshold && flashlightLight != null)
            {
                flickerTimer -= Time.deltaTime;
                if (flickerTimer <= 0f)
                {
                    flickerTimer = UnityEngine.Random.Range(0.08f, 0.35f);
                    bool flickerOff = UnityEngine.Random.value < 0.25f;
                    flashlightLight.intensity = flickerOff ? baseIntensity * 0.2f : baseIntensity * UnityEngine.Random.Range(0.7f, 1.0f);
                }
            }
            else if (flashlightLight != null)
            {
                flashlightLight.intensity = baseIntensity;
            }
        }

        public void ToggleFlashlight()
        {
            if (currentBattery <= 0f)
            {
                currentBattery = maxBattery;
            }

            isOn = !isOn;
            if (flashlightLight == null)
            {
                var spot = transform.Find("SpotLight")?.GetComponent<Light>();
                flashlightLight = spot != null ? spot : GetComponentInChildren<Light>();
            }

            if (flashlightLight != null)
            {
                flashlightLight.enabled = isOn;
            }

            PlayClick(isOn);
            OnFlashlightToggled?.Invoke(isOn);
        }

        public void ReloadBattery()
        {
            if (playerInventory != null && playerInventory.ConsumeBattery())
            {
                currentBattery = maxBattery;
                if (!isOn) ToggleFlashlight();
                PlayClick(true);
                OnBatteryChanged?.Invoke(1.0f);
                Debug.Log("[THE LAST BELL] Fener pili deitirildi.");
            }
        }

        private void PlayClick(bool turningOn)
        {
            if (audioSource != null)
            {
                AudioClip clip = turningOn ? switchOnClick : switchOffClick;
                if (clip != null)
                {
                    audioSource.pitch = UnityEngine.Random.Range(0.97f, 1.03f);
                    audioSource.PlayOneShot(clip, 0.85f);
                }
            }
        }

        public void Configure(Light spot, Transform cam, AudioSource aSource, AudioClip onClick, AudioClip offClick)
        {
            flashlightLight = spot;
            cameraTarget = cam;
            audioSource = aSource;
            switchOnClick = onClick;
            switchOffClick = offClick;
        }
    }
}
