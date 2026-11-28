using System;
using System.Collections;
using UnityEngine;
using TheLastBell.Player;
using TheLastBell.Gameplay;

namespace TheLastBell.Interaction
{
    public enum FestekItemState
    {
        CradledInPanel = 0,
        DroppedPhysics = 1,
        HeldInLeftHand = 2,
        SeatedInSocket = 3,
        Stored = 4
    }

    /// <summary>
    /// THE LAST BELL - Özel "Feştek" Elektromekanik Köprü Cihazı.
    /// Panodan fiziksel yerçekimiyle düşer, zemin çarpışmasında metalik ses üretir,
    /// erişilemez konuma kaçarsa güvenli zemine döner ve sol elde taşınır.
    /// </summary>
    [DisallowMultipleComponent]
    public class FestekItem : MonoBehaviour, IInteractable, IInventoryPhysicalItem
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
        private bool hasNotifiedFloorDiscovery;
        private bool pickingUp;
        private float droppedAt;
        private PhysicalContact pickupContact;
        private FirstPersonController recoveryActor;
        private bool sweptRestingFloor;
        [SerializeField] private InventoryItemDefinition inventoryDefinition;
        public void ConfigureInventoryItem(InventoryItemDefinition definition) => inventoryDefinition = definition;
        private Renderer highlightRenderer;
        private MaterialPropertyBlock highlight;
        public bool HasLanded => hasNotifiedFloorDiscovery;

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
            highlightRenderer = GetComponentInChildren<MeshRenderer>();
            highlight = new MaterialPropertyBlock();
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
            pickupInteractionTarget.Configure(gripPoint != null ? gripPoint : transform, PreferredInteractionHand.Left, "PickUp", 0.75f, 0.30f, 0.70f);
            pickupInteractionTarget.ConfigurePhysicalContact(2.10f, 0.39f, HandGripProfile.ForDevice(DeviceGripAction.Pickup));
            pickupInteractionTarget.ConfigureApproach(null, null, posture: RequiredInteractionPosture.Crouching);
        }

        private void Update()
        {
            if (currentState == FestekItemState.DroppedPhysics)
            {
                if (!pickingUp) EnforceReachableSafetyBounds();
                if (!hasNotifiedFloorDiscovery && Time.time - droppedAt > 1.4f &&
                    itemRigidbody != null && itemRigidbody.linearVelocity.sqrMagnitude < 0.08f)
                    NotifyFloorDiscovery();
                if (!pickingUp && !sweptRestingFloor && hasNotifiedFloorDiscovery && Time.time - droppedAt > 2.0f &&
                    itemRigidbody != null && itemRigidbody.linearVelocity.sqrMagnitude < 0.035f)
                {
                    sweptRestingFloor = true;
                    if (recoveryActor == null) recoveryActor = FindAnyObjectByType<FirstPersonController>();
                    Vector3 recoveryStand = new Vector3(safeFloorRecoveryPoint.x, 0.025f, safeFloorRecoveryPoint.z - 0.60f);
                    if (recoveryActor != null && Vector3.Distance(transform.position, safeFloorRecoveryPoint) > 0.32f &&
                        !InteractionApproach.CanApproachFrom(recoveryActor.gameObject, pickupInteractionTarget, recoveryStand))
                        RecoverToOpenFloor();
                }
                SetHighlight(hasNotifiedFloorDiscovery && !pickingUp
                    ? 0.33f + 0.18f * Mathf.Sin(Time.time * 3f) : 0f);
            }
        }

        public void EnforceReachableSafetyBounds()
        {
            Vector3 pos = transform.position;
            float horizDist = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(safeFloorRecoveryPoint.x, safeFloorRecoveryPoint.z));
            if (pos.y < safeFloorRecoveryPoint.y - 0.22f || horizDist > 2.6f || float.IsNaN(pos.y) ||
                (Application.isPlaying && Time.time - droppedAt > 2.8f && pos.y > safeFloorRecoveryPoint.y + 0.4f))
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
            droppedAt = Time.time;
            sweptRestingFloor = false;
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
                bool floorContact = false;
                for (int i = 0; i < collision.contactCount; i++)
                    floorContact |= collision.GetContact(i).normal.y > 0.55f;
                if (floorContact) NotifyFloorDiscovery();
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

        private void NotifyFloorDiscovery()
        {
            if (hasNotifiedFloorDiscovery) return;
            hasNotifiedFloorDiscovery = true;
            ChapterObjectiveManager.Instance?.NotifyFestekDiscoveredOnFloor();
        }

        private void SetHighlight(float intensity)
        {
            if (highlightRenderer == null || highlight == null) return;
            highlightRenderer.GetPropertyBlock(highlight);
            highlight.SetColor("_EmissionColor", new Color(0.78f, 0.55f, 0.26f) * intensity);
            highlightRenderer.SetPropertyBlock(highlight);
        }

        public void PickupIntoLeftHand(GameObject playerRoot)
        {
            if (currentState != FestekItemState.DroppedPhysics || playerRoot == null) return;
            if (!AttachToLeftHand(playerRoot)) return;
            var inventory = playerRoot.GetComponent<PlayerInventory>();
            if (inventoryDefinition != null && inventory != null)
                inventory.TryCollectEquipped(inventoryDefinition, this);

            if (Application.isPlaying && itemAudioSource != null && pickupClip != null)
            {
                itemAudioSource.pitch = 1f;
                itemAudioSource.PlayOneShot(pickupClip, 0.85f);
            }
            ChapterObjectiveManager.Instance?.NotifyFestekPickedUp();
            OnStateChanged?.Invoke(currentState);
        }

        public bool TryEquip(GameObject player)
        {
            if (!AttachToLeftHand(player)) return false;
            gameObject.SetActive(true);
            return true;
        }

        public void StoreAt(Transform pocket)
        {
            currentState = FestekItemState.Stored;
            ApplyPhysicsForState(currentState);
            transform.SetParent(pocket, true);
            gameObject.SetActive(false);
            OnStateChanged?.Invoke(currentState);
        }

        public bool AttachToLeftHand(GameObject playerRoot)
        {
            if (playerRoot == null) return false;
            var animation = playerRoot.GetComponentInChildren<ProtagonistAnimationController>(true);
            animation?.InitializeIfNeeded();
            var socket = animation != null ? animation.LeftHandFestekSocket : null;
            if (socket == null) return false;

            Vector3 gripOffset = gripPoint != null ? transform.InverseTransformPoint(gripPoint.position) : Vector3.zero;
            transform.SetParent(socket, true);
            transform.localRotation = Quaternion.identity;
            transform.localPosition = -Vector3.Scale(gripOffset, transform.localScale);
            currentState = FestekItemState.HeldInLeftHand;
            ApplyPhysicsForState(currentState);
            SetHighlight(0f);
            animation.IsHoldingFestek = true;
            return true;
        }

        private IEnumerator ReachAndLift(GameObject player)
        {
            while (pickupContact != null && !pickupContact.Ready && !pickupContact.Failed)
            {
                pickupContact.Step(Time.deltaTime);
                yield return null;
            }
            if (pickupContact == null) yield break;
            if (pickupContact.Ready) PickupIntoLeftHand(player);
            else
            {
                // Physics can leave the object under a cabinet or up against a wall.
                // Keep the item in the puzzle by falling again at the authored open floor point.
                if (Vector3.Distance(transform.position, safeFloorRecoveryPoint) > 0.28f) RecoverToOpenFloor();
                player.GetComponent<PlayerInteractor>()?.ShowStatusMessage(PhysicalContact.ObstructionMessage);
            }
            FinishReach();
        }

        private void RecoverToOpenFloor()
        {
            Vector3 safe = safeFloorRecoveryPoint + Vector3.up * 0.12f;
            if (Physics.Raycast(safe + Vector3.up * 0.65f, Vector3.down, out var floor, 1.2f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) && floor.normal.y > 0.7f)
                safe.y = floor.point.y + 0.085f;
            itemRigidbody.linearVelocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            itemRigidbody.position = safe;
            itemRigidbody.rotation = Quaternion.Euler(0f, 34f, 0f);
            itemRigidbody.WakeUp();
        }

        private void FinishReach()
        {
            pickupContact?.End();
            pickupContact = null;
            pickingUp = false;
        }

        private void OnDisable()
        {
            if (pickingUp)
            {
                StopAllCoroutines();
                FinishReach();
            }
        }

        public void SeatIntoSocket(Transform socketInsertionTransform, GameObject playerRoot, bool notify = true)
        {
            if (socketInsertionTransform != null)
            {
                Vector3 alignOffset = insertionAlignPoint != null ? transform.InverseTransformPoint(insertionAlignPoint.position) : Vector3.zero;
                transform.SetParent(socketInsertionTransform, true);
                transform.localRotation = Quaternion.identity;
                transform.localPosition = -Vector3.Scale(alignOffset, transform.localScale);
            }

            currentState = FestekItemState.SeatedInSocket;
            ApplyPhysicsForState(currentState);

            if (playerRoot != null)
            {
                var animCtrl = playerRoot.GetComponentInChildren<ProtagonistAnimationController>(true);
                if (animCtrl != null) animCtrl.IsHoldingFestek = false;
            }

            if (notify) ChapterObjectiveManager.Instance?.NotifyFestekSeated();
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
            return currentState == FestekItemState.DroppedPhysics && hasNotifiedFloorDiscovery && !pickingUp;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract() || interactor == null) return;
            pickupContact = PhysicalContact.TryBegin(interactor.gameObject, pickupInteractionTarget, this, transform);
            if (pickupContact == null)
            {
                interactor.ShowStatusMessage(PhysicalContact.ObstructionMessage);
                return;
            }
            pickingUp = true;
            StartCoroutine(ReachAndLift(interactor.gameObject));
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
