using System.Collections;
using TheLastBell.Gameplay;
using TheLastBell.Interaction;
using TheLastBell.Player;
using TheLastBell.UI;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace TheLastBell.Cinematics
{
    public sealed class ChapterFinale : MonoBehaviour
    {
        private enum Phase { Television, Reaction, Corridor, Elevator, Approach, Interior, Trap, ChapterTwo }
        public const float DescentDuration = 6.65f;
        public const float DraggingDuration = 4.45f;
        [SerializeField] private TelevisionScreening screening;
        [SerializeField] private ChapterObjectiveManager objectives;
        [SerializeField] private ChapterDialogue dialogue;
        [SerializeField] private DialogueLine[] lines;
        [SerializeField] private FirstPersonController player;
        [SerializeField] private FestekSocketInteractable socket;
        [SerializeField] private Transform elevatorThreshold, leftDoor, rightDoor;
        [SerializeField] private Collider thresholdBlocker;
        [SerializeField] private ParticleSystem gas;
        [SerializeField] private Transform dragStart, awakening;
        [SerializeField] private GameObject awakeningRoom;
        [SerializeField] private Shader eyelidShader;
        [SerializeField] private AnimationClip fallClip, getUpClip;
        [SerializeField] private AudioSource cabinSound, bodySound, music;
        [SerializeField] private AudioClip slam, hiss, distress, torchImpact, collapse, descent, score, dragging, roomTone;
        private Phase phase;
        private Vector3 leftClosed, rightClosed;
        private bool trapRunning, backgrounded;
        private float priorFear;
        private InteractionFocus trapFocus;
        private ProtagonistAnimationController animation;
        private CameraModeController cameraMode;
        private CharacterProceduralController procedural;
        private CharacterController capsule;
        private PlayerInventory inventory;
        private FlashlightPhysicalItem torch;
        private Material eyelids;
        private GameObject overlay;
        private Text chapter;
        private PlayableGraph graph;
        private AnimationClipPlayable pose;
        private Transform aim;
        private float Delta => backgrounded ? 0f : Time.deltaTime;
        public TelevisionScreening Screening => screening;
        public Transform ElevatorThreshold => elevatorThreshold;
        public FestekSocketInteractable ElevatorSocket => socket;
        public Transform Awakening => awakening;
        public bool AssetsReady => lines != null && lines.Length == 4 && System.Array.TrueForAll(lines, line => line != null && line.clip != null) &&
            fallClip != null && getUpClip != null && eyelidShader != null && gas != null && thresholdBlocker != null &&
            leftDoor != null && rightDoor != null && descent != null && score != null && dragging != null;

        public void Configure(TelevisionScreening tv, ChapterObjectiveManager objective, ChapterDialogue voices,
            DialogueLine[] dialogueLines, FirstPersonController actor, FestekSocketInteractable cabSocket,
            Transform threshold, Transform left, Transform right, Collider blocker, ParticleSystem vapor,
            Transform dragAnchor, Transform wakeAnchor, GameObject finalRoom, Shader lids, AnimationClip fall, AnimationClip rise,
            AudioSource cab, AudioSource foley, AudioSource musicSource, AudioClip[] audio)
        {
            screening = tv;
            objectives = objective;
            dialogue = voices;
            lines = dialogueLines;
            player = actor;
            socket = cabSocket;
            elevatorThreshold = threshold;
            leftDoor = left;
            rightDoor = right;
            thresholdBlocker = blocker;
            gas = vapor;
            dragStart = dragAnchor;
            awakening = wakeAnchor;
            awakeningRoom = finalRoom;
            eyelidShader = lids;
            fallClip = fall;
            getUpClip = rise;
            cabinSound = cab;
            bodySound = foley;
            music = musicSource;
            slam = audio[0];
            hiss = audio[1];
            distress = audio[2];
            torchImpact = audio[3];
            collapse = audio[4];
            descent = audio[5];
            score = audio[6];
            dragging = audio[7];
            roomTone = audio[8];
        }

        private void Awake()
        {
            if (player == null || !AssetsReady) return;
            leftClosed = leftDoor.localPosition;
            rightClosed = rightDoor.localPosition;
            animation = player.GetComponentInChildren<ProtagonistAnimationController>(true);
            procedural = player.GetComponentInChildren<CharacterProceduralController>(true);
            capsule = player.GetComponent<CharacterController>();
            inventory = player.GetComponent<PlayerInventory>();
            cameraMode = player.GetComponent<CameraModeController>();
            torch = player.GetComponentInChildren<FlashlightPhysicalItem>(true);
            aim = new GameObject("Finale_Actor_Focus").transform;
            aim.SetParent(transform, false);
        }

        private void OnEnable()
        {
            if (screening != null) screening.Finished += FinishScreening;
            if (dialogue != null) dialogue.Completed += DialogueComplete;
            if (socket != null) socket.ElevatorLocked += StartTrap;
        }

        private void OnDisable()
        {
            if (screening != null) screening.Finished -= FinishScreening;
            if (dialogue != null) dialogue.Completed -= DialogueComplete;
            if (socket != null) socket.ElevatorLocked -= StartTrap;
            StopAllCoroutines();
            if (!trapRunning) return;
            socket?.ReleaseFocus();
            ReleaseActor();
        }

        private void FinishScreening()
        {
            if (phase != Phase.Television) return;
            phase = Phase.Reaction;
            dialogue.Enqueue(lines[0]);
        }

        private void DialogueComplete(string id)
        {
            if (phase == Phase.Reaction && id == lines[0].id)
            {
                phase = Phase.Corridor;
                objectives.SetFinaleObjective("Etrafı araştır.");
            }
            else if (phase == Phase.Elevator && id == lines[1].id)
            {
                StartCoroutine(MoveDoors(1f, 1.38f));
                objectives.SetFinaleObjective("Asansörü araştır.");
            }
        }

        private void Update()
        {
            if (player == null || phase < Phase.Corridor || phase >= Phase.Trap || Delta <= 0f) return;
            Vector3 local = elevatorThreshold.InverseTransformPoint(player.transform.position);
            if (phase == Phase.Corridor && Mathf.Abs(local.x) < 1.35f && local.z > -23f && local.z <= -1f)
            {
                phase = Phase.Elevator;
                dialogue.Enqueue(lines[1]);
            }

            if (phase == Phase.Elevator && !thresholdBlocker.enabled && local.z >= -2.35f && Mathf.Abs(local.x) < 1.4f)
            {
                phase = Phase.Approach;
                dialogue.Enqueue(lines[2]);
                objectives.SetFinaleObjective("Asansöre gir.");
            }

            if (phase == Phase.Approach && IsCabInterior(local))
            {
                phase = Phase.Interior;
                dialogue.Enqueue(lines[3]);
            }

            if (phase == Phase.Interior)
            {
                bool inside = IsCabInterior(local);
                socket.AllowElevatorUse = inside;
                bool ready = socket.SocketState == FestekSocketState.Activating ||
                    socket.SocketState == FestekSocketState.SeatedReadyToActivate;
                objectives.SetFinaleObjective(!inside ? "Asansöre gir." : ready ? "Feştek’i çevir." : "Feştek’i asansör panelinde kullan.");
            }
        }

        public static bool IsCabInterior(Vector3 local) => Mathf.Abs(local.x) <= 1.14f && local.z > 0.42f && local.z < 2.45f;

        private IEnumerator MoveDoors(float open, float seconds)
        {
            if (open == 0f) thresholdBlocker.enabled = true;
            Vector3 fromL = leftDoor.localPosition;
            Vector3 fromR = rightDoor.localPosition;
            Vector3 destL = leftClosed + Vector3.left * (1.05f * open);
            Vector3 destR = rightClosed + Vector3.right * (1.05f * open);
            for (float t = 0f; t < seconds; t += Delta)
            {
                float blend = Mathf.SmoothStep(0f, 1f, t / seconds);
                leftDoor.localPosition = Vector3.Lerp(fromL, destL, blend);
                rightDoor.localPosition = Vector3.Lerp(fromR, destR, blend);
                yield return null;
            }
            leftDoor.localPosition = destL;
            rightDoor.localPosition = destR;
            thresholdBlocker.enabled = open == 0f;
        }

        private void StartTrap()
        {
            if (phase != Phase.Interior || trapRunning) return;
            phase = Phase.Trap;
            trapRunning = true;
            priorFear = animation != null ? animation.FearLevel : 0f;
            animation?.SetFearLevel(0.55f);
            animation?.TriggerImpactFlinch();
            objectives.SetFinaleObjective(null);
            StartCoroutine(Trap());
        }

        private IEnumerator Trap()
        {
            InteractionFocus initialFocus = socket.ActiveFocus;
            Vector3 localStart = elevatorThreshold.InverseTransformPoint(player.transform.position);
            // The authored camera direction faces the rear panel. Recoil is toward the shut doors.
            Vector3 recoil = elevatorThreshold.TransformPoint(new Vector3(
                Mathf.Clamp(localStart.x + 0.18f, -0.65f, 0.65f), localStart.y, Mathf.Max(0.89f, localStart.z - 0.56f)));
            StartCoroutine(MoveDoors(0f, 0.28f));
            bool slammed = false;
            for (float t = 0f; t < 0.69f; t += Delta)
            {
                initialFocus?.MoveTowards(recoil, t < 0.28f ? 0.75f : 1.18f, Delta);
                yield return null;
                if (!slammed && t >= 0.28f)
                {
                    slammed = true;
                    Play(cabinSound, slam, 0.92f);
                }
            }
            if (!slammed) Play(cabinSound, slam, 0.92f);
            yield return socket.RecoverElevatorItem();
            if (inventory != null) yield return inventory.StoreEquippedForCinematic();
            aim.position = elevatorThreshold.TransformPoint(new Vector3(0f, 1.50f, 0.12f));
            initialFocus?.Follow(aim);
            Vector3 collapsePosition = elevatorThreshold.TransformPoint(new Vector3(0.06f, localStart.y, 1.62f));
            for (float t = 0f; t < 0.68f; t += Delta)
            {
                initialFocus?.MoveTowards(collapsePosition, 1.08f, Delta);
                yield return null;
            }
            socket.ReleaseFocus();
            trapFocus = InteractionFocus.Begin(player.gameObject, aim, this, steerGaze: false);
            cameraMode?.SetCameraMode(CameraViewMode.FirstPerson);
            CreateOverlay();

            gas.Play();
            Play(cabinSound, hiss, 0.55f);
            Play(bodySound, distress, 0.7f);
            for (float t = 0f; t < 2.55f; t += Delta)
            {
                float strain = Mathf.SmoothStep(0f, 1f, t / 2.55f);
                animation?.SetFearLevel(Mathf.Lerp(0.55f, 0.91f, strain));
                animation?.SetLowReach(strain * 0.29f);
                SetEyeCamera(Quaternion.Euler(Mathf.Sin(t * 2.7f) * 3f + strain * 8f,
                    player.transform.eulerAngles.y + Mathf.Sin(t * 1.9f) * 3.4f, Mathf.Sin(t * 2.8f) * strain * 5.5f), 0f);
                SetEyes(1f - strain * 0.23f);
                animation?.SetFlashlightGripWeight(1f - Mathf.Clamp01((t - 1.42f) / 0.96f));
                yield return null;
            }
            torch?.LoseInElevator(torchImpact);
            MobileControlManager.Instance?.SetFlashlightAvailable(false);
            animation?.SetCinematicOverride(true);
            StartPose(fallClip);
            Quaternion fallFrom = Camera.main != null ? Camera.main.transform.rotation : player.transform.rotation;
            float fallDuration = Mathf.Clamp(fallClip.length, 0.85f, 1.7f);
            bool landed = false;
            for (float t = 0f; t < fallDuration; t += Delta)
            {
                float f = t / fallDuration;
                SamplePose(f);
                FollowAnimatedEyes(fallFrom, Mathf.SmoothStep(0f, 1f, f));
                SetEyes(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.94f, f)));
                if (!landed && f >= 0.71f)
                {
                    landed = true;
                    Play(bodySound, collapse, 0.76f);
                }
                yield return null;
            }
            SetEyes(0f);
            gas.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Play(cabinSound, descent, 0.72f);
            Play(music, score, 0.64f);
            for (float t = 0f; t < DescentDuration; t += Delta) yield return null;

            awakeningRoom.SetActive(true);
            MoveActor(dragStart.position, dragStart.rotation, false);
            StartPose(getUpClip);
            SampleLyingPose(1f);
            Play(bodySound, dragging, 0.83f);
            for (float t = 0f; t < DraggingDuration; t += Delta)
            {
                float travel = t / DraggingDuration;
                player.transform.position = dragStart.position + dragStart.forward * (travel * 2.75f + Mathf.Sin(t * 9f) * 0.014f);
                SampleLyingPose(0.98f + 0.018f * Mathf.Sin(t * 3.1f));
                Vector3 gaze = Vector3.up + dragStart.forward * (0.18f + Mathf.Sin(t * 2.5f) * 0.027f);
                Quaternion angle = Quaternion.LookRotation(gaze, -dragStart.forward) * Quaternion.Euler(
                    Mathf.Sin(t * 8.2f) * 1.3f, Mathf.Sin(t * 3.1f) * 2.6f, Mathf.Sin(t * 5.8f) * 2.2f);
                SetEyeCamera(angle, -0.01f, maxEyeHeight: 0.35f);
                SetEyes(Peek(t, 0.35f, 1.73f, 0.62f) + Peek(t, 2.1f, 3.84f, 0.73f));
                yield return null;
            }

            SetEyes(0f);
            MoveActor(awakening.position, awakening.rotation, false);
            SampleLyingPose(1f);
            if (roomTone != null)
            {
                bodySound.clip = roomTone;
                bodySound.loop = true;
                bodySound.volume = 0.17f;
                bodySound.Play();
            }
            float awakeTime = Mathf.Clamp(getUpClip.length, 2.5f, 5f);
            const float standStart = 1.95f;
            Quaternion lyingGaze = Quaternion.LookRotation(Vector3.up + awakening.forward * 0.42f, -awakening.forward);
            for (float t = 0f; t < awakeTime + standStart + 0.10f; t += Delta)
            {
                float roll = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.83f, standStart, t));
                float f = Mathf.Clamp01((t - standStart) / awakeTime);
                if (t < standStart)
                {
                    SampleLyingPose(1f - roll);
                    Quaternion floorGaze = Quaternion.LookRotation(Vector3.down + awakening.forward * 0.48f, awakening.forward);
                    SetEyeCamera(Quaternion.Slerp(lyingGaze, floorGaze, roll), 0f);
                }
                else
                {
                    SamplePose(f);
                    Transform eyes = procedural != null ? procedural.EyeAnchor : null;
                    Quaternion head = eyes != null
                        ? Quaternion.LookRotation(eyes.forward, eyes.up) : awakening.rotation;
                    float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.71f, 1f, f));
                    SetEyeCamera(Quaternion.Slerp(head, awakening.rotation, settle), 0f);
                }
                SetEyes(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 1.51f, t)));
                if (chapter != null) chapter.color = new Color(0.91f, 0.83f, 0.70f,
                    0.94f * Mathf.SmoothStep(0f, 1f, Mathf.Min(t / 0.72f, (3.55f - t) / 0.75f)));
                yield return null;
            }
            MoveActor(awakening.position, awakening.rotation, true);
            player.SetCrouch(false);
            // Flush the gameplay gaze to the standing direction before the camera hands back.
            player.SetScriptedGazeOverride(true, awakening.eulerAngles.y, 0f, 1f);
            yield return null;
            ReleaseActor();
            phase = Phase.ChapterTwo;
        }

        private static float Peek(float time, float start, float end, float strength)
        {
            if (time <= start || time >= end) return 0f;
            return strength * Mathf.SmoothStep(0f, 1f, Mathf.Min((time - start) / 0.46f, (end - time) / 0.53f));
        }

        private void MoveActor(Vector3 position, Quaternion rotation, bool enableCollision)
        {
            if (capsule != null) capsule.enabled = false;
            player.transform.SetPositionAndRotation(position, rotation);
            if (capsule != null) capsule.enabled = enableCollision;
        }

        private void SetEyeCamera(Quaternion rotation, float heightBias, float maxEyeHeight = 2.3f)
        {
            if (cameraMode == null) return;
            Transform eye = procedural != null ? procedural.EyeAnchor : null;
            Vector3 actual = eye != null ? eye.position : player.transform.position + Vector3.up * 1.62f;
            actual.y = player.transform.position.y + Mathf.Clamp(actual.y - player.transform.position.y + heightBias, 0.15f, maxEyeHeight);
            if (!awakeningRoom.activeSelf)
            {
                Vector3 local = elevatorThreshold.InverseTransformPoint(actual);
                local.x = Mathf.Clamp(local.x, -1.02f, 1.02f);
                local.y = Mathf.Clamp(local.y, 0.15f, 2.19f);
                local.z = Mathf.Clamp(local.z, 0.12f, 2.30f);
                actual = elevatorThreshold.TransformPoint(local);
            }
            cameraMode.SetFinaleEyePose(actual, rotation);
        }

        private void FollowAnimatedEyes(Quaternion initial, float blend)
        {
            Vector3 forward = procedural != null && procedural.EyeAnchor != null
                ? procedural.EyeAnchor.forward : player.transform.forward;
            Quaternion animated = Quaternion.LookRotation(forward, player.transform.up);
            SetEyeCamera(Quaternion.Slerp(initial, animated, blend), 0f);
        }

        private void StartPose(AnimationClip clip)
        {
            if (graph.IsValid()) graph.Destroy();
            var animator = animation != null ? animation.AnimatorComponent : null;
            if (animator == null || clip == null) return;
            graph = PlayableGraph.Create("Finale body pose");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            pose = AnimationClipPlayable.Create(graph, clip);
            pose.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph, "Protagonist", animator).SetSourcePlayable(pose);
            graph.Play();
        }

        private void SamplePose(float normalized)
        {
            if (!graph.IsValid()) return;
            pose.SetTime(pose.GetAnimationClip().length * Mathf.Clamp(normalized, 0f, 0.999f));
            graph.Evaluate(0f);
        }

        // The supplied get-up clip starts prone. Roll the complete humanoid pose into and out of supine
        // before resuming the original hands-to-floor and standing motion.
        private void SampleLyingPose(float supine)
        {
            SamplePose(0f);
            Animator rig = animation != null ? animation.AnimatorComponent : null;
            Transform hips = rig != null && rig.isHuman ? rig.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips == null) return;
            float turn = Mathf.Clamp01(supine) * 180f;
            Quaternion roll = Quaternion.AngleAxis(turn, player.transform.forward);
            hips.rotation = roll * hips.rotation;
            hips.position += Vector3.up * (0.105f * Mathf.Sin(turn * Mathf.Deg2Rad));
        }

        private void CreateOverlay()
        {
            var canvasGo = new GameObject("Finale_Eyelids", typeof(Canvas), typeof(CanvasScaler));
            overlay = canvasGo;
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 850;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var mask = new GameObject("Eyelids", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            mask.transform.SetParent(canvasGo.transform, false);
            mask.rectTransform.anchorMin = Vector2.zero;
            mask.rectTransform.anchorMax = Vector2.one;
            mask.rectTransform.offsetMin = mask.rectTransform.offsetMax = Vector2.zero;
            mask.raycastTarget = false;
            eyelids = new Material(eyelidShader);
            mask.material = eyelids;
            chapter = new GameObject("Chapter_Two_Title", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            chapter.transform.SetParent(canvasGo.transform, false);
            chapter.rectTransform.anchorMin = new Vector2(0.25f, 0.77f);
            chapter.rectTransform.anchorMax = new Vector2(0.75f, 0.93f);
            chapter.rectTransform.offsetMin = chapter.rectTransform.offsetMax = Vector2.zero;
            chapter.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            chapter.fontSize = 32;
            chapter.alignment = TextAnchor.MiddleCenter;
            chapter.text = "BÖLÜM 2";
            chapter.color = Color.clear;
            chapter.raycastTarget = false;
        }

        private void SetEyes(float amount)
        {
            if (eyelids != null) eyelids.SetFloat("_Openness", Mathf.Clamp01(amount));
        }

        private void ReleaseActor()
        {
            if (graph.IsValid()) graph.Destroy();
            if (capsule != null) capsule.enabled = true;
            animation?.SetCinematicOverride(false);
            animation?.SetLowReach(0f);
            animation?.SetFearLevel(priorFear);
            cameraMode?.ClearFinaleEyePose();
            if (trapFocus != null && trapFocus.IsOwnedBy(this)) trapFocus.End();
            if (eyelids != null) Destroy(eyelids);
            if (overlay != null) Destroy(overlay);
            trapRunning = false;
        }

        private void OnApplicationPause(bool paused)
        {
            backgrounded = paused;
            if (!trapRunning) return;
            SetPaused(cabinSound, paused);
            SetPaused(bodySound, paused);
            SetPaused(music, paused);
            if (gas != null && paused && gas.isPlaying) gas.Pause();
            else if (gas != null && !paused && gas.isPaused) gas.Play();
        }

        private static void SetPaused(AudioSource source, bool paused)
        {
            if (source == null) return;
            if (paused) source.Pause();
            else source.UnPause();
        }

        private static void Play(AudioSource source, AudioClip clip, float gain)
        {
            if (source != null && clip != null) source.PlayOneShot(clip, gain);
        }
    }
}
