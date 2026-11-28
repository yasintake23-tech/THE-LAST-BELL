using UnityEngine;
using TheLastBell.Player;

namespace TheLastBell.Interaction
{
    // Plans a short movement in the same collision space as the player controller.
    // A detour is only accepted when both of its legs and the final reach are clear.
    public sealed class InteractionApproach
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly float[] Angles = { 0f, -27f, 27f, -55f, 55f, -85f, 85f, -118f, 118f, 180f };
        private readonly GameObject actor;
        private readonly InteractionTarget target;
        private readonly Transform point;
        private readonly InteractionFocus focus;
        private Vector3 waypoint, stand, chosenTargetPosition;
        private bool via, planned;
        private int retries;
        private float stalled, time;
        public bool Arrived { get; private set; }
        public bool Blocked { get; private set; }

        public InteractionApproach(GameObject player, InteractionTarget profile, Transform grip, InteractionFocus session)
        {
            actor = player;
            target = profile;
            point = grip;
            focus = session;
        }

        public static bool CanAcquire(GameObject actor, InteractionTarget target)
        {
            if (target == null) return true;
            Transform grip = target.GetHandTarget(actor.transform.position);
            if (grip == null || !target.IsWithinReach(actor.transform.position, grip)) return false;
            return Plan(actor, target, grip, actor.transform.position, -1, out _, out _, out _);
        }

        public static bool CanApproachFrom(GameObject actor, InteractionTarget target, Vector3 from)
        {
            return Plan(actor, target, target.GetHandTarget(from), from, -1, out _, out _, out _);
        }

        public bool TryAlternative()
        {
            if (retries >= 2 || time > 4.4f) return false;
            retries++;
            Arrived = false;
            Blocked = false;
            planned = false;
            return true;
        }

        public void Step(float dt)
        {
            if (Arrived || Blocked || dt <= 0f) return;
            if (focus == null || !focus.IsFocused || point == null)
            {
                Blocked = true;
                return;
            }

            time += dt;
            if (!planned || (point.position - chosenTargetPosition).sqrMagnitude > 0.12f * 0.12f)
            {
                chosenTargetPosition = point.position;
                planned = Plan(actor, target, point, actor.transform.position, retries, out stand, out waypoint, out via);
                if (!planned || time > 4f)
                {
                    Blocked = true;
                    return;
                }
            }

            Vector3 from = actor.transform.position;
            Vector3 destination = via ? waypoint : stand;
            if (Flat(destination - from).sqrMagnitude <= 0.075f * 0.075f)
            {
                if (via) via = false;
                else Arrived = true;
                return;
            }

            focus.MoveTowards(destination, target.ApproachSpeed, dt);
            stalled = Flat(actor.transform.position - from).magnitude < target.ApproachSpeed * dt * 0.18f ? stalled + dt : 0f;
            if (stalled < 0.24f) return;

            // A new blocker invalidates this path. Try the next stand and both detour sides.
            stalled = 0f;
            planned = false;
            if (++retries > 2 || time > 4f) Blocked = true;
        }

        private static bool Plan(GameObject actor, InteractionTarget profile, Transform grip, Vector3 from, int retry,
            out Vector3 stand, out Vector3 waypoint, out bool via)
        {
            stand = waypoint = default;
            via = false;
            if (grip == null) return false;
            Vector3 towardActor = Flat(from - grip.position);
            Vector3 axis = towardActor.sqrMagnitude > 0.01f ? towardActor.normalized : -actor.transform.forward;
            Vector3 gripBase = new Vector3(grip.position.x, from.y, grip.position.z);
            var capsule = actor.GetComponent<CharacterController>();
            float radius = capsule != null ? Mathf.Max(0.16f, capsule.radius - 0.025f) : 0.24f;
            float height = capsule != null ? Mathf.Max(1.04f, capsule.height - 0.08f) : 1.68f;
            float best = float.PositiveInfinity;

            for (int i = -2; i < Angles.Length; i++)
            {
                if ((i == -2 && profile.StandAnchor == null) || (i == -1 && profile.AlternativeStandAnchor == null) ||
                    (i >= 0 && i < retry)) continue;
                Vector3 candidate = i == -2 ? profile.StandAnchor.position : i == -1 ? profile.AlternativeStandAnchor.position :
                    gripBase + Quaternion.AngleAxis(Angles[i], Vector3.up) * axis * profile.ContactDistance;
                candidate.y = from.y;
                if (Flat(candidate - from).sqrMagnitude > 3.4f * 3.4f) continue;
                if (!TryGetFloor(candidate, actor.transform, out Vector3 ground)) continue;
                if (Mathf.Abs(ground.y - from.y) > 0.26f) continue;
                candidate.y = ground.y;

                Vector3 reachOrigin = candidate + Vector3.up * Mathf.Clamp(grip.position.y - candidate.y, 0.22f, 1.45f);
                if (!ClearLine(reachOrigin, grip.position, actor.transform, profile.transform)) continue;
                if (ClearLeg(from, candidate, radius, height, actor.transform))
                {
                    float score = Flat(candidate - from).magnitude + (i < 0 ? -0.30f : 0.012f * i);
                    if (score < best)
                    {
                        best = score;
                        stand = candidate;
                        via = false;
                    }
                    if (retry < 0) return true;
                    continue;
                }

                Vector3 side = Vector3.Cross(Vector3.up, Flat(candidate - from).normalized);
                for (int d = -1; d <= 1; d += 2)
                {
                    Vector3 detour = Vector3.Lerp(from, candidate, 0.44f) + side * (d * 0.67f);
                    float score = (detour - from).magnitude + (candidate - detour).magnitude + 0.24f;
                    if (score >= best || !ClearLeg(from, detour, radius, height, actor.transform) ||
                        !ClearLeg(detour, candidate, radius, height, actor.transform)) continue;
                    best = score;
                    waypoint = detour;
                    stand = candidate;
                    via = true;
                    if (retry < 0) return true;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        private static bool TryGetFloor(Vector3 candidate, Transform actor, out Vector3 ground)
        {
            ground = default;
            float closest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(candidate + Vector3.up * 0.44f, Vector3.down,
                Hits, 0.90f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i];
                if (hit.transform.IsChildOf(actor) || hit.distance >= closest) continue;
                if (hit.normal.y < 0.65f) continue;
                closest = hit.distance;
                ground = hit.point;
            }
            return !float.IsPositiveInfinity(closest);
        }

        private static bool ClearLeg(Vector3 start, Vector3 end, float radius, float height, Transform actor)
        {
            Vector3 offset = Flat(end - start);
            if (offset.sqrMagnitude < 0.003f) return true;
            int count = Physics.CapsuleCastNonAlloc(start + Vector3.up * (radius + 0.20f),
                start + Vector3.up * Mathf.Max(radius + 0.20f, height - radius), radius,
                offset.normalized, Hits, offset.magnitude + 0.02f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (Hits[i].collider != null && !Hits[i].collider.transform.IsChildOf(actor)) return false;
            return true;
        }

        private static bool ClearLine(Vector3 start, Vector3 end, Transform actor, Transform target)
        {
            Vector3 offset = end - start;
            if (offset.sqrMagnitude < 0.003f) return true;
            int count = Physics.RaycastNonAlloc(start, offset.normalized, Hits, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = Hits[i].collider.transform;
                if (!hit.IsChildOf(actor) && !hit.IsChildOf(target)) return false;
            }
            return true;
        }

        private static Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up);
    }
}
