using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TheLastBell.Player;
using TheLastBell.Gameplay;
using TheLastBell.UI;

namespace TheLastBell.Interaction
{
    public enum FestekSocketState
    {
        Empty,
        Inserting,
        SeatedReadyToActivate,
        Activating,
        Activated,
        Withdrawing
    }

    [DisallowMultipleComponent]
    public sealed class FestekSocketInteractable : MonoBehaviour, IInteractable
    {
        private const float RequiredTurn = 90f;
        [SerializeField] private FestekSocketState socketState;
        [SerializeField] private Transform insertionTransform;
        [SerializeField] private Renderer statusLensRenderer;
        [SerializeField] private FacilityPowerManager powerManager;
        [SerializeField] private AudioSource socketAudioSource;
        [SerializeField] private AudioClip socketInsertClip;
        [SerializeField] private AudioClip buttonClickClip;
        [SerializeField] private InventoryItemDefinition inventoryItem;

        private FestekItem seatedFestek;
        private GameObject carrier;
        private MaterialPropertyBlock lensProperties;
        private static readonly int LensColor = Shader.PropertyToID("_Color");
        private static readonly int LensEmission = Shader.PropertyToID("_EmissionColor");
        private InteractionFocus focus;
        private ProtagonistAnimationController animation;
        private Transform wrist;
        private GameObject rotationPanel;
        private Vector3 itemToHandOffset;
        private Quaternion itemToHandRotation;
        private float turn;
        private int notch;
        private bool removing;
        [SerializeField] private bool elevatorSocket;
        public bool AllowElevatorUse { get; set; }
        public event System.Action ElevatorLocked;
        public bool IsElevatorSocket => elevatorSocket;
        public FestekSocketState SocketState => socketState;
        public bool IsActivated => socketState == FestekSocketState.Activated;
        public FestekItem SeatedFestek => seatedFestek;
        public float RotationProgress => Mathf.Clamp01(turn / RequiredTurn);
        public float RotationDegrees => turn;
        public Transform InsertionAnchor => insertionTransform;

        public void Configure(Transform insertPoint, Renderer lens, FacilityPowerManager power,
            AudioSource source, AudioClip insert, AudioClip click)
        {
            insertionTransform = insertPoint;
            statusLensRenderer = lens;
            powerManager = power;
            socketAudioSource = source;
            socketInsertClip = insert;
            buttonClickClip = click;
        }

        public void ConfigureInventoryItem(InventoryItemDefinition definition) => inventoryItem = definition;
        public void ConfigureElevatorSocket() => elevatorSocket = true;
        public InteractionFocus ActiveFocus => focus;

        private bool CanRemove => !elevatorSocket && IsActivated && seatedFestek != null && !removing && focus == null &&
            powerManager != null && powerManager.IsPowerRestored;

        public string GetInteractionPrompt() => socketState == FestekSocketState.SeatedReadyToActivate
            ? "[E] - Feştek’i çevir" : socketState == FestekSocketState.Empty ? (elevatorSocket ? "[E] - Feştek’i kullan" : GetEmptyPrompt()) : CanRemove ? "[E] - Feştek’i çıkar" : "";

        private static string GetEmptyPrompt()
        {
            var manager = ChapterObjectiveManager.Instance;
            return manager != null && (manager.CurrentState == ChapterObjectiveState.LocateFestekSocket ||
                manager.CurrentState == ChapterObjectiveState.InsertFestek) ? "[E] - Feştek’i tak" : "[E] - Yuvayı incele";
        }

        private void Update()
        {
            if (statusLensRenderer == null) return;
            var player = ChapterObjectiveManager.Instance;
            bool guiding = socketState == FestekSocketState.Empty && (elevatorSocket ? AllowElevatorUse :
                player != null && (player.CurrentState == ChapterObjectiveState.InsertFestek ||
                 player.CurrentState == ChapterObjectiveState.LocateFestekSocket));
            if (lensProperties == null) lensProperties = new MaterialPropertyBlock();
            statusLensRenderer.GetPropertyBlock(lensProperties);
            float glow = guiding ? 0.17f + 0.12f * Mathf.Sin(Time.time * 2.8f) : 0f;
            if (IsActivated)
            {
                float strength = CanRemove ? 0.83f + 0.17f * Mathf.Sin(Time.time * 2.4f) : 1f;
                lensProperties.SetColor(LensEmission, new Color(0.26f, 0.68f, 0.28f) * strength);
            }
            else lensProperties.SetColor(LensEmission, new Color(0.80f, 0.56f, 0.26f) * glow);
            statusLensRenderer.SetPropertyBlock(lensProperties);
        }

        public bool CanInteract() => (!elevatorSocket || AllowElevatorUse) &&
            (socketState == FestekSocketState.Empty || socketState == FestekSocketState.SeatedReadyToActivate || CanRemove);

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract() || interactor == null || insertionTransform == null) return;
            GameObject player = interactor.gameObject;
            var controller = player.GetComponentInChildren<ProtagonistAnimationController>(true);
            controller?.InitializeIfNeeded();
            if (controller == null || controller.LeftHandFestekSocket == null) return;
            Vector3 toSocket = insertionTransform.position - player.transform.position;
            if (new Vector2(toSocket.x, toSocket.z).sqrMagnitude > 1.16f * 1.16f)
            {
                interactor.ShowStatusMessage("İşlem için yuvaya biraz daha yaklaş.");
                return;
            }

            if (CanRemove)
            {
                var inventory = player.GetComponent<PlayerInventory>();
                if (controller.IsHoldingFestek || inventory == null || inventory.EquippedItem != null)
                {
                    interactor.ShowStatusMessage("Feştek’i çıkarmak için sol elin boş olmalı.");
                    return;
                }
                if (inventoryItem != null && BeginFocus(player, controller)) StartCoroutine(RemovePoweredDevice(player, inventory));
                return;
            }

            if (socketState == FestekSocketState.SeatedReadyToActivate)
            {
                if (seatedFestek == null || !BeginFocus(player, controller)) return;
                OpenRotationPanel();
                return;
            }

            var item = controller.LeftHandFestekSocket.GetComponentInChildren<FestekItem>();
            if (item == null || item.CurrentState != FestekItemState.HeldInLeftHand)
            {
                interactor.ShowStatusMessage(elevatorSocket ? "Feştek’i envanterden eline al." :
                    "Yuvaya uygun metal bir bağlantı parçası gerekiyor.");
                return;
            }

            if (!BeginFocus(player, controller)) return;
            carrier = player;
            Transform hand = controller.LeftHandFestekSocket.parent;
            itemToHandRotation = Quaternion.Inverse(item.transform.rotation) * hand.rotation;
            itemToHandOffset = Quaternion.Inverse(item.transform.rotation) * (hand.position - item.transform.position);
            StartCoroutine(Insert(item, player));
        }

        private bool BeginFocus(GameObject player, ProtagonistAnimationController controller)
        {
            focus = InteractionFocus.Begin(player, insertionTransform, this);
            if (focus == null) return false;
            animation = controller;
            animation.IsPosingFestek = true;
            if (wrist == null)
            {
                wrist = new GameObject("Festek_Left_Wrist_Target").transform;
                wrist.SetParent(transform, false);
            }
            return true;
        }

        private IEnumerator ApproachSocket(GameObject player)
        {
            for (float t = 0f; t < 0.95f && focus != null && focus.IsFocused; t += Time.deltaTime)
            {
                Vector3 offset = Vector3.ProjectOnPlane(insertionTransform.position - player.transform.position, Vector3.up);
                if (offset.magnitude <= 0.57f) yield break;
                focus.MoveTowards(insertionTransform.position - offset.normalized * 0.57f, 0.75f, Time.deltaTime);
                yield return null;
            }
        }

        private IEnumerator RemovePoweredDevice(GameObject player, PlayerInventory inventory)
        {
            removing = true;
            yield return ApproachSocket(player);
            Transform hand = animation.LeftHandFestekSocket.parent;
            Transform device = seatedFestek.transform;
            Vector3 seatedPosition = device.localPosition;
            for (float t = 0f; t < 0.48f; t += Time.deltaTime)
            {
                PoseHand(device.position, device.rotation, Mathf.SmoothStep(0f, 1f, t / 0.48f));
                yield return null;
            }
            PoseHand(device.position, device.rotation);
            yield return null;
            if (Vector3.Distance(wrist.position, hand.position) <= 0.13f)
            {
                Play(socketInsertClip, 0.48f);
                for (float t = 0f; t < 0.45f; t += Time.deltaTime)
                {
                    device.localPosition = seatedPosition - Vector3.forward * (0.15f * Mathf.SmoothStep(0f, 1f, t / 0.45f));
                    PoseHand(device.position, device.rotation);
                    yield return null;
                }
                device.localPosition = seatedPosition - Vector3.forward * 0.15f;
                PoseHand(device.position, device.rotation);
                yield return null;
                if (Vector3.Distance(wrist.position, hand.position) <= 0.10f && TransferPoweredDevice(player, inventory))
                {
                    for (float t = 0f; t < 0.26f; t += Time.deltaTime)
                    {
                        PoseHand(device.position, device.rotation, 1f - Mathf.Clamp01(t / 0.26f));
                        yield return null;
                    }
                }
            }
            if (seatedFestek != null)
            {
                Vector3 from = device.localPosition;
                for (float t = 0f; t < 0.24f; t += Time.deltaTime)
                {
                    float blend = Mathf.SmoothStep(0f, 1f, t / 0.24f);
                    device.localPosition = Vector3.Lerp(from, seatedPosition, blend);
                    PoseHand(device.position, device.rotation, 1f - blend);
                    yield return null;
                }
                SetTurn(RequiredTurn);
                player.GetComponent<PlayerInteractor>()?.ShowStatusMessage("Feştek’i çıkarmak için yuvanın karşısına geç.");
            }
            removing = false;
            ReleaseFocus();
        }

        private bool TransferPoweredDevice(GameObject player, PlayerInventory inventory)
        {
            if (seatedFestek == null || inventory == null || inventoryItem == null || inventory.EquippedItem != null)
                return false;
            if (!seatedFestek.AttachToLeftHand(player)) return false;
            if (!inventory.TryCollectEquipped(inventoryItem, seatedFestek))
            {
                seatedFestek.SeatIntoSocket(insertionTransform, player, notify: false);
                SetTurn(RequiredTurn);
                return false;
            }
            seatedFestek = null;
            ChapterObjectiveManager.Instance?.NotifyFestekRemoved();
            return true;
        }

        public bool RemoveForVerification(GameObject player)
        {
            return !Application.isPlaying && CanRemove && player != null &&
                TransferPoweredDevice(player, player.GetComponent<PlayerInventory>());
        }

        private IEnumerator Insert(FestekItem item, GameObject player)
        {
            socketState = FestekSocketState.Inserting;
            ChapterObjectiveManager.Instance?.NotifyFestekInserting();
            yield return ApproachSocket(player);
            Vector3 offset = item.InsertionAlignPoint != null
                ? Quaternion.Inverse(item.transform.rotation) * (item.InsertionAlignPoint.position - item.transform.position)
                : Vector3.zero;
            Quaternion aligned = insertionTransform.rotation;
            Vector3 end = insertionTransform.position - aligned * offset;
            Vector3 mouth = end - insertionTransform.forward * 0.13f;
            Vector3 from = item.transform.position;
            Quaternion fromRotation = item.transform.rotation;

            for (float elapsed = 0f; elapsed < 1.24f; elapsed += Time.deltaTime)
            {
                float approach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.74f));
                float travel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 0.74f) / 0.50f));
                PoseHand(Vector3.Lerp(Vector3.Lerp(from, mouth, approach), end, travel),
                    Quaternion.Slerp(fromRotation, aligned, approach));
                yield return null;
            }

            PoseHand(end, aligned);
            float settle = 0f;
            while (settle < 0.38f && !IsAligned(item, end, aligned))
            {
                settle += Time.deltaTime;
                yield return null;
            }
            if (!IsAligned(item, end, aligned))
            {
                socketState = FestekSocketState.Empty;
                player.GetComponent<PlayerInteractor>()?.ShowStatusMessage("Yuvanın karşısına geçip biraz daha yaklaş.");
                ReleaseFocus();
                yield break;
            }
            Seat(item, player);
            Play(socketInsertClip, 0.85f);
            OpenRotationPanel();
        }

        private static bool IsAligned(FestekItem item, Vector3 position, Quaternion rotation)
        {
            return Vector3.SqrMagnitude(item.transform.position - position) <= 0.065f * 0.065f &&
                   Quaternion.Angle(item.transform.rotation, rotation) <= 28f;
        }

        private void Seat(FestekItem item, GameObject player)
        {
            carrier = player;
            seatedFestek = item;
            item.SeatIntoSocket(insertionTransform, player, notify: !elevatorSocket);
            socketState = FestekSocketState.SeatedReadyToActivate;
        }

        private void PoseHand(Vector3 itemPosition, Quaternion itemRotation, float weight = 1f)
        {
            if (animation == null || wrist == null) return;
            wrist.SetPositionAndRotation(itemPosition + itemRotation * itemToHandOffset, itemRotation * itemToHandRotation);
            animation.SetPhysicalWrist(PreferredInteractionHand.Left, wrist.position, wrist.rotation,
                inventoryItem != null ? inventoryItem.Grip : HandGripProfile.Device, weight);
        }

        private void OpenRotationPanel()
        {
            var canvas = GameObject.Find("Gameplay_HUD_Canvas");
            if (canvas == null)
            {
                ReleaseFocus();
                return;
            }

            socketState = FestekSocketState.Activating;
            rotationPanel = FestekRotationControl.Create(canvas.transform, this);
            PoseHand(seatedFestek.transform.position, seatedFestek.transform.rotation);
        }

        public void ApplyRotationDelta(float degrees)
        {
            if ((Application.isPlaying && socketState != FestekSocketState.Activating) ||
                (!Application.isPlaying && socketState != FestekSocketState.SeatedReadyToActivate) ||
                seatedFestek == null || float.IsNaN(degrees) || float.IsInfinity(degrees)) return;

            SetTurn(Mathf.Clamp(turn + degrees, -28f, RequiredTurn));
            int detent = Mathf.FloorToInt(Mathf.Abs(turn) / 22.5f);
            if (detent != notch && detent < 4) Play(buttonClickClip, 0.22f);
            notch = detent;
            if (turn < RequiredTurn) return;

            socketState = FestekSocketState.Activated;
            Play(buttonClickClip, 0.92f);
            if (statusLensRenderer != null)
            {
                var properties = new MaterialPropertyBlock();
                statusLensRenderer.GetPropertyBlock(properties);
                properties.SetColor(LensColor, new Color(0.32f, 0.78f, 0.36f));
                properties.SetColor(LensEmission, new Color(0.26f, 0.68f, 0.28f));
                statusLensRenderer.SetPropertyBlock(properties);
            }
            if (elevatorSocket)
            {
                if (rotationPanel != null) Destroy(rotationPanel);
                rotationPanel = null;
                ElevatorLocked?.Invoke();
                return;
            }
            ChapterObjectiveManager.Instance?.NotifyFestekActivated();
            if (Application.isPlaying)
            {
                powerManager?.BeginPowerRestorationSequence();
                if (rotationPanel != null) Destroy(rotationPanel);
                rotationPanel = null;
                StartCoroutine(ReleaseAfterLock());
            }
            else powerManager?.ExecuteImmediateForTest();
        }

        private void SetTurn(float angle)
        {
            turn = angle;
            if (seatedFestek == null) return;
            var device = seatedFestek.transform;
            Vector3 anchor = seatedFestek.InsertionAlignPoint != null
                ? device.InverseTransformPoint(seatedFestek.InsertionAlignPoint.position) : Vector3.zero;
            device.localRotation = Quaternion.AngleAxis(-turn, Vector3.forward);
            device.localPosition = -(device.localRotation * Vector3.Scale(anchor, device.localScale));
            PoseHand(device.position, device.rotation);
        }

        public void CancelAndWithdraw()
        {
            if (socketState != FestekSocketState.Activating && socketState != FestekSocketState.SeatedReadyToActivate) return;
            if (seatedFestek == null || carrier == null)
            {
                ReleaseFocus();
                return;
            }
            if (rotationPanel != null) Destroy(rotationPanel);
            rotationPanel = null;
            socketState = FestekSocketState.Withdrawing;
            if (Application.isPlaying) StartCoroutine(Withdraw());
            else CompleteWithdrawal();
        }

        private IEnumerator Withdraw()
        {
            float initialTurn = turn;
            for (float t = 0f; t < 0.24f; t += Time.deltaTime)
            {
                SetTurn(Mathf.Lerp(initialTurn, 0f, Mathf.SmoothStep(0f, 1f, t / 0.24f)));
                yield return null;
            }
            SetTurn(0f);
            if (seatedFestek != null)
            {
                var device = seatedFestek.transform;
                Vector3 seated = device.localPosition;
                for (float t = 0f; t < 0.34f; t += Time.deltaTime)
                {
                    device.localPosition = seated - Vector3.forward * (0.14f * Mathf.SmoothStep(0f, 1f, t / 0.34f));
                    PoseHand(device.position, device.rotation);
                    yield return null;
                }
            }
            CompleteWithdrawal();
        }

        private void CompleteWithdrawal()
        {
            if (seatedFestek != null && seatedFestek.AttachToLeftHand(carrier))
            {
                seatedFestek = null;
                turn = 0f;
                notch = 0;
                socketState = FestekSocketState.Empty;
                ChapterObjectiveManager.Instance?.NotifyFestekWithdrawn();
            }
            else
            {
                SetTurn(0f);
                socketState = FestekSocketState.SeatedReadyToActivate;
            }
            ReleaseFocus();
        }

        public IEnumerator RecoverElevatorItem()
        {
            if (!elevatorSocket || !IsActivated || seatedFestek == null || carrier == null) yield break;
            if (Application.isPlaying)
            {
                yield return ApproachSocket(carrier);
                var device = seatedFestek.transform;
                Vector3 seated = device.localPosition;
                Play(socketInsertClip, 0.51f);
                for (float t = 0f; t < 0.46f; t += Time.deltaTime)
                {
                    device.localPosition = seated - Vector3.forward * (0.14f * Mathf.SmoothStep(0f, 1f, t / 0.46f));
                    PoseHand(device.position, device.rotation);
                    yield return null;
                }
            }
            if (seatedFestek.AttachToLeftHand(carrier)) seatedFestek = null;
            if (animation != null) animation.ClearPhysicalContact(PreferredInteractionHand.Left);
        }

        private IEnumerator ReleaseAfterLock()
        {
            for (float elapsed = 0f; elapsed < 0.32f; elapsed += Time.deltaTime)
            {
                if (seatedFestek != null) PoseHand(seatedFestek.transform.position, seatedFestek.transform.rotation, 1f - elapsed / 0.32f);
                yield return null;
            }
            ReleaseFocus();
        }

        public void ReleaseFocus()
        {
            if (rotationPanel != null) Destroy(rotationPanel);
            rotationPanel = null;
            if (socketState == FestekSocketState.Activating) socketState = FestekSocketState.SeatedReadyToActivate;
            if (animation != null)
            {
                animation.ClearPhysicalContact(PreferredInteractionHand.Left);
                animation.ClearLeftHandInteractTarget();
                animation.IsPosingFestek = false;
            }
            animation = null;
            focus?.End();
            focus = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (removing && seatedFestek != null) SetTurn(RequiredTurn);
            removing = false;
            if (socketState == FestekSocketState.Inserting) socketState = FestekSocketState.Empty;
            if (socketState == FestekSocketState.Withdrawing) CompleteWithdrawal();
            else ReleaseFocus();
        }

        private void Play(AudioClip clip, float gain)
        {
            if (Application.isPlaying && socketAudioSource != null && clip != null)
                socketAudioSource.PlayOneShot(clip, gain);
        }

        public void SeatForVerification(FestekItem item, GameObject player)
        {
            if (Application.isPlaying || item == null || item.CurrentState != FestekItemState.HeldInLeftHand ||
                socketState != FestekSocketState.Empty) return;
            ChapterObjectiveManager.Instance?.NotifyFestekInserting();
            Seat(item, player);
        }
    }
}
