using System;
using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    [Serializable]
    public struct HandGripProfile
    {
        public Vector3 wristAngles;
        [Range(0f, 1f)] public float fingerCurl;
        [Range(0f, 1f)] public float thumbCurl;
        public Vector3 elbowOffset;

        public static HandGripProfile Handle => new HandGripProfile
        {
            fingerCurl = 0.74f, thumbCurl = 0.54f, elbowOffset = new Vector3(0.38f, 0.88f, 0.06f)
        };
        public static HandGripProfile Flashlight => new HandGripProfile
        {
            fingerCurl = 0.91f, thumbCurl = 0.72f, elbowOffset = new Vector3(0.38f, 0.91f, 0.07f)
        };
        public static HandGripProfile Device => new HandGripProfile
        {
            fingerCurl = 0.83f, thumbCurl = 0.68f, elbowOffset = new Vector3(0.33f, 0.84f, 0.10f)
        };
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
        private float approachTime;
        private float reachTime;

        public bool Failed { get; private set; }
        public bool Ready { get; private set; }

        private PhysicalContact(GameObject actor, InteractionTarget target, InteractionFocus lockSession, Component requester)
        {
            player = actor;
            animation = actor.GetComponentInChildren<ProtagonistAnimationController>(true);
            profile = target;
            focus = lockSession;
            owner = requester;
            hand = target.PreferredHand == PreferredInteractionHand.Right ? PreferredInteractionHand.Right : PreferredInteractionHand.Left;
        }

        public static PhysicalContact TryBegin(GameObject actor, InteractionTarget target, Component owner)
        {
            if (actor == null || target == null || !target.IsWithinReach(actor.transform.position)) return null;
            var animation = actor.GetComponentInChildren<ProtagonistAnimationController>(true);
            if (animation == null || (animation.IsHoldingFestek && target.PreferredHand != PreferredInteractionHand.Right)) return null;
            var focus = InteractionFocus.Begin(actor, target.HandTarget, owner);
            return focus == null ? null : new PhysicalContact(actor, target, focus, owner);
        }

        public void Step(float dt)
        {
            if (Failed || profile == null || player == null || focus == null || !focus.IsOwnedBy(owner))
            {
                Failed = true;
                return;
            }

            var point = profile.HandTarget;
            Vector3 offset = Vector3.ProjectOnPlane(point.position - player.transform.position, Vector3.up);
            Vector3 step = profile.StandAnchor != null
                ? Vector3.ProjectOnPlane(profile.StandAnchor.position - player.transform.position, Vector3.up)
                : offset.normalized * Mathf.Max(0f, offset.magnitude - profile.ContactDistance);
            if (!Ready && step.magnitude > 0.05f && reachTime <= 0f)
            {
                approachTime += dt;
                focus.MoveTowards(player.transform.position + step, profile.ApproachSpeed, dt);
                if (approachTime >= profile.ApproachTimeout)
                {
                    // Wall/handle clearance can stop the capsule a few centimetres early.
                    if (offset.magnitude > profile.ContactDistance + 0.13f) Failed = true;
                    else reachTime = 0.001f;
                }
                return;
            }

            if (offset.magnitude > profile.ContactDistance + (Ready ? 0.45f : 0.16f))
            {
                if (Ready) animation.ClearPhysicalContact(hand);
                else Failed = true;
                return;
            }
            reachTime += dt;
            float blend = Mathf.SmoothStep(0f, 1f, reachTime / profile.ReachDuration);
            animation.SetPhysicalContact(hand, point, profile.Grip, blend);
            if (!Ready) Ready = reachTime >= profile.ReachDuration && animation.IsPalmNear(hand, point.position, 0.15f);
            if (!Ready && reachTime > profile.ReachDuration + 0.43f) Failed = true;
        }

        public void End(Transform nextFocus = null)
        {
            if (animation != null) animation.ClearPhysicalContact(hand);
            if (focus == null || !focus.IsOwnedBy(owner)) return;
            if (nextFocus == null) focus.End();
            else focus.Follow(nextFocus);
        }

        public InteractionFocus Focus => focus;
    }
}
