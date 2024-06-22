﻿using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Attachments;
using InventorySystem.Items.Jailbird;
using InventorySystem.Items.Pickups;

using Mirror;

using UnityEngine;

namespace LabExtended.Extensions
{
    /// <summary>
    /// A class that holds extensions for the <see cref="ItemBase"/> and <see cref="Inventory"/> class.
    /// </summary>
    public static class ItemExtensions
    {
        /// <summary>
        /// Gets an item prefab.
        /// </summary>
        /// <typeparam name="T">The type of the item to get.</typeparam>
        /// <param name="itemType">The type of the item to get.</param>
        /// <returns>The <see cref="ItemBase"/> prefab instance if found, otherwise <see langword="null"/>.</returns>
        public static T GetItemPrefab<T>(this ItemType itemType) where T : ItemBase
            => InventoryItemLoader.TryGetItem<T>(itemType, out var item) ? item : null;

        /// <summary>
        /// Gets an instance of an item.
        /// </summary>
        /// <typeparam name="T">The type of the item to get.</typeparam>
        /// <param name="itemType">The type of the item to get.</param>
        /// <returns>The item's instance, if succesfull. Otherwise <see langword="null"/>.</returns>
        public static T GetItemInstance<T>(this ItemType itemType) where T : ItemBase
        {
            if (!InventoryItemLoader.TryGetItem<T>(itemType, out var result))
                return null;

            var item = UnityEngine.Object.Instantiate(result);

            item.ItemSerial = ItemSerialGenerator.GenerateNext();
            return item;
        }

        /// <summary>
        /// Gets a pickup instance of an item.
        /// </summary>
        /// <typeparam name="T">The type of the pickup instance to get.</typeparam>
        /// <param name="itemType">The type of the pickup instance to get.</param>
        /// <param name="position">The position to spawn the pickup at.</param>
        /// <param name="scale">The scale of the pickup item.</param>
        /// <param name="rotation">The rotation of the pickup item.</param>
        /// <param name="serial">The item's serial. If <see langword="null"/> a new one will be generated.</param>
        /// <param name="spawnPickup">Whether or not to spawn the pickup for players.</param>
        /// <returns>The pickup instance, if found. Otherwise <see langword="null"/>.</returns>
        public static T GetPickupInstance<T>(this ItemType itemType, Vector3? position = null, Vector3? scale = null, Quaternion? rotation = null, ushort? serial = null, bool spawnPickup = false) where T : ItemPickupBase
        {
            if (!InventoryItemLoader.TryGetItem<ItemBase>(itemType, out var itemBase))
                return null;

            if (itemBase.PickupDropModel is null)
                return null;

            var pickup = UnityEngine.Object.Instantiate(itemBase.PickupDropModel,
                position.HasValue ? position.Value : Vector3.zero,
                rotation.HasValue ? rotation.Value : Quaternion.identity);

            pickup.NetworkInfo = new PickupSyncInfo(itemType, itemBase.Weight, serial.HasValue ? serial.Value : ItemSerialGenerator.GenerateNext());

            if (position.HasValue)
                pickup.Position = position.Value;

            if (rotation.HasValue)
                pickup.Rotation = rotation.Value;

            if (scale.HasValue)
                pickup.transform.localScale = scale.Value;

            if (spawnPickup)
                NetworkServer.Spawn(pickup.gameObject);

            return (T)pickup;
        }

        /// <summary>
        /// Freezes the specified pickup.
        /// </summary>
        /// <param name="itemPickupBase">The pickup to freeze.</param>
        /// <returns><see langword="true"/> if the pickup was succesfully frozen, otherwise <see langword="false"/>.</returns>
        public static bool FreezePickup(this ItemPickupBase itemPickupBase)
        {
            if (itemPickupBase.PhysicsModule is null || itemPickupBase.PhysicsModule is not PickupStandardPhysics pickupStandardPhysics)
                return false;

            if (pickupStandardPhysics.Rb is null)
                return false;

            pickupStandardPhysics.Rb.isKinematic = true;
            pickupStandardPhysics.Rb.constraints = RigidbodyConstraints.FreezeAll;

            pickupStandardPhysics.ClientFrozen = true;
            return true;
        }

        /// <summary>
        /// Unfreezes the specified pickup.
        /// </summary>
        /// <param name="itemPickupBase">The pickup to unfreeze.</param>
        /// <returns><see langword="true"/> if the pickup was succesfully unfrozen, otherwise <see langword="false"/>.</returns>
        public static bool UnfreezePickup(this ItemPickupBase itemPickupBase)
        {
            if (itemPickupBase.PhysicsModule is null || itemPickupBase.PhysicsModule is not PickupStandardPhysics pickupStandardPhysics)
                return false;

            if (pickupStandardPhysics.Rb is null)
                return false;

            pickupStandardPhysics.Rb.isKinematic = false;
            pickupStandardPhysics.Rb.constraints = RigidbodyConstraints.None;

            pickupStandardPhysics.ClientFrozen = false;
            return true;
        }

        /// <summary>
        /// Unlocks the specified pickup.
        /// </summary>
        /// <param name="itemPickupBase">The pickup to unlock.</param>
        public static void UnlockPickup(this ItemPickupBase itemPickupBase)
        {
            if (itemPickupBase is null)
                return;

            var info = itemPickupBase.Info;

            info.Locked = false;
            info.InUse = false;

            itemPickupBase.NetworkInfo = info;
        }

        /// <summary>
        /// Locks the specified pickup.
        /// </summary>
        /// <param name="itemPickupBase">The pickup to lock.</param>
        public static void LockPickup(this ItemPickupBase itemPickupBase)
        {
            if (itemPickupBase is null)
                return;

            var info = itemPickupBase.Info;

            info.Locked = true;
            info.InUse = true;

            itemPickupBase.NetworkInfo = info;
        }

        /// <summary>
        /// Sets up an item (firearm preferences, jailbird status etc.)
        /// </summary>
        /// <param name="item">The item to set up.</param>
        /// <param name="owner">The item's owner.</param>
        public static void SetupItem(this ItemBase item, ReferenceHub owner = null)
        {
            if (owner != null && item.Owner != null && item.Owner != owner)
            {
                item.OwnerInventory.ServerRemoveItem(item.ItemSerial, item.PickupDropModel);
                item.Owner = null;
            }

            if (owner != null)
                item.Owner = owner;

            item.OnAdded(null);

            if (item is IAcquisitionConfirmationTrigger confirmationTrigger)
            {
                confirmationTrigger.AcquisitionAlreadyReceived = true;
                confirmationTrigger.ServerConfirmAcqusition();
            }

            if (item is Firearm firearm)
            {
                var preferenceCode = uint.MinValue;
                var flags = firearm.Status.Flags;

                if (owner is null || !AttachmentsServerHandler.PlayerPreferences.TryGetValue(owner, out var preferences)
                    || !preferences.TryGetValue(item.ItemTypeId, out preferenceCode))
                    preferenceCode = AttachmentsUtils.GetRandomAttachmentsCode(item.ItemTypeId);

                if (firearm.HasAdvantageFlag(AttachmentDescriptiveAdvantages.Flashlight))
                    flags |= FirearmStatusFlags.FlashlightEnabled;

                firearm.Status = new FirearmStatus(firearm.AmmoManagerModule.MaxAmmo, flags | FirearmStatusFlags.MagazineInserted, preferenceCode);
            }

            if (item is JailbirdItem jailbird)
                jailbird.ServerReset();
        }
    }
}