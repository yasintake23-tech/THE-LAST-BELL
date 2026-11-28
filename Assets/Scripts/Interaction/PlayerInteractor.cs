using System;
using UnityEngine;
using TheLastBell.Player;
using TheLastBell.Gameplay;

namespace TheLastBell.Interaction
{
    /// <summary>
    /// THE LAST BELL - Oyuncu Etkileim Denetleyicisi (Player Interactor).
    /// Kamerann / karakterin bakt dorultuda Raycast atarak etkileime geilebilir nesneleri (IInteractable) tespit eder.
    /// Bilgisayarda 'E' tuu veya Android'de dokunmatik etkileim butonu ile tetiklenir.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("--- Mesafe ve Alglama ---")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float maxInteractionDistance = 3.2f;
        [SerializeField] private LayerMask interactionLayers = ~0;

        [Header("--- Gecikme & Gvenlik ---")]
        [SerializeField] private float interactionCooldown = 0.25f;

        private IInteractable currentInteractable = null;
        private float lastInteractTime = float.NegativeInfinity;
        private string currentPrompt;
        private readonly RaycastHit[] rayHits = new RaycastHit[24];
        private readonly Collider[] nearbyHits = new Collider[48];
        private TPSController tps;
        private FirstPersonController fpc;

        // Olaylar (UI / HUD gncellemesi iin)
        public event Action<string> OnPromptUpdated;
        public event Action<string> OnStatusMessageTriggered;

        public IInteractable CurrentInteractable => currentInteractable;

        private void Awake()
        {
            tps = GetComponent<TPSController>();
            fpc = GetComponent<FirstPersonController>();
            if (playerCamera == null && Camera.main != null)
            {
                playerCamera = Camera.main.transform;
            }
        }

        private void Update()
        {
            CheckForInteractable();

            if (Input.GetKeyDown(KeyCode.E))
            {
                TriggerInteraction();
            }
        }

        private void CheckForInteractable()
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 forward = transform.forward;

            if (playerCamera != null)
            {
                origin = playerCamera.position;
                forward = playerCamera.forward;
            }

            IInteractable target = FindAlongRay(origin, forward, maxInteractionDistance, out _);
            if (!CanOffer(target)) target = FindNearbyInteractable();

            if (Time.time - lastInteractTime < interactionCooldown) target = null;
            currentInteractable = target;
            string nextPrompt = target != null ? target.GetInteractionPrompt() : null;
            if (string.IsNullOrEmpty(nextPrompt)) nextPrompt = null;
            if (nextPrompt == currentPrompt) return;
            currentPrompt = nextPrompt;
            OnPromptUpdated?.Invoke(nextPrompt);
        }

        private bool CanOffer(IInteractable target)
        {
            if (!(target is Component behaviour) || behaviour == null || !target.CanInteract()) return false;
            var profile = behaviour.GetComponent<InteractionTarget>();
            return profile == null || InteractionApproach.CanAcquire(gameObject, profile);
        }

        private IInteractable FindAlongRay(Vector3 origin, Vector3 direction, float distance, out bool blocked)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, rayHits, distance, interactionLayers, QueryTriggerInteraction.Collide);
            float nearest = float.PositiveInfinity;
            IInteractable candidate = null;
            blocked = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = rayHits[i];
                if (hit.distance >= nearest || hit.collider.transform.IsChildOf(transform)) continue;
                var interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (hit.collider.isTrigger && interactable == null) continue;
                nearest = hit.distance;
                candidate = interactable;
                blocked = candidate == null;
            }
            return candidate;
        }

        private IInteractable FindNearbyInteractable()
        {
            Vector3 eye = transform.position + Vector3.up * 1.15f;
            int count = Physics.OverlapSphereNonAlloc(eye + transform.forward * 0.58f, 2.05f, nearbyHits,
                interactionLayers, QueryTriggerInteraction.Collide);
            float nearest = float.PositiveInfinity;
            IInteractable result = null;
            for (int i = 0; i < count; i++)
            {
                Collider hit = nearbyHits[i];
                if (hit.transform.IsChildOf(transform)) continue;
                var candidate = hit.GetComponentInParent<IInteractable>();
                if (!CanOffer(candidate)) continue;

                Vector3 offset = hit.bounds.ClosestPoint(eye) - eye;
                float distance = offset.magnitude;
                if (distance >= nearest) continue;
                if (distance > 0.01f)
                {
                    Vector3 direction = offset / distance;
                    Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
                    if (flat.sqrMagnitude > 0.16f && Vector3.Dot(transform.forward, flat.normalized) < -0.15f) continue;
                    var visible = FindAlongRay(eye, direction, distance + 0.03f, out bool blocked);
                    if (blocked || (visible != null && visible != candidate)) continue;
                }
                nearest = distance;
                result = candidate;
            }
            return result;
        }

        public void TriggerInteraction()
        {
            if (!isActiveAndEnabled) return;
            // Reserve the target represented by the visible prompt instead of raycasting a different one on tap.
            var selected = currentInteractable;
            if (Time.time - lastInteractTime < interactionCooldown || selected == null || string.IsNullOrEmpty(currentPrompt)) return;
            if (!(selected is Component component) || component == null || !selected.CanInteract())
            {
                CheckForInteractable();
                ShowStatusMessage("Bu etkileşim artık kullanılamıyor.");
                return;
            }

            lastInteractTime = Time.time;
            // DoorInteractable owns the handle animation and starts its own physical motion.
            if (!(component.GetComponent<InteractionTarget>() != null || selected is TelevisionScreening))
            {
                if (tps != null)
                {
                    if (selected is ItemPickupInteractable) tps.PlayPickupAnimation();
                    else tps.PlayInteractAnimation();
                }
                else if (fpc != null)
                {
                    if (selected is ItemPickupInteractable) fpc.PlayPickupAnimation();
                    else fpc.PlayInteractAnimation();
                }
            }

            currentInteractable = null;
            currentPrompt = null;
            OnPromptUpdated?.Invoke(null);
            selected.Interact(this);
            if (isActiveAndEnabled) CheckForInteractable();
        }

        public void ShowStatusMessage(string message)
        {
            OnStatusMessageTriggered?.Invoke(message);
        }
    }
}
