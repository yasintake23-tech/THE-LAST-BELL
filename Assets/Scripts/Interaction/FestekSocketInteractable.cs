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
        Activated
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

        private FestekItem seatedFestek;
        private InteractionFocus focus;
        private ProtagonistAnimationController animation;
        private Transform wrist;
        private GameObject rotationPanel;
        private Vector3 itemToHandOffset;
        private Quaternion itemToHandRotation;
        private float turn;
        private int notch;
        public FestekSocketState SocketState => socketState;
        public bool IsActivated => socketState == FestekSocketState.Activated;
        public FestekItem SeatedFestek => seatedFestek;
        public float RotationProgress => turn / RequiredTurn;
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

        public string GetInteractionPrompt() => socketState == FestekSocketState.SeatedReadyToActivate
            ? "[E] - Feştek’i çevir" : socketState == FestekSocketState.Empty ? "[E] - Bağlantı yuvası" : "";

        public bool CanInteract() => socketState == FestekSocketState.Empty || socketState == FestekSocketState.SeatedReadyToActivate;

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
                interactor.ShowStatusMessage("Feştek’i takmak için yuvaya biraz daha yaklaş.");
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
                interactor.ShowStatusMessage("Yuvaya uygun metal bir bağlantı parçası gerekiyor.");
                return;
            }

            if (!BeginFocus(player, controller)) return;
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

        private IEnumerator Insert(FestekItem item, GameObject player)
        {
            socketState = FestekSocketState.Inserting;
            ChapterObjectiveManager.Instance?.NotifyFestekInserting();
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
            yield return new WaitForEndOfFrame();
            Seat(item, player);
            Play(socketInsertClip, 0.85f);
            OpenRotationPanel();
        }

        private void Seat(FestekItem item, GameObject player)
        {
            seatedFestek = item;
            item.SeatIntoSocket(insertionTransform, player);
            socketState = FestekSocketState.SeatedReadyToActivate;
        }

        private void PoseHand(Vector3 itemPosition, Quaternion itemRotation)
        {
            if (animation == null || wrist == null) return;
            wrist.SetPositionAndRotation(itemPosition + itemRotation * itemToHandOffset, itemRotation * itemToHandRotation);
            animation.SetLeftHandInteractionIK(wrist, 1f);
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

            turn = Mathf.Clamp(turn + degrees, 0f, RequiredTurn);
            var device = seatedFestek.transform;
            Vector3 anchor = seatedFestek.InsertionAlignPoint != null
                ? device.InverseTransformPoint(seatedFestek.InsertionAlignPoint.position) : Vector3.zero;
            device.localRotation = Quaternion.AngleAxis(-turn, Vector3.forward);
            device.localPosition = -(device.localRotation * Vector3.Scale(anchor, device.localScale));
            PoseHand(device.position, device.rotation);

            int detent = Mathf.FloorToInt(turn / 22.5f);
            if (detent > notch && detent < 4) Play(buttonClickClip, 0.23f);
            notch = detent;
            if (turn < RequiredTurn) return;

            socketState = FestekSocketState.Activated;
            Play(buttonClickClip, 0.92f);
            if (statusLensRenderer != null)
            {
                var properties = new MaterialPropertyBlock();
                statusLensRenderer.GetPropertyBlock(properties);
                properties.SetColor("_Color", new Color(0.32f, 0.78f, 0.36f));
                properties.SetColor("_EmissionColor", new Color(0.26f, 0.68f, 0.28f));
                statusLensRenderer.SetPropertyBlock(properties);
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

        private IEnumerator ReleaseAfterLock()
        {
            for (float elapsed = 0f; elapsed < 0.32f; elapsed += Time.deltaTime)
            {
                animation?.SetLeftHandInteractionIK(wrist, 1f - elapsed / 0.32f);
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
            if (socketState == FestekSocketState.Inserting) socketState = FestekSocketState.Empty;
            ReleaseFocus();
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
