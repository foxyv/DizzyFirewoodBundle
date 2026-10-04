using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Dizzy.FirewoodBundle
{
    // Open-crate helpers shared by the hook and bundle gathers.
    internal static class CrateSlots
    {
        private static readonly FieldInfo SlotItemField = AccessTools.Field(typeof(CrateInventoryButton), "currentItem");
        private static readonly FieldInfo PointedButtonField = AccessTools.Field(typeof(GoPointer), "pointedAtButton");

        internal static GoPointerButton PointedButton(GoPointer pointer)
        {
            if (pointer == null || PointedButtonField == null)
                return null;
            return PointedButtonField.GetValue(pointer) as GoPointerButton;
        }

        internal static ShipItem ItemOnSlot(GoPointerButton button)
        {
            CrateInventoryButton slot = button as CrateInventoryButton;
            if (slot == null || SlotItemField == null)
                return null;
            return SlotItemField.GetValue(slot) as ShipItem;
        }

        // The crate open on screen, if it holds this item.
        internal static CrateInventory OpenCrateHolding(ShipItem item)
        {
            if (item == null || CrateInventoryUI.instance == null || !CrateInventoryUI.instance.showingUI)
                return null;
            CrateInventory inventory = CrateInventoryUI.instance.currentCrate;
            if (inventory == null || inventory.containedItems == null || !inventory.containedItems.Contains(item))
                return null;
            return inventory;
        }

        internal static CrateInventory CrateWithId(int crateId)
        {
            if (CrateInventoryUI.instance != null && CrateInventoryUI.instance.currentCrate != null)
            {
                CrateInventory open = CrateInventoryUI.instance.currentCrate;
                SaveablePrefab openSave = open.GetComponent<SaveablePrefab>();
                if (openSave != null && openSave.instanceId == crateId)
                    return open;
            }

            CrateInventory[] inventories = Object.FindObjectsOfType<CrateInventory>();
            for (int i = 0; i < inventories.Length; i++)
            {
                SaveablePrefab crateSave = inventories[i].GetComponent<SaveablePrefab>();
                if (crateSave != null && crateSave.instanceId == crateId)
                    return inventories[i];
            }

            return null;
        }

        // Redraws the open crate after items were taken out, and clears slots
        // that still point at an item that is gone or shown twice.
        internal static void Refresh(CrateInventory inventory)
        {
            if (inventory == null
                || CrateInventoryUI.instance == null
                || !CrateInventoryUI.instance.showingUI
                || CrateInventoryUI.instance.currentCrate != inventory)
                return;
            CrateInventoryUI.instance.RefreshButtons();
            ReleaseShrunkSlots(inventory);
        }

        internal static void ReleaseShrunkSlots(CrateInventory inventory)
        {
            if (inventory == null || CrateInventoryUI.instance == null || CrateInventoryUI.instance.buttons == null)
                return;
            if (SlotItemField == null)
                return;

            var seen = new List<ShipItem>();
            CrateInventoryButton[] buttons = CrateInventoryUI.instance.buttons;
            for (int i = 0; i < buttons.Length; i++)
            {
                CrateInventoryButton button = buttons[i];
                if (button == null)
                    continue;
                ShipItem shown = SlotItemField.GetValue(button) as ShipItem;
                if (shown == null)
                    continue;
                bool live = inventory.containedItems != null && inventory.containedItems.Contains(shown);
                if (!live || seen.Contains(shown))
                {
                    SlotItemField.SetValue(button, null);
                    continue;
                }

                seen.Add(shown);
            }
        }

        internal static void ClearCrateSlot(ShipItem item)
        {
            if (item == null || CrateInventoryUI.instance == null || CrateInventoryUI.instance.buttons == null)
                return;
            if (SlotItemField == null)
                return;

            CrateInventoryButton[] buttons = CrateInventoryUI.instance.buttons;
            for (int i = 0; i < buttons.Length; i++)
            {
                CrateInventoryButton button = buttons[i];
                if (button != null && SlotItemField.GetValue(button) as ShipItem == item)
                    SlotItemField.SetValue(button, null);
            }
        }
    }
}
