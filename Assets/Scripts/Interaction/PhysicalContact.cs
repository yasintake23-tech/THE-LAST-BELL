using System;
using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    public enum DeviceGripAction { Pickup, Carry, Inspect, ElectricalSeat, ElevatorSeat, Turn, Withdraw, Pocket }

    public enum AuthoredGrip { Relaxed, Open, Flashlight, DoorLever, DoorPull, Switch, Pickup, Carry, Seat, Turn, Withdraw, Pocket, Release }

    [Serializable]
    public struct HandGripProfile
    {
        public AuthoredGrip pose;
        [Range(0f, 1f)] public float closure;
        public Vector3 elbowOffset;
        [Min(0f)] public float objectRadius;

        public static HandGripProfile Handle => Make(AuthoredGrip.DoorPull, new Vector3(0.38f, 0.98f, 0.06f), 0.016f);
        public static HandGripProfile DoorLever => Make(AuthoredGrip.DoorLever, new Vector3(0.36f, 0.98f, 0.12f), 0.015f);
        public static HandGripProfile Lever => Make(AuthoredGrip.Switch, new Vector3(0.32f, 0.98f, 0.17f), 0.020f);
        public static HandGripProfile Flashlight => Make(AuthoredGrip.Flashlight, new Vector3(0.37f, 0.95f, 0.09f), 0.0225f);
        public static HandGripProfile Device => Make(AuthoredGrip.Carry, new Vector3(0.34f, 0.94f, 0.12f), 0.023f);

        private static HandGripProfile Make(AuthoredGrip pose, Vector3 elbow, float radius) =>
            new HandGripProfile { pose = pose, closure = 1f, elbowOffset = elbow, objectRadius = radius };

        public static HandGripProfile ForDevice(DeviceGripAction action)
        {
            var grip = Device;
            switch (action)
            {
                case DeviceGripAction.Pickup:
                    grip.pose = AuthoredGrip.Pickup;
                    grip.elbowOffset = new Vector3(0.38f, 0.55f, 0.17f);
                    break;
                case DeviceGripAction.ElectricalSeat:
                case DeviceGripAction.ElevatorSeat:
                    grip.pose = AuthoredGrip.Seat;
                    grip.elbowOffset = new Vector3(0.32f, 0.96f, 0.17f);
                    break;
                case DeviceGripAction.Turn:
                case DeviceGripAction.Inspect:
                    grip.pose = AuthoredGrip.Turn;
                    break;
                case DeviceGripAction.Withdraw:
                    grip.pose = AuthoredGrip.Withdraw;
                    break;
                case DeviceGripAction.Pocket:
                    grip.pose = AuthoredGrip.Pocket;
                    break;
            }
            return grip;
        }

        public HandGripProfile Close(float factor)
        {
            var grip = this;
            grip.closure = Mathf.Clamp01(factor);
            return grip;
        }
    }

    public enum PhysicalContactPhase { Reserved, Approach, Align, Reach, Contact, Execute, Recover, Complete, Failed }

    // Owns the input lock while a mechanism approaches, reaches and follows a live contact point.
    public sealed class PhysicalContact
    {
        private readonly GameObject player;
        private readonly ProtagonistAnimationController animation;
        private readonly InteractionTarget profile;
        private readonly InteractionFocus focus;
        private readonly Component owner;
        private readonly PreferredInteractionHand hand;
        private readonly Transform point;
        private readonly Transform heldDevice;
        private readonly InteractionApproach approach;
        private readonly FirstPersonController movement;
        private readonly bool wasCrouching;
        private static readonly AnimationCurve PickupWeight = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.36f, 0.62f, 1.65f, 1.65f),
            new Keyframe(0.72f, 0.97f, 0.55f, 0.55f), new Keyframe(1f, 1f, 0f, 0f));
        private float phaseTime;
        private float releaseWeight = 1f;

        public PhysicalContactPhase Phase { get; private set; }
        public bool Failed => Phase == PhysicalContactPhase.Failed;
        public bool Ready => Phase == PhysicalContactPhase.Contact || Phase == PhysicalContactPhase.Execute;
        public InteractionFocus Focus => focus;
        public static string ObstructionMessage => "Yol kapalı; etkileşim tamamlanamadı.";

        private PhysicalContact(GameObject actor, InteractionTarget target, InteractionFocus session, Component requester,
            Transform grip, Transform device)
        {
            player = actor;
            animation = actor.GetComponentInChildren<ProtagonistAnimationController>(true);
            profile = target;
            focus = session;
            owner = requester;
            point = grip;
            heldDevice = device;
            hand = target.PreferredHand == PreferredInteractionHand.Right ? PreferredInteractionHand.Right : PreferredInteractionHand.Left;
            approach = new InteractionApproach(actor, target, grip, session);
            movement = actor.GetComponent<FirstPersonController>();
            wasCrouching = movement != null && movement.IsCrouching;
        }

        public static PhysicalContact TryBegin(GameObject actor, InteractionTarget target, Component owner, Transform device = null, bool allowOccupiedHand = false)
        {
            if (actor == null || target == null) return null;
            var animation = actor.GetComponentInChildren<ProtagonistAnimationController>(true);
            if (animation == null || (!allowOccupiedHand && animation.IsHoldingFestek && target.PreferredHand != PreferredInteractionHand.Right)) return null;
            Transform grip = target.GetHandTarget(actor.transform.position);
            var focus = InteractionFocus.Begin(actor, grip, owner, reserveHeading: true);
            return focus == null ? null : new PhysicalContact(actor, target, focus, owner, grip, device);
        }

        public void Step(float dt)
        {
            if (Failed || Phase == PhysicalContactPhase.Complete || dt <= 0f) return;
            if (profile == null || player == null || point == null || focus == null || !focus.IsOwnedBy(owner))
            {
                Fail("Etkileşim kesildi");
                return;
            }

            phaseTime += dt;
            switch (Phase)
            {
                case PhysicalContactPhase.Reserved:
                    SetPhase(PhysicalContactPhase.Approach);
                    return;
                case PhysicalContactPhase.Approach:
                    approach.Step(dt);
                    if (approach.Blocked) { Fail(approach.Failure ?? "Geçiş engellendi"); return; }
                    if (!approach.Arrived) return;
                    focus.AlignOnce(point.position);
                    if (movement != null && profile.RequiredPosture != RequiredInteractionPosture.Any)
                        movement.SetCrouch(profile.RequiredPosture == RequiredInteractionPosture.Crouching);
                    SetPhase(PhysicalContactPhase.Align);
                    return;
                case PhysicalContactPhase.Align:
                    // One shortest-arc turn. The deadline starts the reach without scheduling another walk or turn.
                    if (focus.HeadingError > 8f && phaseTime < 1.45f) return;
                    if (owner is DoorInteractable) animation.TriggerDoorInteract();
                    SetPhase(PhysicalContactPhase.Reach);
                    return;
                case PhysicalContactPhase.Reach:
                    StepReach();
                    return;
                case PhysicalContactPhase.Contact:
                    SetPhase(PhysicalContactPhase.Execute);
                    break;
            }
            Pose(1f, 1f);
        }

        private void StepReach()
        {
            float duration = heldDevice != null ? 0.82f : Mathf.Max(0.50f, profile.ReachDuration);
            float progress = Mathf.Clamp01(phaseTime / duration);
            float extension = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.79f, progress));
            float closure = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.60f, 0.98f, progress));
            Pose(extension, closure);
            if (heldDevice != null) animation.SetLowReach(PickupWeight.Evaluate(progress));
            if (progress < 1f) return;

            // The planned, unobstructed stance is gameplay authority. Retargeted hand bones are presentation only.
            if (heldDevice != null && !InteractionApproach.HasClearReach(player, profile, point))
            {
                Fail("Parçaya uzanma yolu kapanmış");
                return;
            }
            SetPhase(PhysicalContactPhase.Contact);
        }

        private void Pose(float weight, float closure)
        {
            if (heldDevice == null && Ready && Vector3.ProjectOnPlane(point.position - player.transform.position, Vector3.up).magnitude > 0.96f)
                releaseWeight = Mathf.Min(releaseWeight, 0.6f);
            weight *= releaseWeight;
            HandGripProfile grip = profile.Grip.Close(closure * releaseWeight);
            bool touched;
            if (heldDevice != null)
            {
                animation.SetLowReach(weight);
                animation.PoseLeftHandForDevice(point.position, heldDevice.rotation, weight, grip);
                touched = animation.LeftHandFestekSocket != null &&
                    Vector3.Distance(animation.LeftHandFestekSocket.position, point.position) <= 0.115f;
            }
            else
            {
                animation.SetPhysicalContact(hand, point, grip, weight);
                touched = animation.IsPalmNear(hand, point.position, 0.11f);
            }
            focus.ReportContact(weight, touched);
        }

        public void BeginExecution() => SetPhase(PhysicalContactPhase.Execute);
        public void Interrupt(string reason) => Fail(reason);

        private void SetPhase(PhysicalContactPhase phase)
        {
            if (Phase == phase) return;
            Phase = phase;
            phaseTime = 0f;
            focus.Report(phase.ToString());
        }

        private void Fail(string reason)
        {
            Phase = PhysicalContactPhase.Failed;
            if (focus != null) focus.Report("Başarısız", reason);
        }

        public void Recover()
        {
            if (!Failed) SetPhase(PhysicalContactPhase.Recover);
            animation.ClearPhysicalContact(hand);
            if (heldDevice != null) animation.SetLowReach(0f);
            if (movement != null && movement.IsCrouching != wasCrouching &&
                profile.RequiredPosture != RequiredInteractionPosture.Any)
                movement.SetCrouch(wasCrouching);
        }

        public void FadeContact(float amount)
        {
            releaseWeight = Mathf.Min(releaseWeight, Mathf.Clamp01(amount));
        }

        public void End(Transform nextFocus = null)
        {
            if (animation != null && profile != null) Recover();
            if (!Failed && focus != null) SetPhase(PhysicalContactPhase.Complete);
            if (focus == null || !focus.IsOwnedBy(owner)) return;
            if (nextFocus == null) focus.End();
            else focus.Follow(nextFocus);
        }
    }
}
