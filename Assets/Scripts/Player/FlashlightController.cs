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
        [SerializeField] private Texture spotCookie;
        [SerializeField] private float baseIntensity = 4.2f;
        [SerializeField] private bool isOn = true;

        [Header("--- Montaj & Kamera Takibi (Sway) ---")]
        [SerializeField] private Vector3 mountOffset = new Vector3(0.18f, -0.16f, 0.16f);
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float swaySmoothSpeed = 14f;

        [Header("--- Pil Sistemi ---")]
        [SerializeField] private bool useBattery = true;
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float currentBattery = 100f;
        [SerializeField] private float drainRatePerSecond = 0.25f; // Yaklaşık 7 dakika pil ömrü
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
            if (cameraTarget == null && transform.parent != null)
            {
                var cam = transform.parent.GetComponentInChildren<Camera>();
                cameraTarget = cam != null ? cam.transform : transform.parent;
            }
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            playerInventory = GetComponentInParent<PlayerInventory>();

            if (flashlightLight != null)
            {
                ApplyRealisticLightSettings();
                flashlightLight.enabled = isOn;
            }
        }

        public void ApplyRealisticLightSettings()
        {
            if (flashlightLight == null) return;

            flashlightLight.type = LightType.Spot;
            flashlightLight.renderMode = LightRenderMode.ForcePixel;
            flashlightLight.range = 26f;
            flashlightLight.spotAngle = 45f;
            flashlightLight.innerSpotAngle = 15f;
            flashlightLight.intensity = baseIntensity;
            flashlightLight.shadows = LightShadows.Soft;
            flashlightLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.High;
            flashlightLight.shadowBias = 0.015f;
            flashlightLight.shadowNormalBias = 0.15f;
            flashlightLight.shadowNearPlane = 0.12f;
            flashlightLight.color = new Color(1.0f, 0.96f, 0.88f, 1.0f);
            if (spotCookie != null)
            {
                flashlightLight.cookie = spotCookie;
            }

            // Işığın duvar içinden sızmasını önlemek için kapsül yarıçapı içinde tut
            flashlightLight.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            flashlightLight.transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            HandleSway();

            // PC Girdi: 'F' feneri açar/kapatır
            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleFlashlight();
            }

            // PC Girdi: 'R' pil değiştir
            if (Input.GetKeyDown(KeyCode.R))
            {
                ReloadBattery();
            }

            HandleBattery();
        }

        private void HandleSway()
        {
            if (transform.parent == null) return;

            transform.localPosition = mountOffset;

            Quaternion targetWorldRot = transform.parent.rotation;
            if (cameraTarget != null)
            {
                Vector3 aimPoint = cameraTarget.position + cameraTarget.forward * 8.0f;
                Vector3 toAim = aimPoint - transform.position;
                if (toAim.sqrMagnitude > 0.01f)
                {
                    targetWorldRot = Quaternion.LookRotation(toAim.normalized, cameraTarget.up);
                }
            }

            if (swaySmoothSpeed > 0f && Time.deltaTime > 0f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetWorldRot, 1f - Mathf.Exp(-swaySmoothSpeed * Time.deltaTime));
            }
            else
            {
                transform.rotation = targetWorldRot;
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
