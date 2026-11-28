using UnityEngine;

namespace TheLastBell.Interaction
{
    public enum PreferredInteractionHand
    {
        Left,
        Right,
        Either
    }

    public enum RequiredInteractionPosture
    {
        Any,
        Standing,
        Crouching
    }

    /// <summary>
    /// THE LAST BELL - Yeniden Kullanılabilir Hedef Tabanlı Çevresel Etkileşim Tanımı.
    /// Kapı kolları, çekmeceler, dolaplar, anahtarlar, düğmeler, duvar desteği ve hikâye nesneleri için
    /// gerçek dünya el hedefini, tutuş yönelimini, mesafe/duruş gereksinimlerini ve IK temas zamanlamasını tanımlar.
    /// </summary>
    public class InteractionTarget : MonoBehaviour
    {
        [Header("--- Fiziksel Hedefler ---")]
        [SerializeField] private Transform handTarget;
        [SerializeField] private Transform standAnchor;
        [SerializeField] private Transform alternativeStandAnchor;
        [SerializeField] private Transform oppositeGrip;
        [SerializeField] private PreferredInteractionHand preferredHand = PreferredInteractionHand.Left;
        [SerializeField] private RequiredInteractionPosture requiredPosture = RequiredInteractionPosture.Any;

        [Header("--- Mesafe & Yönelim ---")]
        [SerializeField] private float maxReachDistance = 2.4f;
        [SerializeField] private bool alignCharacterToTarget = true;
        [SerializeField] private bool restrictMovementDuringContact = false;

        [Header("--- Animasyon & IK Temas Zamanlaması ---")]
        [SerializeField] private string animationTrigger = "DoorOpen";
        [SerializeField] private float totalDuration = 2.0f;
        [SerializeField] [Range(0f, 1f)] private float contactNormalizedTime = 0.38f;
        [SerializeField] [Range(0f, 1f)] private float releaseNormalizedTime = 0.65f;

        [SerializeField] private HandGripProfile grip;
        [SerializeField] private float contactDistance = 0.55f;
        [SerializeField] private float approachSpeed = 1.04f;
        [SerializeField] private float approachTimeout = 0.85f;
        [SerializeField] private float reachDuration = 0.36f;
        public HandGripProfile Grip => grip.fingerCurl > 0f ? grip : HandGripProfile.Handle;
        public float ContactDistance => Mathf.Max(0.36f, contactDistance);
        public float ApproachSpeed => Mathf.Max(0.3f, approachSpeed);
        public float ApproachTimeout => Mathf.Max(0.4f, approachTimeout);
        public float ReachDuration => Mathf.Max(0.25f, reachDuration);

        public void ConfigurePhysicalContact(float startRange, float closeRange, HandGripProfile profile)
        {
            maxReachDistance = startRange;
            contactDistance = closeRange;
            grip = profile;
        }

        public Transform HandTarget => handTarget != null ? handTarget : transform;
        public Transform StandAnchor => standAnchor;
        public Transform AlternativeStandAnchor => alternativeStandAnchor;

        public Transform GetHandTarget(Vector3 actorPosition) => oppositeGrip != null &&
            (actorPosition - oppositeGrip.position).sqrMagnitude < (actorPosition - HandTarget.position).sqrMagnitude
            ? oppositeGrip : HandTarget;

        public void ConfigureApproach(Transform stand, Transform alternative, Transform otherGrip = null,
            RequiredInteractionPosture posture = RequiredInteractionPosture.Any)
        {
            standAnchor = stand;
            alternativeStandAnchor = alternative;
            oppositeGrip = otherGrip;
            requiredPosture = posture;
        }
        public PreferredInteractionHand PreferredHand => preferredHand;
        public RequiredInteractionPosture RequiredPosture => requiredPosture;
        public float MaxReachDistance => maxReachDistance;
        public bool AlignCharacterToTarget => alignCharacterToTarget;
        public bool RestrictMovementDuringContact => restrictMovementDuringContact;
        public string AnimationTrigger => animationTrigger;
        public float TotalDuration => totalDuration;
        public float ContactNormalizedTime => contactNormalizedTime;
        public float ReleaseNormalizedTime => releaseNormalizedTime;

        public bool IsWithinReach(Vector3 characterPosition, Transform gripPoint = null)
        {
            Vector3 targetPos = (gripPoint != null ? gripPoint : HandTarget).position;
            Vector3 delta = targetPos - characterPosition;
            if (Mathf.Abs(delta.y) > 2.15f) return false;
            delta.y = 0f;
            return delta.magnitude <= maxReachDistance;
        }

        public void Configure(Transform target, PreferredInteractionHand hand, string triggerName, float duration, float contactNorm, float releaseNorm)
        {
            handTarget = target;
            preferredHand = hand;
            animationTrigger = triggerName;
            totalDuration = duration;
            contactNormalizedTime = contactNorm;
            releaseNormalizedTime = releaseNorm;
        }
    }
}
