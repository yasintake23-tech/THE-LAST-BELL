using System;
using System.Collections;
using System.Collections.Generic;
using TheLastBell.Gameplay;
using TheLastBell.Interaction;
using TheLastBell.UI;
using UnityEngine;

namespace TheLastBell.Player
{
    public partial class PlayerInventory
    {
        public sealed class PhysicalItem
        {
            public InventoryItemDefinition Definition { get; }
            internal IInventoryPhysicalItem Instance { get; }

            internal PhysicalItem(InventoryItemDefinition definition, IInventoryPhysicalItem item)
            {
                Definition = definition;
                Instance = item;
            }
        }

        private readonly List<PhysicalItem> physicalItems = new List<PhysicalItem>();
        private InteractionFocus interactionLock;
        private ProtagonistAnimationController pose;
        private Transform pocket;
        private bool transferring;

        public IReadOnlyList<PhysicalItem> PhysicalItems => physicalItems;
        public PhysicalItem EquippedItem { get; private set; }
        public bool IsTransferring => transferring;
        public event Action PhysicalItemsChanged;
        public event Action<InventoryItemDefinition> PhysicalItemStored;

        private void Start() => MobileInventoryPanel.Create(this);

        public bool TryCollectEquipped(InventoryItemDefinition definition, IInventoryPhysicalItem item)
        {
            if (definition == null || item == null || string.IsNullOrWhiteSpace(definition.ItemId) || EquippedItem != null)
                return false;
            if (physicalItems.Exists(entry => entry.Definition.ItemId == definition.ItemId || ReferenceEquals(entry.Instance, item)))
                return false;
            EquippedItem = new PhysicalItem(definition, item);
            physicalItems.Add(EquippedItem);
            PhysicalItemsChanged?.Invoke();
            OnItemCollected?.Invoke(definition.DisplayName);
            return true;
        }

        public void ToggleEquipment(PhysicalItem selected)
        {
            if (transferring || selected == null || !physicalItems.Contains(selected)) return;
            if (!Application.isPlaying)
            {
                ChangeImmediately(selected == EquippedItem ? null : selected);
                return;
            }
            var currentFocus = GetComponent<InteractionFocus>();
            if (currentFocus != null && currentFocus.IsFocused) return;
            StartCoroutine(ChangeEquipment(selected == EquippedItem ? null : selected));
        }

        private IEnumerator ChangeEquipment(PhysicalItem next)
        {
            pose = GetComponentInChildren<ProtagonistAnimationController>(true);
            pose?.InitializeIfNeeded();
            Transform wrist = pose != null && pose.LeftHandFestekSocket != null ? pose.LeftHandFestekSocket.parent : null;
            if (wrist == null) yield break;
            var movement = GetComponent<FirstPersonController>();
            if (movement == null || !movement.AllowMovement) yield break;
            Transform target = transform;
            interactionLock = InteractionFocus.Begin(gameObject, target, this, steerGaze: false);
            if (interactionLock == null) yield break;
            transferring = true;
            PhysicalItemsChanged?.Invoke();
            pose.IsPosingFestek = true;

            Transform pocketTarget = GetPocket();
            if (EquippedItem != null)
            {
                yield return ReachPocket(wrist, pocketTarget, EquippedItem.Definition);
                if (!IsAtPocket(wrist, pocketTarget))
                {
                    EndTransfer();
                    yield break;
                }
                CommitStorage();
            }
            if (next != null)
            {
                yield return ReachPocket(wrist, pocketTarget, next.Definition);
                if (IsAtPocket(wrist, pocketTarget) && next.Instance.TryEquip(gameObject))
                {
                    EquippedItem = next;
                    for (float t = 0f; t < next.Definition.PocketDuration; t += Time.deltaTime)
                    {
                        pose.SetPhysicalWrist(PreferredInteractionHand.Left, pocketTarget.position, pocketTarget.rotation,
                            next.Definition.Grip, 1f - Mathf.SmoothStep(0f, 1f, t / next.Definition.PocketDuration));
                        yield return null;
                    }
                }
            }
            EndTransfer();
        }

        public IEnumerator StoreEquippedForCinematic()
        {
            if (EquippedItem == null || transferring) yield break;
            if (!Application.isPlaying)
            {
                CommitStorage();
                yield break;
            }
            pose = GetComponentInChildren<ProtagonistAnimationController>(true);
            pose?.InitializeIfNeeded();
            Transform wrist = pose != null && pose.LeftHandFestekSocket != null ? pose.LeftHandFestekSocket.parent : null;
            transferring = true;
            if (wrist != null)
            {
                pose.IsPosingFestek = true;
                yield return ReachPocket(wrist, GetPocket(), EquippedItem.Definition);
            }
            CommitStorage();
            EndTransfer();
        }

        private IEnumerator ReachPocket(Transform wrist, Transform target, InventoryItemDefinition definition)
        {
            Vector3 from = transform.InverseTransformPoint(wrist.position);
            Quaternion fromRotation = Quaternion.Inverse(transform.rotation) * wrist.rotation;
            Vector3 wayPoint = new Vector3(-0.33f, target.localPosition.y + 0.13f, 0.18f);
            float duration = definition.PocketDuration;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float blend = Mathf.SmoothStep(0f, 1f, t / duration);
                Vector3 path = Vector3.Lerp(Vector3.Lerp(from, wayPoint, blend),
                    Vector3.Lerp(wayPoint, target.localPosition, blend), blend);
                pose.SetPhysicalWrist(PreferredInteractionHand.Left, transform.TransformPoint(path),
                    Quaternion.Slerp(transform.rotation * fromRotation, target.rotation, blend), definition.Grip, 1f);
                yield return null;
            }
            pose.SetPhysicalWrist(PreferredInteractionHand.Left, target.position, target.rotation, definition.Grip, 1f);
            yield return null;
            yield return null;
        }

        private bool IsAtPocket(Transform wrist, Transform target)
        {
            if (Vector3.Distance(wrist.position, target.position) <= 0.13f) return true;
            GetComponent<PlayerInteractor>()?.ShowStatusMessage("Cebine ulaşmak için ayağa kalk.");
            return false;
        }

        private Transform GetPocket()
        {
            if (pocket != null) return pocket;
            pocket = new GameObject("Inventory_Left_Pocket").transform;
            pocket.SetParent(transform, false);
            Transform hips = pose != null && pose.AnimatorComponent != null
                ? pose.AnimatorComponent.GetBoneTransform(HumanBodyBones.Hips) : null;
            float hipHeight = hips != null ? transform.InverseTransformPoint(hips.position).y - 0.09f : 0.84f;
            pocket.localPosition = new Vector3(-0.23f, Mathf.Clamp(hipHeight, 0.64f, 0.93f), 0.07f);
            pocket.localRotation = Quaternion.Euler(8f, -14f, 78f);
            return pocket;
        }

        private void ChangeImmediately(PhysicalItem next)
        {
            if (EquippedItem != null) CommitStorage();
            if (next != null && next.Instance.TryEquip(gameObject)) EquippedItem = next;
            PhysicalItemsChanged?.Invoke();
        }

        private void CommitStorage()
        {
            PhysicalItem item = EquippedItem;
            item.Instance.StoreAt(GetPocket());
            EquippedItem = null;
            if (pose == null) pose = GetComponentInChildren<ProtagonistAnimationController>(true);
            if (pose != null) pose.IsHoldingFestek = false;
            if (item.Instance is FestekItem) ChapterObjectiveManager.Instance?.NotifyFestekStored();
            PhysicalItemStored?.Invoke(item.Definition);
        }

        private void EndTransfer()
        {
            if (pose != null)
            {
                pose.ClearPhysicalContact(PreferredInteractionHand.Left);
                pose.IsPosingFestek = false;
            }
            pose = null;
            interactionLock?.End();
            interactionLock = null;
            transferring = false;
            PhysicalItemsChanged?.Invoke();
        }

        private void OnDisable()
        {
            if (!transferring) return;
            StopAllCoroutines();
            EndTransfer();
        }
    }
}
