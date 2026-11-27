using System;
using UnityEngine;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Hibrit Kontrol Sistemi Yöneticisi (Mobile Control Manager).
    /// Çalışma zamanında cihaz tipini tespit eder:
    /// - Mobil cihazlarda (Android / iOS): Gameplay_HUD_Canvas içindeki sanal joystick,
    ///   TouchLookZone ve aksiyon butonlarını (Fener, Çömelme, Koşma, Etkileşim) aktif eder.
    /// - PC / Windows ortamında: Dokunmatik UI elemanlarını tamamen devre dışı bırakır,
    ///   fare imlecini kilitler (CursorLockMode.Locked) ve karakteri WASD + Mouse ile yönetime bırakır.
    /// </summary>
    public class MobileControlManager : MonoBehaviour
    {
        [Header("--- Dokunmatik Kontrol Kökü ---")]
        [Tooltip("Tüm mobil kontrolleri barındıran üst obje (Gameplay_HUD_Canvas/Mobile_Controls_Root)")]
        [SerializeField] private GameObject mobileControlsRoot;

        [Header("--- Münferit Dokunmatik Elemanlar (İsteğe Bağlı) ---")]
        [SerializeField] private GameObject virtualJoystick;
        [SerializeField] private GameObject touchLookZone;
        [SerializeField] private GameObject[] actionButtons;

        [Header("--- Test ve Editör Ayarları ---")]
        [Tooltip("PC Unity Editöründe mobil kontrolleri zorla göstermek/test etmek için işaretleyin")]
        [SerializeField] private bool forceMobileInEditor = true;

        [Tooltip("PC ortamında fare imlecini kilitlesin mi")]
        [SerializeField] private bool lockCursorOnPC = true;

        public bool IsMobileActive { get; private set; }

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Start()
        {
            ApplyPlatformControls();
        }

        /// <summary>
        /// Platforma göre UI görünürlüğünü ve imleç durumunu ayarlar.
        /// </summary>
        public void ApplyPlatformControls()
        {
            bool isMobile = Application.isMobilePlatform || (Application.isEditor && forceMobileInEditor);
            IsMobileActive = isMobile;

            if (isMobile)
            {
                // Mobil Cihaz (Android / iOS)
                SetMobileUIActive(true);

                // Mobilde dokunmatik girdi için imleç kilidini kaldır
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                Debug.Log("[MobileControlManager] Mobil platform tespit edildi (Android/iOS). Dokunmatik kontroller aktif edildi.");
            }
            else
            {
                // PC / Windows
                SetMobileUIActive(false);

                // PC'de birinci şahıs bakış için fare imlecini kilitle
                if (lockCursorOnPC)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                Debug.Log("[MobileControlManager] PC/Windows platformu tespit edildi. Dokunmatik UI gizlendi, WASD + Fare kontrolleri aktif.");
            }
        }

        /// <summary>
        /// Dokunmatik kontrolleri topluca açar veya kapatır.
        /// </summary>
        public void SetMobileUIActive(bool active)
        {
            if (mobileControlsRoot != null)
            {
                mobileControlsRoot.SetActive(active);
            }

            if (virtualJoystick != null)
            {
                virtualJoystick.SetActive(active);
            }

            if (touchLookZone != null)
            {
                touchLookZone.SetActive(active);
            }

            if (actionButtons != null)
            {
                foreach (var btn in actionButtons)
                {
                    if (btn != null) btn.SetActive(active);
                }
            }
        }

        /// <summary>
        /// Editörde veya çalışma zamanında referanslar boşsa otomatik bulur.
        /// </summary>
        public void AutoResolveReferences()
        {
            if (mobileControlsRoot == null)
            {
                // Önce GameObject.Find ile Mobile_Controls_Root ara
                GameObject foundRoot = GameObject.Find("Mobile_Controls_Root");
                if (foundRoot != null)
                {
                    mobileControlsRoot = foundRoot;
                }
                else
                {
                    // Canvas altından ara
                    Canvas canvas = GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        Transform t = canvas.transform.Find("Mobile_Controls_Root");
                        if (t != null) mobileControlsRoot = t.gameObject;
                    }
                }
            }

            if (mobileControlsRoot != null)
            {
                if (virtualJoystick == null)
                {
                    Transform j = mobileControlsRoot.transform.Find("Joystick_Background");
                    if (j != null) virtualJoystick = j.gameObject;
                }

                if (touchLookZone == null)
                {
                    Transform l = mobileControlsRoot.transform.Find("Touch_Look_Zone");
                    if (l != null) touchLookZone = l.gameObject;
                }

                if (actionButtons == null || actionButtons.Length == 0)
                {
                    string[] btnNames = { "Mobile_ActionButton", "Mobile_SprintButton", "Mobile_CrouchButton", "Mobile_FlashlightButton" };
                    var btnList = new System.Collections.Generic.List<GameObject>();
                    foreach (var bName in btnNames)
                    {
                        Transform b = mobileControlsRoot.transform.Find(bName);
                        if (b != null) btnList.Add(b.gameObject);
                    }
                    actionButtons = btnList.ToArray();
                }
            }
        }

        private void OnValidate()
        {
            if (mobileControlsRoot == null)
            {
                AutoResolveReferences();
            }
        }
    }
}
