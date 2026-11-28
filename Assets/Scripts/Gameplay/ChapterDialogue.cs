using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TheLastBell.Cinematics;

namespace TheLastBell.Gameplay
{
    [Serializable]
    public struct SubtitleSegment
    {
        public float start;
        public float end;
        public string text;
        public SubtitleSegment(float start, float end, string text)
        {
            this.start = start;
            this.end = end;
            this.text = text;
        }
    }

    [Serializable]
    public sealed class DialogueLine
    {
        public string id;
        public AudioClip clip;
        public SubtitleSegment[] subtitles;
    }

    public sealed class ChapterDialogue : MonoBehaviour
    {
        [SerializeField] private AudioSource voice;
        [SerializeField] private DialogueLine arrival;
        [SerializeField] private DialogueLine approach;
        [SerializeField] private DialogueLine inside;
        [SerializeField] private Transform player;
        [SerializeField] private Transform entrance;
        [SerializeField] private ChapterObjectiveManager objective;
        [SerializeField] private SeamlessIntroManager intro;

        private readonly Queue<DialogueLine> pending = new Queue<DialogueLine>();
        private readonly HashSet<string> played = new HashSet<string>();
        private DialogueLine active;
        private Text subtitle;
        private CanvasGroup subtitleGroup;
        private bool paused;
        public event Action<string> Completed;
        public bool IsSpeaking => active != null && !paused;
        public bool SubtitlesVisible
        {
            get => PlayerPrefs.GetInt("DialogueSubtitles", 1) != 0;
            set => PlayerPrefs.SetInt("DialogueSubtitles", value ? 1 : 0);
        }
        public float DialogueVolume
        {
            get => PlayerPrefs.GetFloat("DialogueVolume", 1f);
            set => PlayerPrefs.SetFloat("DialogueVolume", Mathf.Clamp01(value));
        }

        public void Configure(AudioSource source, Transform actor, Transform door, ChapterObjectiveManager manager,
            SeamlessIntroManager cinematic, DialogueLine first, DialogueLine second, DialogueLine third)
        {
            voice = source;
            player = actor;
            entrance = door;
            objective = manager;
            intro = cinematic;
            arrival = first;
            approach = second;
            inside = third;
        }

        public void Enqueue(DialogueLine line)
        {
            if (line == null || line.clip == null || string.IsNullOrEmpty(line.id) || !played.Add(line.id)) return;
            pending.Enqueue(line);
        }

        private void Update()
        {
            if (voice == null) return;
            if (intro == null || intro.IsIntroFinished)
            {
                Enqueue(arrival);
                if (player != null && entrance != null)
                {
                    Vector3 local = entrance.InverseTransformPoint(player.position);
                    if (local.z <= 0f && local.z >= -2.65f && Mathf.Abs(local.x) <= 1.7f) Enqueue(approach);
                }
                if (objective != null && objective.CurrentState >= ChapterObjectiveState.ExploreInstitution)
                {
                    Enqueue(approach);
                    Enqueue(inside);
                }
            }

            bool shouldPause = Time.timeScale <= 0f;
            if (shouldPause != paused)
            {
                paused = shouldPause;
                if (paused) voice.Pause();
                else voice.UnPause();
            }
            if (paused) return;

            if (active != null && !voice.isPlaying)
            {
                string id = active.id;
                active = null;
                Completed?.Invoke(id);
            }
            if (active == null && pending.Count > 0)
            {
                active = pending.Dequeue();
                voice.clip = active.clip;
                voice.Play();
            }
            voice.volume = DialogueVolume;
            UpdateSubtitles();
        }

        private void UpdateSubtitles()
        {
            if (subtitle == null)
            {
                var canvas = GameObject.Find("Gameplay_HUD_Canvas");
                if (canvas == null) return;
                var safe = new GameObject("Dialogue_SafeArea", typeof(RectTransform), typeof(TheLastBell.UI.SafeAreaFitter));
                var safeRect = (RectTransform)safe.transform;
                safeRect.SetParent(canvas.transform, false);
                safeRect.anchorMin = Vector2.zero;
                safeRect.anchorMax = Vector2.one;
                safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
                var go = new GameObject("Dialogue_Subtitle", typeof(RectTransform), typeof(CanvasGroup), typeof(Text), typeof(Shadow));
                go.transform.SetParent(safeRect, false);
                subtitle = go.GetComponent<Text>();
                subtitle.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                subtitle.fontSize = 28;
                subtitle.alignment = TextAnchor.MiddleCenter;
                subtitle.color = Color.white;
                subtitle.raycastTarget = false;
                go.GetComponent<Shadow>().effectDistance = new Vector2(2, -2);
                subtitleGroup = go.GetComponent<CanvasGroup>();
                subtitleGroup.alpha = 0f;
                subtitleGroup.blocksRaycasts = false;
                subtitle.rectTransform.anchorMin = new Vector2(0.24f, 0.17f);
                subtitle.rectTransform.anchorMax = new Vector2(0.76f, 0.29f);
                subtitle.rectTransform.offsetMin = subtitle.rectTransform.offsetMax = Vector2.zero;
            }

            float time = active != null ? (float)voice.timeSamples / active.clip.frequency : -1f;
            float alpha = 0f;
            if (SubtitlesVisible && active != null && active.subtitles != null)
            {
                foreach (SubtitleSegment segment in active.subtitles)
                {
                    if (time < segment.start || time >= segment.end) continue;
                    subtitle.text = segment.text;
                    alpha = Mathf.Clamp01(Mathf.Min((time - segment.start) / 0.12f, (segment.end - time) / 0.16f));
                    break;
                }
            }
            subtitleGroup.alpha = alpha;
        }
    }
}
