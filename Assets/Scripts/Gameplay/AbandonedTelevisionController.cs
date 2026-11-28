using System.Collections;
using UnityEngine;

namespace TheLastBell.Gameplay
{
    /// <summary>
    /// THE LAST BELL - Terk Edilmiş Tüplü (CRT) Televizyon Kontrolcüsü.
    /// Bulmaca alanının sağ tarafında başlangıçta tamamen sönük/ölü durur.
    /// Elektrik geri geldiğinde tüp ısınma sesi (SFX_TV_CRTStartup), dengesiz analog parazit
    /// tarama çizgileri, hafif lokal ekran ışıması ve uzamsal 3D parazit sesiyle devreye girer.
    /// </summary>
    [DisallowMultipleComponent]
    public class AbandonedTelevisionController : MonoBehaviour
    {
        [Header("--- Ekran & Işık Referansları ---")]
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Light screenLocalLight;
        [SerializeField] private Material screenUnpoweredMaterial;
        [SerializeField] private Material screenPoweredStaticMaterial;

        [Header("--- Uzamsal 3D CRT Sesleri ---")]
        [SerializeField] private AudioSource tvAudioSource;
        [SerializeField] private AudioClip crtStartupClip;
        [SerializeField] private AudioClip staticLoopClip;

        [Header("--- Atmosferik Parazit Ayarları ---")]
        [SerializeField] private float baseLightIntensity = 1.15f;
        [SerializeField] private bool isPoweredOn = false;

        private Material runtimeScreenMat;
        private float warmUpProgress = 0f;
        private float staticScrollTimer = 0f;

        public bool IsPoweredOn => isPoweredOn;
        public Light ScreenLocalLight => screenLocalLight;
        public Renderer ScreenRenderer => screenRenderer;

        private void Awake()
        {
            ApplyUnpoweredVisuals();
        }

        public void Configure(
            Renderer screenRend, Light glowLight,
            Material offMat, Material onStaticMat,
            AudioSource audioSrc, AudioClip startupClip, AudioClip loopClip)
        {
            screenRenderer = screenRend;
            screenLocalLight = glowLight;
            screenUnpoweredMaterial = offMat;
            screenPoweredStaticMaterial = onStaticMat;
            tvAudioSource = audioSrc;
            crtStartupClip = startupClip;
            staticLoopClip = loopClip;
            ApplyUnpoweredVisuals();
        }

        public void ApplyUnpoweredVisuals()
        {
            isPoweredOn = false;
            warmUpProgress = 0f;

            if (screenRenderer != null && screenUnpoweredMaterial != null)
            {
                screenRenderer.sharedMaterial = screenUnpoweredMaterial;
            }
            if (screenLocalLight != null)
            {
                screenLocalLight.enabled = false;
                screenLocalLight.intensity = 0f;
            }
            if (tvAudioSource != null && tvAudioSource.isPlaying)
            {
                tvAudioSource.Stop();
            }
        }

        public void ActivateTelevision(bool immediate = false)
        {
            if (isPoweredOn) return;
            isPoweredOn = true;

            if (screenRenderer != null && screenPoweredStaticMaterial != null)
            {
                runtimeScreenMat = Application.isPlaying
                    ? new Material(screenPoweredStaticMaterial)
                    : screenPoweredStaticMaterial;
                runtimeScreenMat.EnableKeyword("_EMISSION");
                if (Application.isPlaying) screenRenderer.material = runtimeScreenMat;
                else screenRenderer.sharedMaterial = runtimeScreenMat;
            }

            if (screenLocalLight != null)
            {
                screenLocalLight.enabled = true;
                screenLocalLight.intensity = immediate ? baseLightIntensity : 0.25f;
            }

            if (tvAudioSource != null)
            {
                if (crtStartupClip != null && Application.isPlaying)
                {
                    tvAudioSource.PlayOneShot(crtStartupClip, 0.90f);
                }
                if (staticLoopClip != null)
                {
                    tvAudioSource.clip = staticLoopClip;
                    tvAudioSource.loop = true;
                    tvAudioSource.volume = immediate ? 0.42f : 0.12f;
                    if (Application.isPlaying) tvAudioSource.Play();
                }
            }

            if (immediate || !Application.isPlaying)
            {
                warmUpProgress = 1f;
                StepStaticEffect(0.05f);
            }
            else
            {
                StartCoroutine(WarmUpRoutine());
            }
        }

        private IEnumerator WarmUpRoutine()
        {
            float dur = 0.95f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                warmUpProgress = Mathf.Clamp01(elapsed / dur);
                if (tvAudioSource != null)
                {
                    tvAudioSource.volume = Mathf.Lerp(0.10f, 0.42f, warmUpProgress);
                }
                yield return null;
            }
            warmUpProgress = 1f;
        }

        private void Update()
        {
            if (!isPoweredOn) return;
            StepStaticEffect(Time.deltaTime);
        }

        public void StepStaticEffect(float dt)
        {
            if (!isPoweredOn) return;
            staticScrollTimer += dt;

            // Dengesiz analog parazit titremesi (korkutucu ama abartısız soğuk fosfor ışıması)
            float n1 = Mathf.PerlinNoise(staticScrollTimer * 19f, 0.37f);
            float n2 = Mathf.PerlinNoise(0.83f, staticScrollTimer * 7.5f);
            float syncDip = (Mathf.Sin(staticScrollTimer * 4.3f) > 0.92f) ? 0.62f : 1.0f;
            float flicker = Mathf.Lerp(0.76f, 1.18f, n1 * 0.65f + n2 * 0.35f) * syncDip * Mathf.Max(0.25f, warmUpProgress);

            if (screenLocalLight != null)
            {
                screenLocalLight.intensity = baseLightIntensity * flicker;
            }

            Material mat = runtimeScreenMat != null ? runtimeScreenMat : screenPoweredStaticMaterial;
            if (mat != null)
            {
                float offX = Mathf.Repeat(staticScrollTimer * 3.7f + n1 * 0.5f, 1f);
                float offY = Mathf.Repeat(staticScrollTimer * 11.3f + n2 * 0.8f, 1f);
                mat.mainTextureOffset = new Vector2(offX, offY);
                Color emis = new Color(0.58f, 0.68f, 0.78f) * (1.15f * flicker);
                mat.SetColor("_EmissionColor", emis);
            }
        }
    }
}
