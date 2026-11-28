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
        public bool IsFocused => owner != null;
        public bool IsOwnedBy(Component requester) => requester != null && owner == requester;

        public static InteractionFocus Begin(GameObject player, Transform target, Component requester, bool steerGaze = true)
        {
            if (player == null || requester == null) return null;
            var focus = player.GetComponent<InteractionFocus>();
            if (focus == null) focus = player.AddComponent<InteractionFocus>();
            if (focus.IsFocused) return null;
            focus.owner = requester;
            focus.steerGaze = steerGaze;
            focus.subject = target;
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
            float yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(-offset.y, new Vector2(offset.x, offset.z).magnitude) * Mathf.Rad2Deg;
            movement.SetScriptedGazeOverride(true, yaw, pitch, 0.35f);
        }

        public void MoveTowards(Vector3 destination, float speed, float deltaTime)
        {
            if (owner == null || movement == null) return;
            Vector3 offset = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
            movement.MoveForInteraction(Vector3.ClampMagnitude(offset, speed * deltaTime), deltaTime);
        }

        public void Follow(Transform target)
        {
            if (owner != null && target != null) subject = target;
        }

        public void End()
        {
            owner = null;
            subject = null;
            if (movement != null)
            {
                ClearInput();
                movement.SetScriptedGazeOverride(false, 0f, 0f);
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
