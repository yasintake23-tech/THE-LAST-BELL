using System;
using System.Collections;
using UnityEngine;
using TheLastBell.Player;
using TheLastBell.Gameplay;

namespace TheLastBell.Interaction
{
    public enum FestekSocketState
    {
        Empty = 0,
        Inserting = 1,
        SeatedReadyToActivate = 2,
        Activating = 3,
        Activated = 4
    }

    /// <summary>
    /// THE LAST BELL - Feştek Bağlantı Yuvası ve Etkinleştirme Düğmesi.
    /// - Oyuncu elinde Feştek yokken incelerse gerçekçi, sade bir gözlem mesajı verir.
    /// - Oyuncu sağ elinde Feştek tutarken etkileşime geçtiğinde:
    ///   1) Sağ el IK ile Feştek'i yuvanın ağzına hizalar ve içeri kaydırarak oturtur (InsertFestek).
    ///   2) Ardından el/parmak etkinleştirme düğmesine (Activation_Button) basar (ActivateFestek).
    ///   3) Sol eldeki el fenerini sorunsuzca sağ ele geri döndürür ve FacilityPowerManager'ı tetikler.
    /// </summary>
    [DisallowMultipleComponent]
    public class FestekSocketInteractable : MonoBehaviour, IInteractable
    {
        [Header("--- Yuva & Düğme Referansları ---")]
        [SerializeField] private FestekSocketState socketState = FestekSocketState.Empty;
        [SerializeField] private Transform insertionApproachPoint;
        [SerializeField] private Transform insertionTransform;
        [SerializeField] private Transform activationButtonMesh;
        [SerializeField] private Transform buttonFingerTarget;
        [SerializeField] private Renderer statusLensRenderer;
        [SerializeField] private FacilityPowerManager powerManager;

        [Header("--- Uzamsal 3D Sesler ---")]
        [SerializeField] private AudioSource socketAudioSource;
        [SerializeField] private AudioClip socketInsertClip;
        [SerializeField] private AudioClip buttonClickClip;

        private FestekItem seatedFestek;
        private Vector3 buttonRestLocalPos;

        public FestekSocketState SocketState => socketState;
        public bool IsActivated => socketState == FestekSocketState.Activated;
        public FestekItem SeatedFestek => seatedFestek;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void Configure(
            Transform approachPt, Transform insertPt, Transform buttonMesh, Transform fingerPt,
            Renderer lensRend, FacilityPowerManager facilityMgr,
            AudioSource audioSrc, AudioClip insertSfx, AudioClip clickSfx)
        {
            insertionApproachPoint = approachPt;
            insertionTransform = insertPt;
            activationButtonMesh = buttonMesh;
            buttonFingerTarget = fingerPt;
            statusLensRenderer = lensRend;
            powerManager = facilityMgr;
            socketAudioSource = audioSrc;
            socketInsertClip = insertSfx;
            buttonClickClip = clickSfx;
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (insertionApproachPoint == null) insertionApproachPoint = FindDeepChild(transform, "Insertion_Approach_Point");
            if (insertionTransform == null) insertionTransform = FindDeepChild(transform, "Insertion_Transform");
            if (activationButtonMesh == null) activationButtonMesh = FindDeepChild(transform, "Activation_Button");
            if (buttonFingerTarget == null) buttonFingerTarget = FindDeepChild(transform, "Button_Finger_Target");
            if (statusLensRenderer == null)
            {
                Transform lensT = FindDeepChild(transform, "Socket_StatusLens");
                if (lensT != null) statusLensRenderer = lensT.GetComponent<Renderer>();
            }
            if (activationButtonMesh != null && buttonRestLocalPos == Vector3.zero)
            {
                buttonRestLocalPos = activationButtonMesh.localPosition;
            }
        }

        public string GetInteractionPrompt()
        {
            if (socketState == FestekSocketState.Inserting || socketState == FestekSocketState.Activating || socketState == FestekSocketState.Activated)
                return "";

            if (socketState == FestekSocketState.SeatedReadyToActivate)
                return "[E] - Devre Düğmesine Bas";

            FestekItem held = FindHeldFestek();
            if (held != null)
            {
                return "[E] - Feştek'i Yuvasına Yerleştir";
            }
            return "[E] - Bağlantı Yuvasını İncele";
        }

        public bool CanInteract()
        {
            return socketState == FestekSocketState.Empty || socketState == FestekSocketState.SeatedReadyToActivate;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract()) return;
            GameObject playerRoot = interactor != null ? interactor.gameObject : null;

            if (socketState == FestekSocketState.SeatedReadyToActivate)
            {
                StartCoroutine(PressActivationButtonRoutine(playerRoot));
                return;
            }

            FestekItem held = FindHeldFestek();
            if (held == null)
            {
                interactor?.ShowStatusMessage("Özel bir köprü yuvası... Buraya tam oturacak metal bir bağlantı parçası gerekiyor.");
                return;
            }

            StartCoroutine(InsertAndActivateRoutine(held, playerRoot));
        }

        private IEnumerator InsertAndActivateRoutine(FestekItem festek, GameObject playerRoot)
        {
            InitializeIfNeeded();
            socketState = FestekSocketState.Inserting;
            ChapterObjectiveManager.Instance?.NotifyFestekInserting();

            CharacterProceduralController proc = playerRoot != null
                ? playerRoot.GetComponentInChildren<CharacterProceduralController>(true)
                : null;

            Vector3 approachPos = insertionApproachPoint != null ? insertionApproachPoint.position : transform.position + transform.forward * 0.24f;
            Quaternion insertRot = insertionTransform != null ? insertionTransform.rotation : Quaternion.LookRotation(-transform.forward, Vector3.up);

            // 1. Sağ eli yuva girişine hizala (0.28 sn)
            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.deltaTime;
                float w = Mathf.SmoothStep(0f, 0.92f, t / 0.28f);
                proc?.SetRightHandExplicitOverride(approachPos, insertRot, w);
                yield return null;
            }

            // 2. Feştek'i yuvanın içine kaydırarak oturt (0.46 sn)
            if (socketAudioSource != null && socketInsertClip != null)
            {
                socketAudioSource.PlayOneShot(socketInsertClip, 0.92f);
            }

            festek.transform.SetParent(null, true);
            Vector3 startPos = approachPos;
            Quaternion startRot = insertRot;
            Vector3 alignLocal = festek.InsertionAlignPoint != null ? festek.InsertionAlignPoint.localPosition : Vector3.zero;
            Vector3 seatedTargetPos = insertionTransform != null
                ? insertionTransform.TransformPoint(-alignLocal)
                : transform.position;
            Quaternion seatedTargetRot = insertionTransform != null ? insertionTransform.rotation : startRot;

            t = 0f;
            while (t < 0.46f)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / 0.46f);
                Vector3 curPos = Vector3.Lerp(startPos, seatedTargetPos, u);
                Quaternion curRot = Quaternion.Slerp(startRot, seatedTargetRot, u);
                festek.transform.SetPositionAndRotation(curPos, curRot);
                proc?.SetRightHandExplicitOverride(curPos - seatedTargetRot * Vector3.forward * 0.06f, curRot, 0.92f);
                yield return null;
            }

            seatedFestek = festek;
            festek.SeatIntoSocket(insertionTransform, playerRoot);
            socketState = FestekSocketState.SeatedReadyToActivate;

            // 3. Doğrudan etkinleştirme düğmesine basma aşamasına geç
            yield return PressActivationButtonRoutine(playerRoot);
        }

        private IEnumerator PressActivationButtonRoutine(GameObject playerRoot)
        {
            socketState = FestekSocketState.Activating;
            CharacterProceduralController proc = playerRoot != null
                ? playerRoot.GetComponentInChildren<CharacterProceduralController>(true)
                : null;

            Vector3 fingerPos = buttonFingerTarget != null ? buttonFingerTarget.position : transform.position;
            Quaternion fingerRot = buttonFingerTarget != null ? buttonFingerTarget.rotation : Quaternion.identity;

            // Sağ eli düğmeye götür (0.24 sn)
            float t = 0f;
            while (t < 0.24f)
            {
                t += Time.deltaTime;
                proc?.SetRightHandExplicitOverride(fingerPos, fingerRot, 0.88f);
                yield return null;
            }

            // Düğmeyi fiziksel olarak içeri bastır + klik sesi (0.18 sn)
            if (socketAudioSource != null && buttonClickClip != null)
            {
                socketAudioSource.PlayOneShot(buttonClickClip, 0.95f);
            }

            Vector3 pressedLocal = buttonRestLocalPos + new Vector3(0f, 0f, -0.008f);
            if (activationButtonMesh != null) activationButtonMesh.localPosition = pressedLocal;

            IlluminateStatusLens(true);
            socketState = FestekSocketState.Activated;
            ChapterObjectiveManager.Instance?.NotifyFestekActivated();

            yield return new WaitForSeconds(0.18f);

            if (activationButtonMesh != null)
                activationButtonMesh.localPosition = Vector3.Lerp(pressedLocal, buttonRestLocalPos, 0.55f);

            proc?.ClearRightHandExplicitOverride();

            // Sağ el artık serbest; sol eldeki feneri pürüzsüzce ana tutuşa geri döndür
            if (playerRoot != null)
            {
                var flashPhys = playerRoot.GetComponentInChildren<FlashlightPhysicalItem>(true);
                flashPhys?.ReturnFromLeftHand();
            }

            if (powerManager == null) powerManager = FindAnyObjectByType<FacilityPowerManager>();
            powerManager?.BeginPowerRestorationSequence();
        }

        public void ExecuteImmediateForTest(FestekItem festek, GameObject playerRoot)
        {
            InitializeIfNeeded();
            ChapterObjectiveManager.Instance?.NotifyFestekInserting();
            seatedFestek = festek;
            festek?.SeatIntoSocket(insertionTransform, playerRoot);
            IlluminateStatusLens(true);
            socketState = FestekSocketState.Activated;
            ChapterObjectiveManager.Instance?.NotifyFestekActivated();

            if (playerRoot != null)
            {
                var proc = playerRoot.GetComponentInChildren<CharacterProceduralController>(true);
                proc?.ClearRightHandExplicitOverride();
                var flashPhys = playerRoot.GetComponentInChildren<FlashlightPhysicalItem>(true);
                flashPhys?.ReturnFromLeftHand();
            }

            if (powerManager == null) powerManager = FindAnyObjectByType<FacilityPowerManager>();
            powerManager?.ExecuteImmediateForTest();
        }

        private void IlluminateStatusLens(bool live)
        {
            if (statusLensRenderer == null) return;
            var mpb = new MaterialPropertyBlock();
            statusLensRenderer.GetPropertyBlock(mpb);
            Color c = live ? new Color(0.32f, 0.78f, 0.36f, 1f) : new Color(0.16f, 0.20f, 0.15f, 1f);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_EmissionColor", live ? new Color(0.28f, 0.85f, 0.32f) * 1.4f : Color.black);
            statusLensRenderer.SetPropertyBlock(mpb);
        }

        private static FestekItem FindHeldFestek()
        {
            var all = FindObjectsByType<FestekItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].CurrentState == FestekItemState.HeldInRightHand)
                    return all[i];
            }
            return null;
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (string.Equals(c.name, targetName, StringComparison.OrdinalIgnoreCase)) return c;
                Transform sub = FindDeepChild(c, targetName);
                if (sub != null) return sub;
            }
            return null;
        }
    }
}
