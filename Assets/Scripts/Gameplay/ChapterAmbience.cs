using UnityEngine;

namespace TheLastBell.Gameplay
{
    public sealed class ChapterAmbience : MonoBehaviour
    {
        [SerializeField] private Transform listener;
        [SerializeField] private AudioSource courtyard;
        [SerializeField] private AudioSource distant;
        [SerializeField] private AudioSource interior;
        [SerializeField] private AudioSource building;
        [SerializeField] private AudioLowPassFilter windFilter;
        [SerializeField] private AudioClip frameCreak;

        private float interiorBlend;
        private float dialogueGain = 1f;
        private ChapterDialogue dialogue;
        private float nextBuildingSound;

        public void Configure(Transform player, AudioSource wind, AudioSource activity, AudioSource room,
            AudioSource creaks, AudioLowPassFilter filter, AudioClip creak)
        {
            listener = player;
            courtyard = wind;
            distant = activity;
            interior = room;
            building = creaks;
            windFilter = filter;
            frameCreak = creak;
        }

        private void Start() => dialogue = FindAnyObjectByType<ChapterDialogue>();

        private void OnEnable()
        {
            nextBuildingSound = Time.time + 14f;
        }

        private void Update()
        {
            if (listener == null) return;

            float target = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1.8f, 2.8f, listener.position.z));
            interiorBlend = Mathf.MoveTowards(interiorBlend, target, Time.deltaTime * 0.73f);
            dialogueGain = Mathf.MoveTowards(dialogueGain, dialogue != null && dialogue.IsSpeaking ? 0.67f : 1f, Time.deltaTime * 1.4f);
            courtyard.volume = Mathf.Lerp(0.55f, 0.055f, interiorBlend) * dialogueGain;
            distant.volume = Mathf.Lerp(0.32f, 0f, interiorBlend) * dialogueGain;
            interior.volume = interiorBlend * 0.42f * dialogueGain;
            windFilter.cutoffFrequency = Mathf.Lerp(16000f, 1150f, interiorBlend) * dialogueGain;

            if (interiorBlend < 0.75f || Time.time < nextBuildingSound) return;
            nextBuildingSound = Time.time + Random.Range(18f, 39f);
            if (frameCreak != null)
                building.PlayOneShot(frameCreak, Random.Range(0.21f, 0.34f));
        }
    }
}
