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

    public enum AnimationApprovalStatus
    {
        ApprovedForGameplay,
        ApprovedWithProceduralCorrection,
        DebugPreviewOnly,
        RequiresReplacement
    }

    [Serializable]
    public class RegisteredAnimationEntry
    {
        public string id;
        public string displayName;
        public string versionTag; // "V2" or "V1"
        public AnimationClipCategory category;
        public AnimationApprovalStatus approvalStatus;
        public string targetLayer;
        public bool defaultLoop;
        public bool usesRootHeightBakeFromFeet;
        public float recommendedSpeedMps;
        public string notes;
        public AnimationClip clip;
    }

    /// <summary>
    /// THE LAST BELL - Yeniden Kullanılabilir Animasyon Kayıt Defteri (ScriptableObject).
    /// Tüm 52 (V1 + V2) içe aktarılmış klibi sürüm, onay durumu, kategori ve hedef katman bilgisiyle saklar.
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
