using UnityEngine;
using TheLastBell.UI;

namespace TheLastBell.Interaction
{
    /// <summary>
    /// THE LAST BELL - Okunabilir Not ve Belge Etkileimi (Note / Document Interactable).
    /// Oyuncu nota baktnda [E] Oku karr, tklandnda kat sesini alp okuma panelini aar.
    /// </summary>
    public class NoteInteractable : MonoBehaviour, IInteractable
    {
        [Header("--- Not erii ---")]
        [SerializeField] private string noteTitle = "Yrtlm Bir Gnlk Sayfas";
        [TextArea(4, 10)]
        [SerializeField] private string noteContent = "14 Kasm 1994...\r\nOkulun alt katndan gelen uultular her geen gn daha da artyor. Mdriyet rencilerin girmesini yasaklad ama dn gece baz fsltr duydum. Sanki oradan biri bizi aryor...";

        [Header("--- Ses ---")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip paperSound;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.spatialBlend = 1f;
                    audioSource.maxDistance = 10f;
                }
            }
        }

        public string GetInteractionPrompt() => "[E] - Notu Oku";

        public bool CanInteract() => true;

        public void Interact(PlayerInteractor interactor)
        {
            if (paperSound != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(paperSound, 0.9f);
            }

            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.ShowNoteUI(noteTitle, noteContent);
            }
            else
            {
                interactor.ShowStatusMessage($"Not: {noteTitle}");
            }
        }

        public void Configure(string title, string content, AudioClip sfx)
        {
            noteTitle = title;
            noteContent = content;
            paperSound = sfx;
        }
    }
}
