using System;
using System.Collections;
using TheLastBell.Interaction;
using TheLastBell.Player;
using UnityEngine;
using UnityEngine.Video;

namespace TheLastBell.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class TelevisionScreening : MonoBehaviour, IInteractable
    {
        [SerializeField] private AbandonedTelevisionController television;
        [SerializeField] private ChapterObjectiveManager objectives;
        [SerializeField] private VideoPlayer video;
        [SerializeField] private Shader screenShader;
        [SerializeField] private Transform viewingPosition;
        [SerializeField] private Transform screenCenter;
        [SerializeField] private float screenRatio;
        [SerializeField] private string streamingFile;
        private InteractionFocus focus;
        private RenderTexture frames;
        private Material signal;
        private Material previousMaterial;
        private bool ended, failed, running, backgrounded, paused;
        public event Action Finished;

        public string StreamingFile => streamingFile;
        public bool UsesPhysicalScreen => television != null && television.ScreenRenderer != null && video != null &&
            video.renderMode == VideoRenderMode.RenderTexture && video.audioOutputMode == VideoAudioOutputMode.AudioSource;
        public bool HasPlayed { get; private set; }

        public void Configure(AbandonedTelevisionController crt, ChapterObjectiveManager objective, VideoPlayer player,
            Shader shader, Transform view, Transform center, float ratio, string fileName)
        {
            television = crt;
            objectives = objective;
            video = player;
            screenShader = shader;
            viewingPosition = view;
            screenCenter = center;
            screenRatio = ratio;
            streamingFile = fileName;
        }

        public string GetInteractionPrompt() => CanInteract() ? "[E] - Televizyonu izle" : "";
        public bool CanInteract() => !HasPlayed && !running && television != null && television.IsPoweredOn &&
            objectives != null && objectives.IsMilestoneComplete;

        public void Interact(PlayerInteractor interactor)
        {
            if (interactor == null || !CanInteract() || !Application.isPlaying) return;
            focus = InteractionFocus.Begin(interactor.gameObject, screenCenter, this);
            if (focus != null) StartCoroutine(Screening(interactor));
        }

        private IEnumerator Screening(PlayerInteractor interactor)
        {
            running = true;
            ended = failed = paused = false;
            Vector3 destination = viewingPosition.position;
            if (Vector3.Distance(destination, interactor.transform.position) < 1.65f)
            {
                for (float t = 0f; t < 1f && PlanarDistance(interactor.transform.position, destination) > 0.075f; t += ActiveDelta)
                {
                    focus.MoveTowards(destination, 0.76f, ActiveDelta);
                    yield return null;
                }
            }

            if (!string.IsNullOrEmpty(streamingFile) && screenShader != null)
            {
                frames = new RenderTexture(960, 540, 0, RenderTextureFormat.ARGB32);
                frames.Create();
                signal = new Material(screenShader);
                signal.mainTexture = frames;
                signal.SetFloat("_ScreenRatio", screenRatio);
                video.targetTexture = frames;
                video.prepareCompleted += Prepared;
                video.errorReceived += VideoError;
                video.loopPointReached += VideoEnded;
                video.source = VideoSource.Url;
                video.url = System.IO.Path.Combine(Application.streamingAssetsPath, streamingFile);
                video.EnableAudioTrack(0, true);
                video.Prepare();
                for (float t = 0f; t < 10f && !failed && !video.isPrepared; t += ActiveDelta) yield return null;
                if (!video.isPrepared) failed = true;

                if (!failed)
                {
                    while (ActiveDelta <= 0f) yield return null;
                    previousMaterial = television.ScreenRenderer.sharedMaterial;
                    television.ScreenRenderer.sharedMaterial = signal;
                    television.SetScreeningAudio(true);
                    video.Play();

                    double lastTime = -1;
                    float stagnant = 0f;
                    while (!ended && !failed && stagnant < 8f)
                    {
                        bool shouldPause = ActiveDelta <= 0f;
                        if (shouldPause != paused)
                        {
                            paused = shouldPause;
                            if (paused) video.Pause();
                            else if (!ended) video.Play();
                        }
                        if (!paused)
                        {
                            double time = video.time;
                            stagnant = time > lastTime + 0.0001 ? 0f : stagnant + ActiveDelta;
                            lastTime = time;
                        }
                        yield return null;
                    }
                    if (!ended) failed = true;
                }
            }
            else failed = true;

            bool showFailure = failed;
            Cleanup();
            // A broken signal must not strand progression on an unsupported device decoder.
            HasPlayed = true;
            if (showFailure) interactor.ShowStatusMessage("Televizyonun sinyali kesildi.");
            Finished?.Invoke();
        }

        private float ActiveDelta => backgrounded ? 0f : Time.deltaTime;
        private static float PlanarDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        private static void Prepared(VideoPlayer player)
        {
            if (player.audioTrackCount > 0) player.SetTargetAudioSource(0, player.GetComponent<AudioSource>());
        }
        private void VideoEnded(VideoPlayer player) => ended = true;
        private void VideoError(VideoPlayer player, string error)
        {
            failed = true;
            Debug.LogWarning($"[TelevisionScreening] Video playback ended with a decoder error: {error}", this);
        }
        private void OnApplicationPause(bool pausedBySystem) => backgrounded = pausedBySystem;

        private void Cleanup()
        {
            if (!running) return;
            running = false;
            video.loopPointReached -= VideoEnded;
            video.prepareCompleted -= Prepared;
            video.errorReceived -= VideoError;
            video.Stop();
            video.targetTexture = null;
            if (previousMaterial != null) television.ScreenRenderer.sharedMaterial = previousMaterial;
            previousMaterial = null;
            television.SetScreeningAudio(false);
            if (frames != null) Destroy(frames);
            if (signal != null) Destroy(signal);
            frames = null;
            signal = null;
            if (focus != null && focus.IsOwnedBy(this)) focus.End();
            focus = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Cleanup();
        }
    }
}
