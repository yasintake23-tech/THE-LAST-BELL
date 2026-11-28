using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastBell.Player
{
    /// <summary>
    /// THE LAST BELL - Oyuncu Envanter ve Grev Eyas Yneticisi.
    /// Toplanan anahtarlar, fener pilleri ve incelenen belgeleri saklar.
    /// </summary>
    public partial class PlayerInventory : MonoBehaviour
    {
        [Header("--- Kaynaklar ---")]
        [SerializeField] private int flashlightBatteries = 1;
        [SerializeField] private List<string> collectedKeyIds = new List<string>();

        // Olaylar (HUD ve sistemler iin)
        public event Action<string> OnItemCollected;
        public event Action<int> OnBatteriesChanged;

        public int FlashlightBatteries => flashlightBatteries;

        public void AddKey(string keyId, string displayName)
        {
            if (string.IsNullOrEmpty(keyId)) return;
            if (!collectedKeyIds.Contains(keyId))
            {
                collectedKeyIds.Add(keyId);
                Debug.Log($"[THE LAST BELL] Anahtar alnd: {displayName} (ID: {keyId})");
                OnItemCollected?.Invoke(displayName);
            }
        }

        public bool HasKey(string keyId)
        {
            if (string.IsNullOrEmpty(keyId)) return true;
            return collectedKeyIds.Contains(keyId);
        }

        public void AddBattery(int amount = 1)
        {
            flashlightBatteries = Mathf.Max(0, flashlightBatteries + amount);
            Debug.Log($"[THE LAST BELL] Pil alnd. Mevcut pil says: {flashlightBatteries}");
            OnBatteriesChanged?.Invoke(flashlightBatteries);
            OnItemCollected?.Invoke("El Feneri Pili");
        }

        public bool ConsumeBattery()
        {
            if (flashlightBatteries > 0)
            {
                flashlightBatteries--;
                OnBatteriesChanged?.Invoke(flashlightBatteries);
                return true;
            }
            return false;
        }
    }
}
