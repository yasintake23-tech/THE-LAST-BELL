using System;
using UnityEngine;
using TheLastBell.Player;
using TheLastBell.Gameplay;

namespace TheLastBell.Interaction
{
    public enum FestekItemState
    {
        CradledInPanel = 0,
        DroppedPhysics = 1,
        HeldInRightHand = 2,
        SeatedInSocket = 3
    }

    /// <summary>
    /// THE LAST BELL - Özel "Feştek" Elektromekanik Köprü Cihazı.
    /// Panodan fiziksel yerçekimiyle düşer, zemin çarpışmasında metalik ses üretir,
    /// erişilemez konuma kaçarsa güvenli zemin noktasına kurtarılır, sağ ele alındığında
    /// el fenerini sol ele çakışmasız aktarır ve özel soketine hizalanarak yerleşir.
    /// </summary>
    [DisallowMultipleComponent]
    public class FestekItem : MonoBehaviour, IInteractable
    {
        [Header("--- Durum & Referans Noktaları ---")]
        [SerializeField] private FestekItemState currentState = FestekItemState.CradledInPanel;
        [SerializeField] private Transform gripPoint;
        [SerializeField] private Transform insertionAlignPoint;
        [SerializeField] private Transform tipReference;
        [SerializeField] private Transform buttonPoint;

        [Header("--- Fizik & Güvenlik Kurtarma ---")]
        [SerializeField] private Rigidbody itemRigidbody;
        [SerializeField] private BoxCollider itemCollider;
        [SerializeField] private Vector3 safeFloorRecoveryPoint = new Vector3(4.2f, 0.06f, 8.65f);

        [Header("--- Uzamsal 3D Sesler ---")]
        [SerializeField] private AudioSource itemAudioSource;
        [SerializeField] private AudioClip metalDropImpactClip;
        [SerializeField] private AudioClip pickupClip;

        private InteractionTarget pickupInteractionTarget;
        private float lastImpactAudioTime = -10f;
        private bool hasNotifiedFloorDiscovery = false;

        public FestekItemState CurrentState => currentState;
        public Transform GripPoint => gripPoint;
        public Transform InsertionAlignPoint => insertionAlignPoint;
        public Transform TipReference => tipReference;
        public Transform ButtonPoint => buttonPoint;

        public event Action<FestekItemState> OnStateChanged;

        private void Awake()
        {
            ResolveAnchorsAndPhysics();
            ApplyPhysicsForState(currentState);
        }

        public void Configure(AudioSource audioSrc, AudioClip impactSfx, AudioClip pickupSfx, Vector3 safeFloorPos)
        {
            itemAudioSource = audioSrc;
            metalDropImpactClip = impactSfx;
            pickupClip = pickupSfx;
            safeFloorRecoveryPoint = safeFloorPos;
            ResolveAnchorsAndPhysics();
        }

        public void ResolveAnchorsAndPhysics()
        {
            if (gripPoint == null) gripPoint = FindDeepChild(transform, "Grip_Point");
            if (insertionAlignPoint == null) insertionAlignPoint = FindDeepChild(transform, "Insertion_Align_Point");
            if (tipReference == null) tipReference = FindDeepChild(transform, "Tip_Reference");
            if (buttonPoint == null) buttonPoint = FindDeepChild(transform, "Button_Point");

            if (itemCollider == null) itemCollider = GetComponent<BoxCollider>();
            if (itemCollider == null)
            {
                itemCollider = gameObject.AddComponent<BoxCollider>();
                itemCollider.size = new Vector3(0.052f, 0.044f, 0.215f);
                itemCollider.center = Vector3.zero;
            }

            if (itemRigidbody == null) itemRigidbody = GetComponent<Rigidbody>();
            if (itemRigidbody == null)
            {
                itemRigidbody = gameObject.AddComponent<Rigidbody>();
                itemRigidbody.mass = 0.68f;
                itemRigidbody.linearDamping = 0.35f;
                itemRigidbody.angularDamping = 0.65f;
                itemRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (pickupInteractionTarget == null)
            {
                pickupInteractionTarget = GetComponent<InteractionTarget>();
                if (pickupInteractionTarget == null) pickupInteractionTarget = gameObject.AddComponent<InteractionTarget>();
            }
            pickupInteractionTarget.Configure(gripPoint != null ? gripPoint : transform, PreferredInteractionHand.Right, "PickUp", 0.75f, 0.30f, 0.70f);
        }

        private void Update()
        {
            if (currentState == FestekItemState.DroppedPhysics)
            {
                EnforceReachableSafetyBounds();
            }
        }

        public void EnforceReachableSafetyBounds()
        {
            Vector3 pos = transform.position;
            float horizDist = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(safeFloorRecoveryPoint.x, safeFloorRecoveryPoint.z));
            if (pos.y < safeFloorRecoveryPoint.y - 0.22f || horizDist > 2.6f || float.IsNaN(pos.y))
            {
                if (itemRigidbody != null)
                {
                    itemRigidbody.linearVelocity = Vector3.zero;
                    itemRigidbody.angularVelocity = Vector3.zero;
                }
                transform.position = safeFloorRecoveryPoint + Vector3.up * 0.035f;
                transform.rotation = Quaternion.Euler(0f, 34f, 0f);
            }
        }

        public void ReleaseFromPanel(Vector3 worldImpulse, Vector3 angularImpulse, Vector3 safeFloorPos)
        {
            if (currentState != FestekItemState.CradledInPanel) return;

            safeFloorRecoveryPoint = safeFloorPos;
            ResolveAnchorsAndPhysics();

            transform.SetParent(null, true);
            currentState = FestekItemState.DroppedPhysics;
            ApplyPhysicsForState(currentState);

            if (itemRigidbody != null)
            {
                itemRigidbody.linearVelocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
                itemRigidbody.AddForce(worldImpulse, ForceMode.Impulse);
                itemRigidbody.AddTorque(angularImpulse, ForceMode.Impulse);
            }

            OnStateChanged?.Invoke(currentState);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (currentState != FestekItemState.DroppedPhysics) return;

            if (!hasNotifiedFloorDiscovery)
            {
                hasNotifiedFloorDiscovery = true;
                ChapterObjectiveManager.Instance?.NotifyFestekDiscoveredOnFloor();
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed > 0.35f && Time.time - lastImpactAudioTime > 0.14f)
            {
                lastImpactAudioTime = Time.time;
                if (itemAudioSource != null && metalDropImpactClip != null)
                {
                    float vol = Mathf.Clamp01(speed / 3.8f) * 0.90f;
                    itemAudioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                    itemAudioSource.PlayOneShot(metalDropImpactClip, Mathf.Max(0.28f, vol));
                }
            }
        }

        public void PickupIntoRightHand(GameObject playerRoot)
        {
            if (currentState != FestekItemState.DroppedPhysics || playerRoot == null) return;

            var animCtrl = playerRoot.GetComponentInChildren<ProtagonistAnimationController>(true);
            var procCtrl = playerRoot.GetComponentInChildren<CharacterProceduralController>(true);
            var flashlightPhys = playerRoot.GetComponentInChildren<FlashlightPhysicalItem>(true);

            animCtrl?.InitializeIfNeeded();

            // Sağ el Feştek tutacağı için aktif feneri sol ele çakışmasız aktar!
            if (flashlightPhys != null && flashlightPhys.CurrentState != FlashlightPhysicalState.DroppedPhysics)
            {
                flashlightPhys.TransferToLeftHand();
            }

            if (procCtrl != null && pickupInteractionTarget != null)
            {
                procCtrl.BeginEnvironmentalInteraction(pickupInteractionTarget);
            }

            Transform rightFestekSock = animCtrl != null ? animCtrl.RightHandFestekSocket : null;
            if (rightFestekSock != null)
            {
                transform.SetParent(rightFestekSock, false);
                // Grip_Point avuç içine tam oturacak ve uç (+Z) ileri bakacak şekilde hizala
                Vector3 gripOffset = gripPoint != null ? gripPoint.localPosition : Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localPosition = -gripOffset;
            }

            currentState = FestekItemState.HeldInRightHand;
            ApplyPhysicsForState(currentState);
            animCtrl?.SetRightHandCustomItemHeld(true);

            if (itemAudioSource != null && pickupClip != null)
            {
                itemAudioSource.pitch = 1.0f;
                itemAudioSource.PlayOneShot(pickupClip, 0.85f);
            }

            ChapterObjectiveManager.Instance?.NotifyFestekPickedUp();
            OnStateChanged?.Invoke(currentState);
        }

        public void SeatIntoSocket(Transform socketInsertionTransform, GameObject playerRoot)
        {
            if (socketInsertionTransform != null)
            {
                transform.SetParent(socketInsertionTransform, false);
                Vector3 alignOffset = insertionAlignPoint != null ? insertionAlignPoint.localPosition : Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localPosition = -alignOffset;
            }

            currentState = FestekItemState.SeatedInSocket;
            ApplyPhysicsForState(currentState);

            if (playerRoot != null)
            {
                var animCtrl = playerRoot.GetComponentInChildren<ProtagonistAnimationController>(true);
                animCtrl?.SetRightHandCustomItemHeld(false);
            }

            ChapterObjectiveManager.Instance?.NotifyFestekSeated();
            OnStateChanged?.Invoke(currentState);
        }

        private void ApplyPhysicsForState(FestekItemState state)
        {
            bool physicsActive = (state == FestekItemState.DroppedPhysics);
            if (itemRigidbody != null)
            {
                itemRigidbody.isKinematic = !physicsActive;
                itemRigidbody.useGravity = physicsActive;
            }
            if (itemCollider != null)
            {
                // Yerdeyken hem fiziksel çarpışma hem de IInteractable raycast algılaması için açık tut
                itemCollider.enabled = physicsActive;
            }
        }

        public string GetInteractionPrompt()
        {
            return currentState == FestekItemState.DroppedPhysics ? "[E] - Feştek Parçasını Al" : "";
        }

        public bool CanInteract()
        {
            return currentState == FestekItemState.DroppedPhysics;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract() || interactor == null) return;
            PickupIntoRightHand(interactor.gameObject);
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
