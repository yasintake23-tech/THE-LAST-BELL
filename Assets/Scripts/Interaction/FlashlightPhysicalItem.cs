using System;
using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    public enum FlashlightPhysicalState
    {
        HeldInRig,
        HeldInHandBone,
        DroppedPhysics
    }

    /// <summary>
    /// THE LAST BELL - Fiziksel El Feneri Etkileşim Mimarisi.
    /// Fenerin kamera/el soketi arasında geçiş yapmasını, düşme/olay anında Rigidbody fiziğine
    /// geçerek yerde ışık saçmaya devam etmesini ve tekrar yerden alınıp ele takılmasını sağlar.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlashlightPhysicalItem : MonoBehaviour, IInteractable
    {
        [Header("--- Durum & Bağlantı Noktaları ---")]
        [SerializeField] private FlashlightPhysicalState currentState = FlashlightPhysicalState.HeldInRig;
        [SerializeField] private Transform cameraRigAttachPoint;
        [SerializeField] private Transform handAttachPoint;
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

        public event Action<FlashlightPhysicalState> OnPhysicalStateChanged;

        private void Awake()
        {
            if (flashlightController == null) flashlightController = GetComponent<FlashlightController>();
            if (cameraRigAttachPoint == null) cameraRigAttachPoint = transform.parent;
            if (animController == null && transform.root != null)
            {
                animController = transform.root.GetComponentInChildren<ProtagonistAnimationController>(true);
            }
            if (handAttachPoint == null && animController != null)
            {
                animController.InitializeIfNeeded();
                handAttachPoint = animController.RightHandSocket;
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
            animController?.SetFlashlightEquipped(true);
            OnPhysicalStateChanged?.Invoke(currentState);
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
            animController?.SetFlashlightEquipped(false);
            OnPhysicalStateChanged?.Invoke(currentState);
        }

        public void PickupAndReattach(bool attachToHandBone = false)
        {
            animController?.TriggerFlashlightPickup();
            bool useHand = attachToHandBone || (CameraModeController.Instance != null && CameraModeController.Instance.IsThirdPerson);
            if (useHand && handAttachPoint != null)
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
            bool inHand = (state == FlashlightPhysicalState.HeldInHandBone);

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
            return currentState == FlashlightPhysicalState.DroppedPhysics;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract()) return;
            PickupAndReattach(attachToHandBone: false);
        }
        #endregion
    }
}
