using System;
using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    public enum FlashlightPhysicalState
    {
        HeldInRig,
        HeldInHandBone,
        HeldInLeftHandBone,
        DroppedPhysics
    }

    /// <summary>
    /// THE LAST BELL - Fiziksel El Feneri Etkileşim Mimarisi.
    /// Fenerin kamera/el soketi arasında geçiş yapmasını, sağ el özel bir nesne (Feştek) tuttuğunda
    /// sol ele (LeftHand_Flashlight_Socket) çakışmasız aktarılmasını ve düşme anında Rigidbody fiziğine
    /// geçerek yerde ışık saçmaya devam etmesini sağlar.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightPhysicalItem : MonoBehaviour, IInteractable
    {
        [Header("--- Durum & Bağlantı Noktaları ---")]
        [SerializeField] private FlashlightPhysicalState currentState = FlashlightPhysicalState.HeldInRig;
        [SerializeField] private Transform cameraRigAttachPoint;
        [SerializeField] private Transform handAttachPoint;
        [SerializeField] private Transform leftHandAttachPoint;
        [SerializeField] private FlashlightController flashlightController;
        [SerializeField] private ProtagonistAnimationController animController;

        [Header("--- Fizik Bileşenleri (Düşme Durumu) ---")]
        [SerializeField] private Rigidbody itemRigidbody;
        [SerializeField] private Collider itemCollider;

        [Header("--- Yerel Soket Ofsetleri ---")]
        [SerializeField] private Vector3 rigLocalPosition = new Vector3(0.18f, -0.16f, 0.16f);
        [SerializeField] private Vector3 handLocalPosition = Vector3.zero;
        [SerializeField] private Vector3 handLocalEuler = Vector3.zero;


        public FlashlightPhysicalState CurrentState => currentState;
        public bool IsTransferredToLeftHand => currentState == FlashlightPhysicalState.HeldInLeftHandBone;

        private AudioClip lostImpact;
        private bool lost;
        private float lastImpact;
        public bool IsLost => lost;

        public void LoseInElevator(AudioClip impact)
        {
            if (lost) return;
            lostImpact = impact;
            lost = true;
            if (currentState != FlashlightPhysicalState.DroppedPhysics)
                DropWithPhysics(transform.forward * 0.37f, transform.right * 0.38f);
            if (flashlightController != null) flashlightController.enabled = false;
        }

        private void OnCollisionEnter(Collision hit)
        {
            if (!lost || lostImpact == null || hit.contactCount == 0 || hit.relativeVelocity.sqrMagnitude < 0.36f || Time.time - lastImpact < 0.22f) return;
            lastImpact = Time.time;
            AudioSource.PlayClipAtPoint(lostImpact, hit.GetContact(0).point, Mathf.Clamp(hit.relativeVelocity.magnitude * 0.17f, 0.12f, 0.8f));
        }

        public event Action<FlashlightPhysicalState> OnPhysicalStateChanged;

        private void Awake()
        {
            if (flashlightController == null) flashlightController = GetComponent<FlashlightController>();
            if (cameraRigAttachPoint == null) cameraRigAttachPoint = transform.parent;
            if (animController == null && transform.root != null)
            {
                animController = transform.root.GetComponentInChildren<ProtagonistAnimationController>(true);
            }
            if (animController != null)
            {
                animController.InitializeIfNeeded();
                if (handAttachPoint == null) handAttachPoint = animController.RightHandSocket;
                if (leftHandAttachPoint == null) leftHandAttachPoint = animController.LeftHandFlashlightSocket;
            }

            if (itemRigidbody == null) itemRigidbody = GetComponent<Rigidbody>();
            if (itemCollider == null) itemCollider = GetComponent<Collider>();

            ApplyStateVisualAndPhysics(currentState);
        }

        private void OnEnable()
        {
            if (animController != null)
            {
                animController.OnFlashlightReleaseEvent += HandleAnimReleaseEvent;
                animController.OnFlashlightGripEvent += HandleAnimGripEvent;
            }
        }

        private void OnDisable()
        {
            if (animController != null)
            {
                animController.OnFlashlightReleaseEvent -= HandleAnimReleaseEvent;
                animController.OnFlashlightGripEvent -= HandleAnimGripEvent;
            }
        }

        public void AttachToCameraRig()
        {
            if (cameraRigAttachPoint != null)
            {
                transform.SetParent(cameraRigAttachPoint, false);
                transform.localPosition = rigLocalPosition;
                transform.localRotation = Quaternion.identity;
            }
            currentState = FlashlightPhysicalState.HeldInRig;
            ApplyStateVisualAndPhysics(currentState);
            animController?.SetLeftHandFlashlightHeld(false);
            animController?.SetFlashlightEquipped(true);
            OnPhysicalStateChanged?.Invoke(currentState);
        }

        public void AttachToHand()
        {
            if (handAttachPoint == null && animController != null)
            {
                animController.InitializeIfNeeded();
                handAttachPoint = animController.RightHandSocket;
            }
            if (handAttachPoint != null)
            {
                transform.SetParent(handAttachPoint, false);
                transform.localPosition = handLocalPosition;
                transform.localRotation = Quaternion.Euler(handLocalEuler);
            }
            currentState = FlashlightPhysicalState.HeldInHandBone;
            ApplyStateVisualAndPhysics(currentState);
            animController?.SetLeftHandFlashlightHeld(false);
            animController?.SetFlashlightEquipped(true);
            OnPhysicalStateChanged?.Invoke(currentState);
        }

        public void TransferToLeftHand()
        {
            if (currentState == FlashlightPhysicalState.DroppedPhysics) return;
            if (leftHandAttachPoint == null && animController != null)
            {
                animController.InitializeIfNeeded();
                leftHandAttachPoint = animController.LeftHandFlashlightSocket;
            }

            if (leftHandAttachPoint != null)
            {
                transform.SetParent(leftHandAttachPoint, false);
                transform.localPosition = handLocalPosition;
                transform.localRotation = Quaternion.Euler(handLocalEuler);
            }
            currentState = FlashlightPhysicalState.HeldInLeftHandBone;
            ApplyStateVisualAndPhysics(currentState);
            animController?.SetFlashlightEquipped(false);
            animController?.SetLeftHandFlashlightHeld(true);
            OnPhysicalStateChanged?.Invoke(currentState);
        }

        public void ReturnFromLeftHand()
        {
            if (currentState != FlashlightPhysicalState.HeldInLeftHandBone) return;
            animController?.SetLeftHandFlashlightHeld(false);
            if (handAttachPoint != null)
            {
                AttachToHand();
            }
            else
            {
                AttachToCameraRig();
            }
        }

        public void DropWithPhysics(Vector3 worldImpulse, Vector3 angularImpulse = default)
        {
            EnsurePhysicsComponents();
            transform.SetParent(null, true);
            currentState = FlashlightPhysicalState.DroppedPhysics;
            ApplyStateVisualAndPhysics(currentState);

            if (itemRigidbody != null)
            {
                itemRigidbody.linearVelocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
                itemRigidbody.AddForce(worldImpulse, ForceMode.Impulse);
                if (angularImpulse != Vector3.zero)
                {
                    itemRigidbody.AddTorque(angularImpulse, ForceMode.Impulse);
                }
            }

            // Fener yerdeyken de ışık saçmaya devam eder; sadece karakter el animasyonu bırakılır
            animController?.SetLeftHandFlashlightHeld(false);
            animController?.SetFlashlightEquipped(false);
            OnPhysicalStateChanged?.Invoke(currentState);
        }

        public void PickupAndReattach(bool attachToHandBone = false)
        {
            animController?.TriggerFlashlightPickup();
            if (animController != null && animController.IsRightHandHoldingCustomItem)
            {
                currentState = FlashlightPhysicalState.HeldInRig;
                TransferToLeftHand();
                return;
            }
            if (handAttachPoint != null)
            {
                AttachToHand();
            }
            else
            {
                AttachToCameraRig();
            }
        }

        private void EnsurePhysicsComponents()
        {
            if (itemCollider == null) itemCollider = GetComponent<Collider>();
            if (itemCollider == null)
            {
                var cap = gameObject.AddComponent<CapsuleCollider>();
                cap.radius = 0.04f;
                cap.height = 0.24f;
                cap.direction = 2; // Z axis
                itemCollider = cap;
            }

            if (itemRigidbody == null) itemRigidbody = GetComponent<Rigidbody>();
            if (itemRigidbody == null)
            {
                itemRigidbody = gameObject.AddComponent<Rigidbody>();
                itemRigidbody.mass = 0.35f;
                itemRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
        }

        private void ApplyStateVisualAndPhysics(FlashlightPhysicalState state)
        {
            bool isDropped = (state == FlashlightPhysicalState.DroppedPhysics);
            bool inHand = (state == FlashlightPhysicalState.HeldInHandBone || state == FlashlightPhysicalState.HeldInLeftHandBone);

            if (itemRigidbody != null)
            {
                itemRigidbody.isKinematic = !isDropped;
                itemRigidbody.useGravity = isDropped;
            }
            if (itemCollider != null)
            {
                itemCollider.enabled = isDropped;
            }
            if (flashlightController != null)
            {
                flashlightController.SetPhysicsDropped(isDropped);
                flashlightController.SetAttachedToHand(inHand);
            }
        }

        private void HandleAnimReleaseEvent()
        {
            if (currentState != FlashlightPhysicalState.DroppedPhysics)
            {
                DropWithPhysics(transform.forward * 1.5f + Vector3.up * 0.5f, Vector3.one * 0.8f);
            }
        }

        private void HandleAnimGripEvent()
        {
            if (lost) return;
            if (currentState == FlashlightPhysicalState.DroppedPhysics)
            {
                AttachToHand();
            }
        }

        #region IInteractable
        public string GetInteractionPrompt()
        {
            return "[E] - El Fenerini Al";
        }

        public bool CanInteract()
        {
            return !lost && currentState == FlashlightPhysicalState.DroppedPhysics;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract()) return;
            PickupAndReattach(attachToHandBone: false);
        }
        #endregion
    }
}
