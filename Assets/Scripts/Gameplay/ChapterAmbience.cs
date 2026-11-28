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

        private void OnEnable()
        {
            nextBuildingSound = Time.time + 14f;
        }

        private void Update()
        {
            if (listener == null) return;

            float target = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1.8f, 2.8f, listener.position.z));
            interiorBlend = Mathf.MoveTowards(interiorBlend, target, Time.deltaTime * 0.73f);
            courtyard.volume = Mathf.Lerp(0.55f, 0.055f, interiorBlend);
            distant.volume = Mathf.Lerp(0.32f, 0f, interiorBlend);
            interior.volume = interiorBlend * 0.42f;
            windFilter.cutoffFrequency = Mathf.Lerp(16000f, 1150f, interiorBlend);

            if (interiorBlend < 0.75f || Time.time < nextBuildingSound) return;
            nextBuildingSound = Time.time + Random.Range(18f, 39f);
            if (frameCreak != null)
                building.PlayOneShot(frameCreak, Random.Range(0.21f, 0.34f));
        }
    }
}
