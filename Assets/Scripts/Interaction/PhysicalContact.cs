using System;
using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    public enum DeviceGripAction { Pickup, Carry, Inspect, ElectricalSeat, ElevatorSeat, Turn, Withdraw, Pocket }

    [Serializable]
    public struct HandGripProfile
    {
        public Vector3 wristAngles;
        [Range(0f, 1f)] public float fingerCurl;
        [Range(0f, 1f)] public float thumbCurl;
        public Vector3 elbowOffset;
        public Vector3 palmOffset;
        [Min(0f)] public float objectRadius;
        public Vector3 fingerJointLimits;
        public Vector4 fingerBias;
        public Vector3 thumbOpposition;

        public static HandGripProfile Handle => new HandGripProfile
        {
            fingerCurl = 0.72f, thumbCurl = 0.52f, objectRadius = 0.016f,
            elbowOffset = new Vector3(0.38f, 0.98f, 0.06f), fingerJointLimits = new Vector3(37, 61, 24),
            fingerBias = new Vector4(0.91f, 1f, 1.03f, 0.97f), thumbOpposition = new Vector3(14f, 18f, 11f)
        };
        public static HandGripProfile Lever => new HandGripProfile
        {
            wristAngles = new Vector3(-8f, 8f, 0f), fingerCurl = 0.80f, thumbCurl = 0.61f, objectRadius = 0.013f,
            palmOffset = new Vector3(0, -0.009f, 0),
            elbowOffset = new Vector3(0.31f, 0.98f, 0.17f), fingerJointLimits = new Vector3(41, 61, 27),
            fingerBias = new Vector4(0.96f, 1f, 1.03f, 0.95f), thumbOpposition = new Vector3(17f, 18f, 11f)
        };
        public static HandGripProfile Flashlight => new HandGripProfile
        {
            fingerCurl = 0.82f, thumbCurl = 0.65f, objectRadius = 0.021f,
            elbowOffset = new Vector3(0.37f, 0.95f, 0.09f), fingerJointLimits = new Vector3(35, 54, 31),
            fingerBias = new Vector4(0.91f, 1f, 1.05f, 1.03f), thumbOpposition = new Vector3(16f, 24f, 11f)
        };
        public static HandGripProfile Device => new HandGripProfile
        {
            fingerCurl = 0.79f, thumbCurl = 0.64f, objectRadius = 0.023f,
            elbowOffset = new Vector3(0.34f, 0.94f, 0.12f), fingerJointLimits = new Vector3(35, 53, 27),
            fingerBias = new Vector4(0.92f, 1f, 1.05f, 1f), thumbOpposition = new Vector3(14f, 21f, 10f)
        };

        public static HandGripProfile ForDevice(DeviceGripAction action)
        {
            var grip = Device;
            switch (action)
            {
                case DeviceGripAction.Pickup:
                    grip.elbowOffset = new Vector3(0.38f, 0.55f, 0.17f);
                    grip.fingerCurl = 0.75f;
                    break;
                case DeviceGripAction.Inspect:
                    grip.fingerCurl = 0.73f;
                    grip.thumbCurl = 0.69f;
                    break;
                case DeviceGripAction.ElectricalSeat:
                    grip.elbowOffset = new Vector3(0.36f, 0.99f, 0.14f);
                    grip.thumbCurl = 0.73f;
                    break;
                case DeviceGripAction.ElevatorSeat:
                    grip.elbowOffset = new Vector3(0.30f, 0.94f, 0.21f);
                    grip.thumbCurl = 0.74f;
                    break;
                case DeviceGripAction.Turn:
                    grip.fingerCurl = 0.83f;
                    grip.thumbCurl = 0.78f;
                    break;
                case DeviceGripAction.Withdraw:
                    grip.fingerCurl = 0.81f;
                    grip.elbowOffset = new Vector3(0.39f, 0.90f, 0.15f);
                    break;
                case DeviceGripAction.Pocket:
                    grip.fingerCurl = 0.72f;
                    grip.elbowOffset = new Vector3(0.39f, 0.80f, 0.09f);
                    break;
            }
            return grip;
        }

        public HandGripProfile Close(float factor)
        {
            var grip = this;
            grip.fingerCurl *= factor;
            grip.thumbCurl *= factor;
            return grip;
        }
    }

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
        private float reachTime;
        private float releaseWeight = 1f;

        public bool Failed { get; private set; }
        public bool Ready { get; private set; }
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
            var focus = InteractionFocus.Begin(actor, grip, owner);
            return focus == null ? null : new PhysicalContact(actor, target, focus, owner, grip, device);
        }

        public void Step(float dt)
        {
            if (Failed || profile == null || player == null || point == null || focus == null || !focus.IsOwnedBy(owner))
            {
                Failed = true;
                return;
            }
            if (!approach.Arrived)
            {
                approach.Step(dt);
                Failed = approach.Blocked;
                return;
            }

            if (!Ready && movement != null)
            {
                if (profile.RequiredPosture == RequiredInteractionPosture.Crouching) movement.SetCrouch(true);
                else if (profile.RequiredPosture == RequiredInteractionPosture.Standing) movement.SetCrouch(false);
            }
            reachTime += dt;
            float time = reachTime / profile.ReachDuration;
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, time)) * releaseWeight;
            HandGripProfile grip = profile.Grip.Close(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.53f, 1.12f, time)));
            if (heldDevice != null)
            {
                animation.SetLowReach(weight);
                animation.PoseLeftHandForDevice(point.position, heldDevice.rotation, weight, grip);
            }
            else
            {
                if (Ready && Vector3.ProjectOnPlane(point.position - player.transform.position, Vector3.up).magnitude > 0.96f)
                    releaseWeight = Mathf.MoveTowards(releaseWeight, 0f, dt * 4.2f);
                animation.SetPhysicalContact(hand, point, grip, weight);
            }
            if (Ready) return;

            float measuredTolerance = heldDevice != null ? 0.115f : 0.11f;
            bool touched = heldDevice != null && animation.LeftHandFestekSocket != null
                ? Vector3.Distance(animation.LeftHandFestekSocket.position, point.position) <= measuredTolerance
                : animation.IsPalmNear(hand, point.position, measuredTolerance);
            // Allow IK to settle, while small retargeting differences must not strand an otherwise reachable action.
            if (time >= 1.08f && touched) Ready = true;
            else if (time >= 2.05f)
            {
                bool withinArmReach = animation.CanReachByArm(hand, point.position, heldDevice != null ? 0.17f : 0.10f);
                if (withinArmReach) Ready = true;
                else if (approach.TryAlternative())
                {
                    reachTime = 0f;
                    animation.ClearPhysicalContact(hand);
                    if (heldDevice != null) animation.SetLowReach(0f);
                }
                else Failed = true;
            }
        }

        public void FadeContact(float amount)
        {
            releaseWeight = Mathf.Min(releaseWeight, Mathf.Clamp01(amount));
        }

        public void End(Transform nextFocus = null)
        {
            if (animation != null)
            {
                animation.ClearPhysicalContact(hand);
                if (heldDevice != null) animation.SetLowReach(0f);
            }
            if (movement != null && movement.IsCrouching != wasCrouching && profile != null &&
                profile.RequiredPosture != RequiredInteractionPosture.Any)
                movement.SetCrouch(wasCrouching);
            if (focus == null || !focus.IsOwnedBy(owner)) return;
            if (nextFocus == null) focus.End();
            else focus.Follow(nextFocus);
        }
    }
}
