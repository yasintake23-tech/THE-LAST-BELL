using System.Collections.Generic;
using UnityEngine;
using TheLastBell.Interaction;
using TheLastBell.UI;

namespace TheLastBell.Player
{
    public sealed class InteractionFocus : MonoBehaviour
    {
        private FirstPersonController movement;
        private PlayerInteractor interactor;
        private Transform subject;
        private Camera gameplayCamera;
        private CameraModeController cameraMode;
        private CameraViewMode previousMode;
        private Component owner;
        private bool movementWasAllowed;
        private bool interactionWasEnabled;
        private bool wasFrozen;
        private bool steerGaze;
        private bool fixedHeading;
        private float heading;
        private string diagnosticTarget, diagnosticPhase, failureReason;
        private Vector3 reservedStand;
        private float contactWeight;
        private bool contactObserved;
        private readonly Queue<string> transitions = new Queue<string>();
        public static bool LogDiagnostics { get; set; }
        public string DiagnosticSummary => string.IsNullOrEmpty(diagnosticTarget) ? "Etkileşim: bekliyor" :
            $"{diagnosticTarget} | {diagnosticPhase} | {failureReason}\n" +
            $"Duruş {reservedStand:F2} • {Vector3.ProjectOnPlane(reservedStand - transform.position, Vector3.up).magnitude:F2} m • " +
            $"yön farkı {HeadingError:F1}° • kol IK {contactWeight:F2} • temas {(contactObserved ? "var" : "yok")}\n" +
            $"Hareket: {(IsFocused ? "Etkileşim" : "Oyuncu")} • Kamera: {GetComponent<CameraModeController>()?.CurrentMode.ToString() ?? "?"}";
        public float HeadingError => Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, heading));
        public bool IsFocused => owner != null;
        public bool IsOwnedBy(Component requester) => requester != null && owner == requester;

        public static InteractionFocus Begin(GameObject player, Transform target, Component requester, bool steerGaze = true, bool reserveHeading = false)
        {
            if (player == null || requester == null) return null;
            var focus = player.GetComponent<InteractionFocus>();
            if (focus == null) focus = player.AddComponent<InteractionFocus>();
            if (focus.IsFocused) return null;
            focus.owner = requester;
            focus.steerGaze = steerGaze;
            focus.subject = target;
            focus.fixedHeading = reserveHeading;
            focus.heading = player.transform.eulerAngles.y;
            focus.diagnosticTarget = requester.name;
            focus.failureReason = "";
            focus.contactWeight = 0f;
            focus.contactObserved = false;
            focus.reservedStand = player.transform.position;
            focus.Report("Hedef ayrıldı");
            focus.gameplayCamera = Camera.main;
            focus.movement = player.GetComponent<FirstPersonController>();
            focus.interactor = player.GetComponent<PlayerInteractor>();
            if (focus.movement != null)
            {
                focus.movementWasAllowed = focus.movement.AllowMovement;
                focus.movement.AllowMovement = false;
                focus.wasFrozen = focus.movement.FreezeGazeForInspection;
                focus.movement.FreezeGazeForInspection = true;
                focus.ClearInput();
                if (reserveHeading) focus.movement.SetInteractionHeading(true, focus.heading, focus.movement.DesiredLookPitch);
            }
            if (focus.interactor != null)
            {
                focus.interactionWasEnabled = focus.interactor.enabled;
                focus.interactor.enabled = false;
            }
            focus.cameraMode = Application.isPlaying && steerGaze ? player.GetComponent<CameraModeController>() : null;
            if (focus.cameraMode != null)
            {
                focus.previousMode = focus.cameraMode.CurrentMode;
                focus.cameraMode.SetCameraMode(CameraViewMode.FirstPerson);
                if (focus.movement != null) focus.movement.FreezeGazeForInspection = true;
            }
            MobileControlManager.Instance?.SetInteractionFocus(true);
            return focus;
        }

        private void Update()
        {
            if (subject == null || owner == null)
            {
                if (movement != null || interactor != null) End();
                return;
            }

            if (movement == null || !steerGaze) return;
            Vector3 eye = gameplayCamera != null ? gameplayCamera.transform.position : movement.transform.position + Vector3.up * 1.6f;
            Vector3 offset = subject.position - eye;
            if (fixedHeading)
            {
                // The animated eye drives pitch only. Its orbit around the capsule cannot feed back into body yaw.
                float radius = Vector3.ProjectOnPlane(subject.position - transform.position, Vector3.up).magnitude;
                float pitch = Mathf.Atan2(-offset.y, Mathf.Max(0.19f, radius)) * Mathf.Rad2Deg;
                movement.SetInteractionHeading(true, heading, pitch);
                return;
            }
            float yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float gazePitch = Mathf.Atan2(-offset.y, new Vector2(offset.x, offset.z).magnitude) * Mathf.Rad2Deg;
            movement.SetScriptedGazeOverride(true, yaw, gazePitch, 0.35f);
        }

        public void AlignOnce(Vector3 contact)
        {
            if (owner == null || movement == null) return;
            Vector3 offset = Vector3.ProjectOnPlane(contact - transform.position, Vector3.up);
            if (offset.sqrMagnitude > 0.20f * 0.20f)
                heading = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            fixedHeading = true;
            movement.SetScriptedGazeOverride(false, 0f, 0f);
            movement.SetInteractionHeading(true, heading, movement.DesiredLookPitch);
        }

        public void Report(string phase, string failure = null)
        {
            if (phase == diagnosticPhase && failure == null) return;
            diagnosticPhase = phase;
            if (failure != null) failureReason = failure;
            string message = $"{Time.time:F2} {diagnosticTarget}: {phase}" +
                (failure != null ? $" ({failure})" : "");
            if (transitions.Count == 6) transitions.Dequeue();
            transitions.Enqueue(message);
            if (LogDiagnostics) Debug.Log("[Interaction] " + message);
        }

        public string RecentTransitions => string.Join("\n", transitions);
        public void ReportStand(Vector3 stand) => reservedStand = stand;
        public void ReportContact(float weight, bool observed)
        {
            contactWeight = weight;
            contactObserved |= observed;
        }

        public void MoveTowards(Vector3 destination, float speed, float deltaTime)
        {
            if (owner == null || movement == null) return;
            Vector3 offset = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            movement.MoveForInteraction(Vector3.ClampMagnitude(offset, speed * deltaTime), deltaTime);
        }

        public void Follow(Transform target)
        {
            if (owner == null || target == null) return;
            subject = target;
            if (fixedHeading) AlignOnce(target.position);
        }

        public void End()
        {
            owner = null;
            subject = null;
            if (movement != null)
            {
                ClearInput();
                movement.SetScriptedGazeOverride(false, 0f, 0f);
                movement.SetInteractionHeading(false);
                movement.AllowMovement = movementWasAllowed;
                movement.FreezeGazeForInspection = wasFrozen;
            }
            if (interactor != null) interactor.enabled = interactionWasEnabled;
            if (cameraMode != null && cameraMode.CurrentMode != previousMode)
                cameraMode.SetCameraMode(previousMode);
            cameraMode = null;
            movement = null;
            interactor = null;
            MobileControlManager.Instance?.SetInteractionFocus(false);
        }

        private void OnDisable()
        {
            if (owner != null || movement != null || interactor != null) End();
        }

        private void ClearInput()
        {
            movement.MobileMoveInput = Vector2.zero;
            movement.MobileLookInput = Vector2.zero;
            movement.MobileSprintInput = false;
            movement.MobileCrouchInput = false;
        }
    }
}
