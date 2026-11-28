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

        public Transform HandTarget => handTarget != null ? handTarget : transform;
        public Transform StandAnchor => standAnchor;
        public PreferredInteractionHand PreferredHand => preferredHand;
        public RequiredInteractionPosture RequiredPosture => requiredPosture;
        public float MaxReachDistance => maxReachDistance;
        public bool AlignCharacterToTarget => alignCharacterToTarget;
        public bool RestrictMovementDuringContact => restrictMovementDuringContact;
        public string AnimationTrigger => animationTrigger;
        public float TotalDuration => totalDuration;
        public float ContactNormalizedTime => contactNormalizedTime;
        public float ReleaseNormalizedTime => releaseNormalizedTime;

        public bool IsWithinReach(Vector3 characterPosition)
        {
            Vector3 targetPos = HandTarget.position;
            Vector3 delta = targetPos - characterPosition;
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
