using UnityEngine;
using UnityEngine.EventSystems;

namespace TheLastBell.UI
{
    /// <summary>
    /// THE LAST BELL - Hibrit Kontrol Sistemi Yöneticisi (Mobile Control Manager).
    /// Çalışma zamanında cihaz tipini tespit eder, EventSystem varlığını garanti eder,
    /// intro/duraklatma/not okuma sırasında dokunmatik kontrolleri gizler ve platforma göre imleç kilidini yönetir.
    /// </summary>
    public class MobileControlManager : MonoBehaviour
    {
        public static MobileControlManager Instance { get; private set; }

        [Header("--- Dokunmatik Kontrol Kökü ---")]
        [SerializeField] private GameObject mobileControlsRoot;

        [Header("--- Münferit Dokunmatik Elemanlar (İsteğe Bağlı) ---")]
        [SerializeField] private GameObject virtualJoystick;
        [SerializeField] private GameObject touchLookZone;
        [SerializeField] private GameObject[] actionButtons;

        [Header("--- Test ve Editör Ayarları ---")]
        [SerializeField] private bool forceMobileInEditor = true;
        [SerializeField] private bool lockCursorOnPC = true;

        private bool isIntroActive = false;
        private bool isPaused = false;

        public bool IsMobileActive { get; private set; }

        private void Awake()
        {
            Instance = this;
            EnsureEventSystemExists();
            AutoResolveReferences();
        }

        private void Start()
        {
            ApplyPlatformControls();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetIntroActive(bool introActive)
        {
            isIntroActive = introActive;
            ApplyPlatformControls();
        }

        public void SetPaused(bool paused)
        {
            isPaused = paused;
            ApplyPlatformControls();
        }

        public static void EnsureEventSystemExists()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Debug.LogWarning("[MobileControlManager] Sahnede EventSystem bulunamadı; çalışma zamanında otomatik oluşturuldu.");
        }

        /// <summary>
        /// Platforma göre UI görünürlüğünü ve imleç durumunu ayarlar.
        /// </summary>
        public void ApplyPlatformControls()
        {
            bool isMobile = Application.isMobilePlatform || (Application.isEditor && forceMobileInEditor);
            IsMobileActive = isMobile;

            bool showMobileControls = isMobile && !isIntroActive && !isPaused;

            if (isMobile)
            {
                SetMobileUIActive(showMobileControls);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                SetMobileUIActive(false);
                if (lockCursorOnPC && !isIntroActive && !isPaused)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (isPaused)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
        }

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

        public void AutoResolveReferences()
        {
            if (mobileControlsRoot == null)
            {
                GameObject foundRoot = GameObject.Find("Mobile_Controls_Root");
                if (foundRoot != null)
                {
                    mobileControlsRoot = foundRoot;
                }
                else
                {
                    Canvas canvas = GetComponentInParent<Canvas>();
                    if (canvas != null)
                    {
                        Transform t = FindChildRecursive(canvas.transform, "Mobile_Controls_Root");
                        if (t != null) mobileControlsRoot = t.gameObject;
                    }
                }
            }

            if (mobileControlsRoot != null)
            {
                if (virtualJoystick == null)
                {
                    Transform j = FindChildRecursive(mobileControlsRoot.transform, "Joystick_Background");
                    if (j != null) virtualJoystick = j.gameObject;
                }

                if (touchLookZone == null)
                {
                    Transform l = FindChildRecursive(mobileControlsRoot.transform, "Touch_Look_Zone");
                    if (l != null) touchLookZone = l.gameObject;
                }

                if (actionButtons == null || actionButtons.Length == 0)
                {
                    string[] btnNames = { "Mobile_ActionButton", "Mobile_SprintButton", "Mobile_CrouchButton", "Mobile_FlashlightButton", "Mobile_PauseButton" };
                    var btnList = new System.Collections.Generic.List<GameObject>();
                    foreach (var bName in btnNames)
                    {
                        Transform b = FindChildRecursive(mobileControlsRoot.transform, bName);
                        if (b != null) btnList.Add(b.gameObject);
                    }
                    actionButtons = btnList.ToArray();
                }
            }
        }

        private static Transform FindChildRecursive(Transform parent, string targetName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == targetName) return child;
                Transform nested = FindChildRecursive(child, targetName);
                if (nested != null) return nested;
            }
            return null;
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
