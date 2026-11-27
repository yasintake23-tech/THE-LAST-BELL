using UnityEngine;

namespace TheLastBell.Interaction
{
    /// <summary>
    /// THE LAST BELL - Modler Etkileim Arayz (Modular Interaction Interface).
    /// Tm kaplar, ekmeceler, notlar, anahtarlar ve dolaplar bu arayz uygular.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Ekranda grnecek ipucu metni (r: "[E] - Kapy A", "[E] - Notu Oku").
        /// </summary>
        string GetInteractionPrompt();

        /// <summary>
        /// Nesne u an etkileime hazr m? (Animasyon oynarken veya devre dyken false dner).
        /// </summary>
        bool CanInteract();

        /// <summary>
        /// Etkileim gerekletiinde arlr.
        /// </summary>
        void Interact(PlayerInteractor interactor);
    }
}
