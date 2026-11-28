using System;
using UnityEngine;
using UnityEngine.UI;
using TheLastBell.Cinematics;
using TheLastBell.Player;
using TheLastBell.Interaction;
using TheLastBell.UI;

namespace TheLastBell.Gameplay
{
    public enum ChapterObjectiveState
    {
        Arrival = 0,
        EnterBuilding = 1,
        ExploreInstitution = 2,
        LocateElectricalPanel = 3,
        OperateElectricalPanel = 4,
        DiscoverFestek = 5,
        PickUpFestek = 6,
        LocateFestekSocket = 7,
        InsertFestek = 8,
        ActivateFestek = 9,
        RestoreElectricity = 10,
        TelevisionActivation = 11,
        ChapterMilestoneComplete = 12
    }

    /// <summary>
    /// THE LAST BELL - Bölüm I (Varış / The Arrival) Anlatı ve Hedef Durum Makinesi.
    /// Oyuncunun terk edilmiş HARPUT ENGLISH COURSE binasına varışından televizyonun
    /// devreye girdiği bölüm dönüm noktasına kadar olan 13 aşamalı ilerleyişi yönetir.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChapterObjectiveManager : MonoBehaviour
    {
        public static ChapterObjectiveManager Instance { get; private set; }

        [Header("--- Durum & Referanslar ---")]
        [SerializeField] private ChapterObjectiveState currentState = ChapterObjectiveState.Arrival;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private DoorInteractable mainEntranceDoor;
        [SerializeField] private Transform electricalPanelTransform;
        [SerializeField] private Transform festekSocketTransform;

        private Text objectiveHudText;
        private Text objectiveHeader;
        private CanvasGroup objectiveHudGroup;
        private CanvasGroup introGroup;
        private SeamlessIntroManager intro;
        private float introTime = -1f;
        private float objectiveChangeTime = float.NegativeInfinity;
        private bool entranceOpened;
        [SerializeField] private AudioSource objectiveAudio;
        [SerializeField] private AudioClip objectiveCue;
        private string pendingObjective;
        private float objectiveFadeStarted;

        public void ConfigureCue(AudioSource source, AudioClip cue)
        {
            objectiveAudio = source;
            objectiveCue = cue;
        }

        public ChapterObjectiveState CurrentState => currentState;
        public bool IsMilestoneComplete => currentState == ChapterObjectiveState.ChapterMilestoneComplete;

        public event Action<ChapterObjectiveState> OnObjectiveChanged;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            ResolveReferences();
            EnsureObjectiveHudWidget();
            RefreshObjectiveHudText();
        }

        private void OnEnable()
        {
            if (mainEntranceDoor != null)
            {
                mainEntranceDoor.OnDoorStateChanged += HandleMainEntranceDoorChanged;
            }
        }

        private void OnDisable()
        {
            if (mainEntranceDoor != null)
            {
                mainEntranceDoor.OnDoorStateChanged -= HandleMainEntranceDoorChanged;
            }
        }

        public void Configure(Transform player, DoorInteractable entranceDoor, Transform panel, Transform socket)
        {
            playerTransform = player;
            mainEntranceDoor = entranceDoor;
            electricalPanelTransform = panel;
            festekSocketTransform = socket;
        }

        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                var fpc = FindAnyObjectByType<FirstPersonController>();
                if (fpc != null) playerTransform = fpc.transform;
            }
            if (mainEntranceDoor == null)
            {
                var doorGo = GameObject.Find("SchoolEntrance_Doorway");
                if (doorGo != null) mainEntranceDoor = doorGo.GetComponentInChildren<DoorInteractable>(true);
            }
        }

        public void InitializePresentation()
        {
            EnsureObjectiveHudWidget();
            RefreshObjectiveHudText();
        }

        private void Start()
        {
            intro = FindAnyObjectByType<SeamlessIntroManager>();
            InitializePresentation();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureObjectiveHudWidget()
        {
            if (objectiveHudGroup != null) return;
            var hud = GameObject.Find("Gameplay_HUD_Canvas");
            if (hud == null) return;
            Transform safeArea = hud.transform.Find("Mobile_Controls_Root");
            if (safeArea == null) safeArea = hud.transform;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            objectiveHudGroup = MakeHudGroup("Chapter_Objective_Tracker", safeArea,
                new Vector2(0f, 1f), new Vector2(24f, -30f), new Vector2(244f, 67f));
            var background = objectiveHudGroup.gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.047f, 0.055f, 0.78f);
            background.raycastTarget = false;
            objectiveHeader = MakeLabel("Objective_Header", objectiveHudGroup.transform, font, 14,
                TextAnchor.MiddleLeft, new Vector2(12, 39), new Vector2(-10, -4), "GÖREV");
            objectiveHeader.color = new Color(0.84f, 0.75f, 0.58f);
            objectiveHudText = MakeLabel("Objective_Label", objectiveHudGroup.transform, font, 17,
                TextAnchor.MiddleLeft, new Vector2(12, 2), new Vector2(-10, -24), "");

            introGroup = MakeHudGroup("Chapter_Introduction", safeArea, new Vector2(0.5f, 1f),
                new Vector2(0f, -67f), new Vector2(250f, 90f));
            var number = MakeLabel("Chapter_Number", introGroup.transform, font, 19,
                TextAnchor.MiddleCenter, new Vector2(0, 50), Vector2.zero, "BÖLÜM 1");
            number.color = new Color(0.89f, 0.79f, 0.62f);
            var subtitle = MakeLabel("Chapter_Subtitle", introGroup.transform, font, 33,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(0, -36), "Hoş Geldin");
            subtitle.fontStyle = FontStyle.Italic;
            introGroup.alpha = 0f;
        }

        private static CanvasGroup MakeHudGroup(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var group = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<CanvasGroup>();
            var rect = (RectTransform)group.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private static Text MakeLabel(string name, Transform parent, Font font, int size, TextAnchor alignment,
            Vector2 lower, Vector2 upper, string content)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = lower;
            label.rectTransform.offsetMax = upper;
            label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.text = content;
            label.raycastTarget = false;
            label.color = new Color(0.93f, 0.93f, 0.91f);
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        private void Update()
        {
            if (playerTransform != null) EvaluateSpatialProgression(playerTransform.position);
            StepPresentation(Time.unscaledTime, intro == null || intro.IsIntroFinished);
        }

        public void StepPresentation(float now, bool controlsReady)
        {
            if (introGroup == null) InitializePresentation();
            if (introGroup == null) return;
            if (!controlsReady)
            {
                objectiveHudGroup.alpha = 0f;
                return;
            }
            if (introTime < 0f) introTime = now;
            float age = now - introTime;
            introGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(age / 0.65f, (5.45f - age) / 0.8f)));

            float changeAge = now - objectiveChangeTime;
            objectiveHeader.text = changeAge < 0.9f ? "✓ GÖREV" : "GÖREV";
            if (pendingObjective != null)
            {
                float fadeAge = now - objectiveFadeStarted;
                objectiveHudGroup.alpha = 1f - Mathf.Clamp01(fadeAge / 0.18f);
                if (fadeAge < 0.18f) return;
                objectiveHudText.text = pendingObjective;
                pendingObjective = null;
                objectiveChangeTime = now;
                if (objectiveAudio != null && objectiveCue != null) objectiveAudio.PlayOneShot(objectiveCue, 0.38f);
                return;
            }
            objectiveHudGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(changeAge / 0.42f));
        }

        public void EvaluateSpatialProgression(Vector3 playerPos)
        {
            switch (currentState)
            {
                case ChapterObjectiveState.Arrival:
                    // Avlu yürüyüş yolunda binaya yaklaşıldığında
                    if (playerPos.z > -11.5f)
                    {
                        AdvanceTo(ChapterObjectiveState.EnterBuilding,
                            "Harput İngilizce Kursu... Son görüşmeden sonra buraya gelmiş olmalı.");
                    }
                    break;

                case ChapterObjectiveState.EnterBuilding:
                    // Giriş eşiğini geçtiğinde (z > 0.6m)
                    if ((entranceOpened || (mainEntranceDoor != null && mainEntranceDoor.IsOpen)) &&
                        playerPos.z > 0.65f && Mathf.Abs(playerPos.x) < 1.9f)
                    {
                        AdvanceTo(ChapterObjectiveState.ExploreInstitution,
                            "İçeride elektrik tamamen kesik. Koridor karanlık.");
                    }
                    break;

                case ChapterObjectiveState.ExploreInstitution:
                    if (playerPos.z > 3.8f)
                    {
                        AdvanceTo(ChapterObjectiveState.LocateElectricalPanel,
                            "Binaya güç veren ana elektrik panosunu bulmalıyım.");
                    }
                    break;

                case ChapterObjectiveState.LocateElectricalPanel:
                    if (electricalPanelTransform != null &&
                        Vector3.Distance(playerPos, electricalPanelTransform.position) <= 3.6f)
                    {
                        AdvanceTo(ChapterObjectiveState.OperateElectricalPanel, null);
                    }
                    break;

                case ChapterObjectiveState.LocateFestekSocket:
                    if (festekSocketTransform != null &&
                        Vector3.Distance(playerPos, festekSocketTransform.position) <= 3.2f)
                    {
                        AdvanceTo(ChapterObjectiveState.InsertFestek, null);
                    }
                    break;
            }
        }

        private void HandleMainEntranceDoorChanged(bool isOpen)
        {
            entranceOpened |= isOpen;
            if (isOpen && currentState == ChapterObjectiveState.Arrival)
            {
                AdvanceTo(ChapterObjectiveState.EnterBuilding, null);
            }
        }

        public void NotifyPanelLeverOperated()
        {
            if (currentState < ChapterObjectiveState.OperateElectricalPanel)
            {
                currentState = ChapterObjectiveState.OperateElectricalPanel;
            }
            AdvanceTo(ChapterObjectiveState.DiscoverFestek,
                "Şalter boşa düştü... Panonun içinden yere ağır metal bir parça yuvarlandı.");
        }

        public void NotifyFestekDiscoveredOnFloor()
        {
            if (currentState <= ChapterObjectiveState.DiscoverFestek)
            {
                AdvanceTo(ChapterObjectiveState.PickUpFestek, null);
            }
        }

        public void NotifyFestekPickedUp()
        {
            if (currentState < ChapterObjectiveState.PickUpFestek)
            {
                currentState = ChapterObjectiveState.PickUpFestek;
            }
            AdvanceTo(ChapterObjectiveState.LocateFestekSocket,
                "Eski tip bir devre köprüsü — Feştek. Yakındaki bağlantı yuvasına takılmalı.");
        }

        public void NotifyFestekInserting()
        {
            if (currentState < ChapterObjectiveState.InsertFestek)
            {
                AdvanceTo(ChapterObjectiveState.InsertFestek, null);
            }
        }

        public void NotifyFestekSeated()
        {
            AdvanceTo(ChapterObjectiveState.ActivateFestek, null);
        }

        public void NotifyFestekActivated()
        {
            AdvanceTo(ChapterObjectiveState.RestoreElectricity,
                "Devre tamamlandı... Hatlara elektrik geliyor.");
        }

        public void NotifyTelevisionActivated()
        {
            AdvanceTo(ChapterObjectiveState.TelevisionActivation, null);
        }

        public void NotifyMilestoneComplete()
        {
            AdvanceTo(ChapterObjectiveState.ChapterMilestoneComplete,
                "Köşedeki eski televizyon kendi kendine açıldı... Yalnız değilim ya da biri az önce buradaydı.");
        }

        public void AdvanceTo(ChapterObjectiveState nextState, string statusThought)
        {
            if (nextState <= currentState) return;
            string previous = GetObjectiveDescription(currentState);
            string next = GetObjectiveDescription(nextState);
            currentState = nextState;
            if (previous != next && Application.isPlaying && objectiveHudText != null)
            {
                pendingObjective = next;
                objectiveFadeStarted = Time.unscaledTime;
            }
            else if (pendingObjective == null) RefreshObjectiveHudText();

            if (!string.IsNullOrEmpty(statusThought) && PlayerHUD.Instance != null)
                PlayerHUD.Instance.ShowStatusMessage(statusThought);

            OnObjectiveChanged?.Invoke(currentState);
        }

        public static string GetObjectiveDescription(ChapterObjectiveState state)
        {
            switch (state)
            {
                case ChapterObjectiveState.Arrival:
                    return "İngilizce kursuna gir.";
                case ChapterObjectiveState.EnterBuilding:
                    return "İngilizce kursuna gir.";
                case ChapterObjectiveState.ExploreInstitution:
                    return "Binayı keşfet.";
                case ChapterObjectiveState.LocateElectricalPanel:
                    return "Elektrik panosunu bul.";
                case ChapterObjectiveState.OperateElectricalPanel:
                    return "Panoyu kontrol et.";
                case ChapterObjectiveState.DiscoverFestek:
                case ChapterObjectiveState.PickUpFestek:
                    return "Feştek'i yerden al.";
                case ChapterObjectiveState.LocateFestekSocket:
                    return "Feştek yuvasını bul.";
                case ChapterObjectiveState.InsertFestek:
                    return "Feştek'i yuvasına yerleştir.";
                case ChapterObjectiveState.ActivateFestek:
                    return "Feştek’i saat yönünde çevir.";
                case ChapterObjectiveState.RestoreElectricity:
                    return "Elektriğin gelmesini bekle.";
                case ChapterObjectiveState.TelevisionActivation:
                    return "Televizyonu kontrol et.";
                case ChapterObjectiveState.ChapterMilestoneComplete:
                    return "Televizyonu kontrol et.";
                default:
                    return "";
            }
        }

        private void RefreshObjectiveHudText()
        {
            EnsureObjectiveHudWidget();
            if (objectiveHudText != null)
            {
                objectiveHudText.text = GetObjectiveDescription(currentState);
            }
        }
    }
}
