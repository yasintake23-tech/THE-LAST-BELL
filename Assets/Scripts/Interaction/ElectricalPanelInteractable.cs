using System;
using System.Collections;
using UnityEngine;
using TheLastBell.Player;
using TheLastBell.Gameplay;

namespace TheLastBell.Interaction
{
    /// <summary>
    /// THE LAST BELL - Ana Elektrik Panosu Şalter Etkileşimi.
    /// Oyuncu şalteri çektiğinde:
    /// 1) Sağ el IK ile şalter koluna (Lever_Hand_Target) uzanır ve kolu aşağı indirir.
    /// 2) Elektrik henüz geri GELMEZ (hat kopuktur).
    /// 3) Panonun alt yuvasında gevşek duran özel "Feştek" parçası gerçek Rigidbody yerçekimiyle
    ///    yere düşer ve metalik çarpma sesi çıkarır.
    /// 4) Tekrarlı tetiklenmeyi (spam) önler.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElectricalPanelInteractable : MonoBehaviour, IInteractable
    {
        [Header("--- Pano Mekanik Parçaları ---")]
        [SerializeField] private Transform mainLeverPivot;
        [SerializeField] private Transform leverHandTarget;
        [SerializeField] private FestekItem cradledFestek;
        [SerializeField] private Vector3 safeDropFloorPoint = new Vector3(4.2f, 0.06f, 8.65f);

        [Header("--- Şalter Açısı & Zamanlama ---")]
        [SerializeField] private float pullAngleDegrees = 74f;
        [SerializeField] private float pullDuration = 0.56f;
        [SerializeField] private bool hasOperated = false;

        [Header("--- Uzamsal 3D Ses ---")]
        [SerializeField] private AudioSource panelAudioSource;
        [SerializeField] private AudioClip leverPullClip;

        private InteractionTarget leverInteractionTarget;
        private Quaternion initialLeverRot;
        private Quaternion pulledLeverRot;
        private bool isOperating;
        private bool pullCommitted;
        private PhysicalContact contact;
        private InteractionFocus focus;

        public bool HasOperated => hasOperated;
        public InteractionTarget LeverInteractionTarget => leverInteractionTarget;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void Configure(Transform leverPivot, Transform handTarget, FestekItem festek, Vector3 safeFloorPos, AudioSource audioSrc, AudioClip leverClip)
        {
            mainLeverPivot = leverPivot;
            leverHandTarget = handTarget;
            cradledFestek = festek;
            safeDropFloorPoint = safeFloorPos;
            panelAudioSource = audioSrc;
            leverPullClip = leverClip;
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (mainLeverPivot == null) mainLeverPivot = FindDeepChild(transform, "Main_Lever_Pivot");
            if (leverHandTarget == null) leverHandTarget = FindDeepChild(transform, "Lever_Hand_Target");

            if (mainLeverPivot != null && leverHandTarget != null)
            {
                if (!leverHandTarget.IsChildOf(mainLeverPivot)) leverHandTarget.SetParent(mainLeverPivot, true);
                var bar = FindDeepChild(mainLeverPivot, "Main_Lever_GripBar")?.GetComponent<Renderer>();
                if (bar != null) leverHandTarget.position = bar.bounds.center;
                // This cabinet faces the room along wrapper +Z. The grip frame follows the lever after calibration.
                leverHandTarget.rotation = Quaternion.LookRotation(transform.forward, transform.up);
            }
            if (mainLeverPivot != null)
            {
                initialLeverRot = mainLeverPivot.localRotation;
                pulledLeverRot = initialLeverRot * Quaternion.Euler(pullAngleDegrees, 0f, 0f);
            }

            if (leverInteractionTarget == null)
            {
                leverInteractionTarget = GetComponent<InteractionTarget>();
                if (leverInteractionTarget == null) leverInteractionTarget = gameObject.AddComponent<InteractionTarget>();
            }
            leverInteractionTarget.Configure(leverHandTarget != null ? leverHandTarget : transform, PreferredInteractionHand.Left, "PanelLeverPull", 0.90f, 0.25f, 0.78f);
            leverInteractionTarget.ConfigureApproach(null, null, posture: RequiredInteractionPosture.Standing);
            leverInteractionTarget.ConfigurePhysicalContact(2.18f, 0.52f, HandGripProfile.Lever);
        }

        public string GetInteractionPrompt()
        {
            if (isOperating || hasOperated) return "";
            return "[E] - Ana Şalteri İndir";
        }

        public bool CanInteract()
        {
            return !isOperating && !hasOperated;
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract()) return;

            InitializeIfNeeded();
            if (interactor != null && Application.isPlaying)
            {
                contact = PhysicalContact.TryBegin(interactor.gameObject, leverInteractionTarget, this);
                if (contact == null)
                {
                    interactor.ShowStatusMessage(PhysicalContact.ObstructionMessage);
                    return;
                }
            }

            StartCoroutine(OperateLeverRoutine(interactor));
        }

        private IEnumerator OperateLeverRoutine(PlayerInteractor interactor)
        {
            isOperating = true;
            pullCommitted = false;
            while (contact != null && !contact.Ready && !contact.Failed)
            {
                contact.Step(Time.deltaTime);
                yield return null;
            }
            if (contact != null && contact.Failed)
            {
                contact.End();
                contact = null;
                isOperating = false;
                interactor?.ShowStatusMessage(PhysicalContact.ObstructionMessage);
                yield break;
            }
            pullCommitted = true;

            if (panelAudioSource != null && leverPullClip != null)
            {
                panelAudioSource.PlayOneShot(leverPullClip, 0.92f);
            }

            bool releasedFestek = false;
            float elapsed = 0f;
            while (elapsed < pullDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / pullDuration);
                if (mainLeverPivot != null)
                {
                    mainLeverPivot.localRotation = Quaternion.Slerp(initialLeverRot, pulledLeverRot, t);
                }

                contact?.Step(Time.deltaTime);
                if (!releasedFestek && t >= 0.92f)
                {
                    releasedFestek = true;
                    if (interactor != null && cradledFestek != null)
                    {
                        if (contact != null)
                        {
                            focus = contact.Focus;
                            contact.End(cradledFestek.transform);
                            contact = null;
                        }
                        else focus = InteractionFocus.Begin(interactor.gameObject, cradledFestek.transform, this);
                        yield return new WaitForSeconds(0.46f);
                    }
                    TriggerFestekDrop();
                }

                yield return null;
            }

            if (mainLeverPivot != null) mainLeverPivot.localRotation = pulledLeverRot;
            if (!releasedFestek) TriggerFestekDrop();

            contact?.End();
            contact = null;
            hasOperated = true;
            isOperating = false;
            if (focus != null)
            {
                float follow = 0f;
                while (follow < 1.65f && cradledFestek != null && (!cradledFestek.HasLanded || follow < 0.82f))
                {
                    follow += Time.deltaTime;
                    yield return null;
                }
                focus.End();
                focus = null;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            contact?.End();
            contact = null;
            if (isOperating && pullCommitted)
            {
                if (mainLeverPivot != null) mainLeverPivot.localRotation = pulledLeverRot;
                TriggerFestekDrop();
                hasOperated = true;
            }
            isOperating = false;
            focus?.End();
            focus = null;
        }

        public void ExecuteImmediateForTest()
        {
            if (hasOperated) return;
            InitializeIfNeeded();
            if (mainLeverPivot != null) mainLeverPivot.localRotation = pulledLeverRot;
            TriggerFestekDrop();
            hasOperated = true;
            isOperating = false;
        }

        private void TriggerFestekDrop()
        {
            ChapterObjectiveManager.Instance?.NotifyPanelLeverOperated();

            if (cradledFestek != null && cradledFestek.CurrentState == FestekItemState.CradledInPanel)
            {
                // Panonun açık ön yüzünden (+Z / ileri yönü) dışarı ve aşağı hafif fiziksel ivmeyle düşür
                Vector3 outDir = transform.forward.sqrMagnitude > 0.01f ? transform.forward.normalized : Vector3.back;
                Vector3 impulse = outDir * 0.85f + Vector3.down * 0.45f + transform.right * 0.12f;
                Vector3 torque = new Vector3(0.45f, 0.25f, -0.35f);
                cradledFestek.ReleaseFromPanel(impulse, torque, safeDropFloorPoint);
            }
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (string.Equals(c.name, targetName, StringComparison.OrdinalIgnoreCase)) return c;
                Transform sub = FindDeepChild(c, targetName);
                if (sub != null) return sub;
            }
            return null;
        }
    }
}
