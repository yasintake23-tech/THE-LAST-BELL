using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastBell.Gameplay
{
    [Serializable]
    public class FacilityLightFixture
    {
        public Renderer fixtureRenderer;
        public int emissiveMaterialIndex = 1;
        public Light pointOrSpotLight;
        public float targetIntensity = 0.95f;
        public Color emissionColor = new Color(0.78f, 0.86f, 0.88f);
        public bool brokenStaysDim = false;
        public AudioSource ballastAudioSource;
    }

    /// <summary>
    /// THE LAST BELL - Merkezi Bina Elektrik ve Güç Yöneticisi (FacilityPowerManager).
    /// Feştek yuvasına yerleştirilip etkinleştirildiğinde:
    /// 1) Ana kontaktör röle sesi (SFX_Power_RelayThunk) duyulur.
    /// 2) Floresan ve koridor armatürleri tek seferde patlamaz; kademeli starter titremesiyle
    ///    korku atmosferini bozmayacak kontrollü şiddette devreye girer (bazı arızalı tüpler loş kalır).
    /// 3) Odanın sağ tarafındaki terk edilmiş CRT televizyon otomatik olarak çalışmaya başlar.
    /// 4) Bölüm I dönüm noktası (ChapterMilestoneComplete) tamamlanır; oyuncu kontrolde kalır.
    /// </summary>
    [DisallowMultipleComponent]
    public class FacilityPowerManager : MonoBehaviour
    {
        public static FacilityPowerManager Instance { get; private set; }

        [Header("--- Durum & Hedef Sistemler ---")]
        [SerializeField] private bool isPowerRestored = false;
        [SerializeField] private List<FacilityLightFixture> fixtures = new List<FacilityLightFixture>();
        [SerializeField] private AbandonedTelevisionController televisionController;

        [Header("--- Röle & Floresan Sesleri ---")]
        [SerializeField] private AudioSource relayAudioSource;
        [SerializeField] private AudioClip relayThunkClip;
        [SerializeField] private AudioClip fluorescentFlickerStartClip;
        [SerializeField] private AudioClip fluorescentHumLoopClip;

        private bool sequenceRunning = false;

        public bool IsPowerRestored => isPowerRestored;
        public AbandonedTelevisionController Television => televisionController;
        public IReadOnlyList<FacilityLightFixture> Fixtures => fixtures;

        public event Action OnPowerRestored;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            if (!isPowerRestored)
            {
                SetAllFixturesUnpowered();
            }
        }

        public void Configure(
            List<FacilityLightFixture> fixtureList,
            AbandonedTelevisionController tvCtrl,
            AudioSource relaySrc,
            AudioClip relayClip,
            AudioClip flickerClip,
            AudioClip humClip)
        {
            fixtures = fixtureList ?? new List<FacilityLightFixture>();
            televisionController = tvCtrl;
            relayAudioSource = relaySrc;
            relayThunkClip = relayClip;
            fluorescentFlickerStartClip = flickerClip;
            fluorescentHumLoopClip = humClip;
            SetAllFixturesUnpowered();
        }

        public void SetAllFixturesUnpowered()
        {
            isPowerRestored = false;
            for (int i = 0; i < fixtures.Count; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;
                if (f.pointOrSpotLight != null)
                {
                    f.pointOrSpotLight.enabled = false;
                    f.pointOrSpotLight.intensity = 0f;
                }
                SetFixtureEmission(f, Color.black);
                if (f.ballastAudioSource != null && f.ballastAudioSource.isPlaying)
                {
                    f.ballastAudioSource.Stop();
                }
            }
            televisionController?.ApplyUnpoweredVisuals();
        }

        public void BeginPowerRestorationSequence()
        {
            if (isPowerRestored || sequenceRunning) return;
            StartCoroutine(PowerRestorationRoutine());
        }

        private IEnumerator PowerRestorationRoutine()
        {
            sequenceRunning = true;

            // Aşama 1: Kontaktör röle darbesi
            if (relayAudioSource != null && relayThunkClip != null)
            {
                relayAudioSource.PlayOneShot(relayThunkClip, 0.95f);
            }

            yield return new WaitForSeconds(0.55f);

            // Aşama 2: Kademeli floresan starter titremesi
            for (int i = 0; i < fixtures.Count; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;
                StartCoroutine(FlickerSingleFixtureRoutine(f, i * 0.28f));
            }

            yield return new WaitForSeconds(1.35f);

            // Aşama 3: Sağ taraftaki terk edilmiş CRT televizyonun devreye girmesi
            isPowerRestored = true;
            ChapterObjectiveManager.Instance?.NotifyTelevisionActivated();
            televisionController?.ActivateTelevision(immediate: false);

            OnPowerRestored?.Invoke();

            yield return new WaitForSeconds(1.25f);

            // Aşama 4: Bölüm I dönüm noktası tamamlandı (Oyuncu kontrolde kalır!)
            ChapterObjectiveManager.Instance?.NotifyMilestoneComplete();
            sequenceRunning = false;
        }

        private IEnumerator FlickerSingleFixtureRoutine(FacilityLightFixture f, float startDelay)
        {
            if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

            if (f.ballastAudioSource != null && fluorescentFlickerStartClip != null)
            {
                f.ballastAudioSource.PlayOneShot(fluorescentFlickerStartClip, 0.65f);
            }

            float[] pulses = { 0.35f, 0.0f, 0.72f, 0.08f, 0.90f, 0.15f };
            for (int p = 0; p < pulses.Length; p++)
            {
                float mult = pulses[p];
                if (f.pointOrSpotLight != null)
                {
                    f.pointOrSpotLight.enabled = mult > 0.01f;
                    f.pointOrSpotLight.intensity = f.targetIntensity * mult;
                }
                SetFixtureEmission(f, f.emissionColor * mult);
                yield return new WaitForSeconds(0.11f);
            }

            float finalMult = f.brokenStaysDim ? 0.18f : 1.0f;
            if (f.pointOrSpotLight != null)
            {
                f.pointOrSpotLight.enabled = true;
                f.pointOrSpotLight.intensity = f.targetIntensity * finalMult;
            }
            SetFixtureEmission(f, f.emissionColor * finalMult);

            if (!f.brokenStaysDim && f.ballastAudioSource != null && fluorescentHumLoopClip != null)
            {
                f.ballastAudioSource.clip = fluorescentHumLoopClip;
                f.ballastAudioSource.loop = true;
                f.ballastAudioSource.volume = 0.20f;
                f.ballastAudioSource.Play();
            }
        }

        public void ExecuteImmediateForTest()
        {
            isPowerRestored = true;
            sequenceRunning = false;

            for (int i = 0; i < fixtures.Count; i++)
            {
                var f = fixtures[i];
                if (f == null) continue;
                float mult = f.brokenStaysDim ? 0.18f : 1.0f;
                if (f.pointOrSpotLight != null)
                {
                    f.pointOrSpotLight.enabled = true;
                    f.pointOrSpotLight.intensity = f.targetIntensity * mult;
                }
                SetFixtureEmission(f, f.emissionColor * mult);
            }

            ChapterObjectiveManager.Instance?.NotifyTelevisionActivated();
            televisionController?.ActivateTelevision(immediate: true);
            OnPowerRestored?.Invoke();
            ChapterObjectiveManager.Instance?.NotifyMilestoneComplete();
        }

        private static void SetFixtureEmission(FacilityLightFixture f, Color emission)
        {
            if (f == null || f.fixtureRenderer == null) return;
            var mpb = new MaterialPropertyBlock();
            int subIdx = Mathf.Clamp(f.emissiveMaterialIndex, 0, Mathf.Max(0, f.fixtureRenderer.sharedMaterials.Length - 1));
            f.fixtureRenderer.GetPropertyBlock(mpb, subIdx);
            mpb.SetColor("_EmissionColor", emission);
            if (emission.maxColorComponent > 0.01f)
            {
                mpb.SetColor("_Color", new Color(0.88f, 0.93f, 0.95f, 1f));
            }
            f.fixtureRenderer.SetPropertyBlock(mpb, subIdx);
        }
    }
}
