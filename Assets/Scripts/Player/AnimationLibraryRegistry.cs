using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastBell.Player
{
    public enum AnimationClipCategory
    {
        Locomotion,
        UpperBody,
        Flashlight,
        Interaction,
        FullBodyAction,
        Cinematic,
        Additive
    }

    [Serializable]
    public class RegisteredAnimationEntry
    {
        public string id;
        public string displayName;
        public AnimationClipCategory category;
        public string targetLayer;
        public bool defaultLoop;
        public bool usesRootHeightBakeFromFeet;
        public string notes;
        public AnimationClip clip;
    }

    /// <summary>
    /// THE LAST BELL - Yeniden Kullanılabilir Animasyon Kayıt Defteri (ScriptableObject).
    /// Tüm içe aktarılmış klipleri kategori, hedef katman ve döngü bilgisiyle saklar;
    /// AnimationDebugPanel ve gelecekteki sinematik/NPC sistemleri için tek kaynak sağlar.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimationLibraryRegistry", menuName = "The Last Bell/Animation Library Registry")]
    public class AnimationLibraryRegistry : ScriptableObject
    {
        [SerializeField] private List<RegisteredAnimationEntry> entries = new List<RegisteredAnimationEntry>();

        public IReadOnlyList<RegisteredAnimationEntry> Entries => entries;

        public void SetEntries(List<RegisteredAnimationEntry> newEntries)
        {
            entries = newEntries ?? new List<RegisteredAnimationEntry>();
        }

        public AnimationClip FindClip(string idOrName)
        {
            if (string.IsNullOrEmpty(idOrName)) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && (string.Equals(e.id, idOrName, StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(e.displayName, idOrName, StringComparison.OrdinalIgnoreCase)))
                {
                    return e.clip;
                }
            }
            return null;
        }
    }
}
