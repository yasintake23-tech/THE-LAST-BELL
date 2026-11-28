using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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

        [Header("--- HUD Hedef Gösterimi ---")]
        [SerializeField] private TextMeshProUGUI objectiveHudText;
        [SerializeField] private CanvasGroup objectiveHudGroup;

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

        private void EnsureObjectiveHudWidget()
        {
            if (objectiveHudText != null) return;

            var hudCanvas = GameObject.Find("Gameplay_HUD_Canvas");
            if (hudCanvas == null) return;

            Transform existing = hudCanvas.transform.Find("Chapter_Objective_Tracker");
            if (existing != null)
            {
                objectiveHudGroup = existing.GetComponent<CanvasGroup>();
                objectiveHudText = existing.GetComponentInChildren<TextMeshProUGUI>(true);
                return;
            }

            var rootGo = new GameObject("Chapter_Objective_Tracker", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            rootGo.transform.SetParent(hudCanvas.transform, false);
            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -24f);
            rt.sizeDelta = new Vector2(520f, 48f);

            var bg = rootGo.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.07f, 0.52f);
            bg.raycastTarget = false;

            objectiveHudGroup = rootGo.GetComponent<CanvasGroup>();
            objectiveHudGroup.alpha = 0.92f;
            objectiveHudGroup.interactable = false;
            objectiveHudGroup.blocksRaycasts = false;

            var txtGo = new GameObject("Objective_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(rootGo.transform, false);
            var txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(14f, 6f);
            txtRt.offsetMax = new Vector2(-12f, -6f);

            objectiveHudText = txtGo.GetComponent<TextMeshProUGUI>();
            objectiveHudText.fontSize = 18f;
            objectiveHudText.color = new Color(0.86f, 0.88f, 0.90f, 0.92f);
            objectiveHudText.alignment = TextAlignmentOptions.MidlineLeft;
            objectiveHudText.raycastTarget = false;
        }

        private void Update()
        {
            if (playerTransform != null)
            {
                EvaluateSpatialProgression(playerTransform.position);
            }
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
                    if (playerPos.z > 0.65f)
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
            if (nextState < currentState) return;
            bool changed = (nextState != currentState);
            currentState = nextState;

            RefreshObjectiveHudText();

            if (!string.IsNullOrEmpty(statusThought) && PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.ShowStatusMessage(statusThought);
            }

            if (changed)
            {
                OnObjectiveChanged?.Invoke(currentState);
            }
        }

        public static string GetObjectiveDescription(ChapterObjectiveState state)
        {
            switch (state)
            {
                case ChapterObjectiveState.Arrival:
                    return "Kuruma yaklaş ve girişi kontrol et";
                case ChapterObjectiveState.EnterBuilding:
                    return "Harput İngilizce Kursu binasına gir";
                case ChapterObjectiveState.ExploreInstitution:
                    return "Karanlık kurumu incele";
                case ChapterObjectiveState.LocateElectricalPanel:
                    return "Elektrik panosunu bul";
                case ChapterObjectiveState.OperateElectricalPanel:
                    return "Ana elektrik şalterini indir";
                case ChapterObjectiveState.DiscoverFestek:
                case ChapterObjectiveState.PickUpFestek:
                    return "Panodan düşen Feştek parçasını yerden al";
                case ChapterObjectiveState.LocateFestekSocket:
                    return "Feştek bağlantı yuvasını bul";
                case ChapterObjectiveState.InsertFestek:
                    return "Feştek'i bağlantı yuvasına yerleştir";
                case ChapterObjectiveState.ActivateFestek:
                    return "Üzerindeki düğmeye basarak devreyi etkinleştir";
                case ChapterObjectiveState.RestoreElectricity:
                    return "Elektriğin geri gelişini bekle";
                case ChapterObjectiveState.TelevisionActivation:
                    return "Çalışmaya başlayan televizyona bak";
                case ChapterObjectiveState.ChapterMilestoneComplete:
                    return "Televizyondaki parazit sinyali incele";
                default:
                    return "";
            }
        }

        private void RefreshObjectiveHudText()
        {
            EnsureObjectiveHudWidget();
            if (objectiveHudText != null)
            {
                objectiveHudText.text = "• " + GetObjectiveDescription(currentState);
            }
        }
    }
}
